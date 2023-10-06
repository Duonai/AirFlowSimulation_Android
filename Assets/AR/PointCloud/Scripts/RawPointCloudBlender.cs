using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
using OpenCVForUnity.CoreModule;
using OpenCVForUnity.Features2dModule;
using OpenCVForUnity.UnityUtils;
using OpenCVForUnity.ImgprocModule;
using OpenCVForUnity.Calib3dModule;

public class RawPointCloudBlender : MonoBehaviour
{
    /// <summary>
    /// Type of depth texture to attach to the material.
    /// </summary>
    public bool UseRawDepth = true;

    // Limit the number of points to bound the performance cost of rendering the point cloud.
    private const int _maxVerticesInBuffer = 1000000;
    private bool _initialized;
    private ARCameraManager _cameraManager;
    private XRCameraIntrinsics _cameraIntrinsics;
    private Mesh _mesh;

    private Vector3[] _vertices = new Vector3[_maxVerticesInBuffer];
    private int _verticesCount = 0;
    private int _vertextDrawCount = 0;
    private int _verticesIndex = 0;
    private int[] _indices = new int[_maxVerticesInBuffer];
    private Color32[] _colors = new Color32[_maxVerticesInBuffer];

    // Buffers that store the color camera image (in YUV420_888 format) each frame.
    private byte[] _cameraBufferY;
    private byte[] _cameraBufferU;
    private byte[] _cameraBufferV;
    private int _cameraHeight;
    private int _cameraWidth;
    private int _pixelStrideUV;
    private int _rowStrideY;
    private int _rowStrideUV;
    private Material _pointCloudMaterial;
    private bool _cachedUseRawDepth = false;

    private Vector3 _lastCameraForward;
    private Vector3 _lastCameraPose;

    //for Space scan
    private float max_x = 0;
    private float min_x = 0;

    private float max_y = 0;
    private float min_y = 0;

    private float max_z = 0;
    private float min_z = 0;

    private float gridLengthX = 0;
    private float gridLengthY = 0;
    private float gridLengthZ = 0;

    private int gridSizeX = 0;
    private int gridSizeY = 0;
    private int gridSizeZ = 0;

    private GridArray[] gridArray;
    private bool[] boolGrid;

    private bool gridCreated = false;

    bool startScan = false;
    bool isPCView = true;

    //gridEditView
    public GameObject occGridbox;
    List<GameObject> boxList;
    bool editMode = false;

    struct GridArray
    {
        public int pointCount;
        public bool occ;
        public bool fixedFlag;
        public Vector3 position;

        public GridArray(int _pointCount = 0, bool _occ = false, bool _fixedFlag = false, Vector3? _position = null)
        {
            pointCount = _pointCount;
            occ = _occ;
            fixedFlag = _fixedFlag;
            position = _position.HasValue? _position.Value : new Vector3(0,0,0);
        }
    }

    //Denoise grid
    private int denoiseCount = 0;
    private denoiseGrid[] denoiseGridArray;
    private List<Vector3> denoiseVertices;
    private List<Vector3> denoiseColors;

    struct denoiseGrid
    {
        public int pointCount;
        public Vector3 position;
        public Vector3 color;

        public denoiseGrid(int _pointCount = 0, Vector3? _position = null, Vector3? _color = null)
        {
            pointCount = _pointCount;
            position = _position.HasValue ? _position.Value : new Vector3(0, 0, 0);
            color = _color.HasValue ? _color.Value : new Vector3(0, 0, 0);
        }
    }

    //Optimization openCV part
    Vector3 prevPose = new Vector3(0, 0, 0);
    Vector3 prevDir = new Vector3(0, 0, 0);
    Vector3 currPose = new Vector3(0, 0, 0);
    Vector3 currDir = new Vector3(0, 0, 0);

    bool firstImage = false;

    //Opt lists
    List<pointGroup> _optList;

    struct pointGroup
    {
        public Texture2D frame;
        public Matrix4x4 localWorldMat;
        public short[] depth;
        public byte[] conf;
        public Vector3 dir;
        public Vector3 pose;
        public int index;

        public pointGroup(Texture2D _frame, Matrix4x4 _localWorldMat, short[] _depth, byte[] _conf, Vector3 _dir, Vector3 _pose, int _index)
        {
            frame = _frame;
            localWorldMat = _localWorldMat;
            depth = _depth;
            conf = _conf;
            dir = _dir;
            pose = _pose;
            index = _index;
        }
    }

    Texture2D _imageTexture;
    Texture2D _prevTexture;
    Texture2D _currTexture;

    // ORB feature module
    ORB feature;
    // Feature matching module (Use brute-force module)
    BFMatcher matcher;

    //first texture
    // Your detection target image as gray scale
    Mat targetImageGray;
    // Extracted key points from target image
    MatOfKeyPoint targetKeyPoints;
    // Extracted descriptors from target image
    Mat targetDescriptors;

    //image texture
    MatOfKeyPoint keyPoints;
    Mat descriptors;

    Thread optThread;
    Vector3 simTrans;

    //Plane inlier
    ARPlaneManager _planeManager;
    Camera _camera;
    public GameObject planePrefab; //temp

    List<planeGroup> _planeList;
    int prevPointCount = 0;
    int currPointCount = 0;
    bool updatePlane = false;

    struct planeGroup
    {
        public TrackableId planeID;
        public Vector3 planeCenter;
        public Vector3 planeNormal;
        public Vector3 center;
        public List<bool> valid;
        public List<int> count;
        public List<Vector3> maxPos;
        public List<Vector3> minPos;
        public bool planeValid;
        public List<int> pointIndex;
        
        public GameObject planeView; //test

        public planeGroup(TrackableId _planeID, Vector3 _planeCenter, Vector3 _planeNormal, Vector3 _center, List<bool> _valid, List<int> _count, 
            List<Vector3> _maxPos, List<Vector3> _minPos, bool _planeValid, List<int> _pointIndex)
        {
            planeID = _planeID;
            planeCenter = _planeCenter;
            planeNormal = _planeNormal;
            center = _center;
            valid = _valid;
            count = _count;
            maxPos = _maxPos;
            minPos = _minPos;
            planeValid = _planeValid;
            pointIndex = _pointIndex;

            planeView = null; //test
        }
    }

    /// <summary>
    /// Resets the point cloud renderer.
    /// </summary>
    public void Reset()
    {
        _verticesCount = 0;
        _vertextDrawCount = 0;
        _verticesIndex = 0;

        Debug.Log("reset");
    }

    private void Start()
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _pointCloudMaterial = GetComponent<Renderer>().material;
        _cameraManager = FindObjectOfType<ARCameraManager>();
        _cameraManager.frameReceived += OnCameraFrameReceived;

        _planeManager = FindObjectOfType<ARPlaneManager>();
        _planeManager.planesChanged += OnARPlaneChanged;
        _camera = FindObjectOfType<Camera>();

        _lastCameraPose = _cameraManager.transform.position;
        _lastCameraForward = _cameraManager.transform.forward;
        
        _optList = new List<pointGroup>();
        _planeList = new List<planeGroup>();
        denoiseVertices = new List<Vector3>();
        denoiseColors = new List<Vector3>();

        // Sets the index buffer.
        for (int i = 0; i < _maxVerticesInBuffer; ++i)
        {
            _indices[i] = i;
        }

        Reset();
    }

    private void Update()
    {
        // Waits until Depth API is initialized.
        if (!_initialized && DepthSource.Initialized)
        {
            _initialized = true;
        }

        if (_initialized)
        {
            if (_cachedUseRawDepth != UseRawDepth)
            {
                DepthSource.SwitchToRawDepth(UseRawDepth);
                _cachedUseRawDepth = UseRawDepth;
            }

            prevPose = currPose;
            currPose = _cameraManager.transform.position;
            prevDir = currDir;
            currDir = _cameraManager.transform.forward;

            if (_currTexture && firstImage)
            {
                _prevTexture.SetPixels(_currTexture.GetPixels());
                _prevTexture.Apply();
                _currTexture.SetPixels(_imageTexture.GetPixels());
                _currTexture.Apply();
            }

            if (Vector3.Distance(prevPose, currPose) >= 0.1)
            {
                float match = calcMatch(_prevTexture, _currTexture);
                if (match <= 20)
                {
                    Debug.Log("loop");
                    Debug.Log("distance: " + Vector3.Distance(prevPose, currPose));
                    Debug.Log("match: " + match);

                    simTrans = currPose - prevPose;
                    addOptList();
                    optThread = new Thread(new ThreadStart(graphOptimization));
                    optThread.Start();
                    return;
                }
            }

            if (shouldAccumulate() && startScan)
            {
                UpdateRawPointCloud();
                calcEssen(_prevTexture, _currTexture);
                addOptList();
            }
        }
    }
    
    void OnARPlaneChanged(ARPlanesChangedEventArgs eventArgs)
    {
        //bool inView = false;
        foreach (ARPlane plane in eventArgs.added)
        {
            int num = (plane.extents.x > plane.extents.y) ? (int)(plane.extents.x / 0.1f) : (int)(plane.extents.y / 0.1f);
            List<bool> tempValid = new List<bool>();
            List<int> tempCount = new List<int>();
            List<Vector3> tempMax = new List<Vector3>();
            List<Vector3> tempMin = new List<Vector3>();
            List<int> tempPoint = new List<int>();

            for (int i = 0; i <= num; i++)
            {
                tempCount.Add(0);
                tempValid.Add(false);
                tempMax.Add(plane.center);
                tempMin.Add(plane.center);
            }

            _planeList.Add(new planeGroup(plane.trackableId, plane.center, plane.center, plane.normal, tempValid, tempCount, 
                tempMax, tempMin, false, tempPoint));
        }

        //Plane optimization
        if (startScan)
        {
            foreach (ARPlane plane in eventArgs.updated)
            {
                Vector3 pos = _camera.WorldToViewportPoint(plane.center);
                for (int b = 0; b < plane.boundary.Count(); b++)
                {
                    Vector3 bound = plane.transform.rotation * new Vector3(plane.boundary[b].x, 0, plane.boundary[b].y) + plane.center;
                    Vector3 bPos = _camera.WorldToViewportPoint(bound);
                    if ((pos.x >= 0 && pos.x <= 1 && pos.y >= 0 && pos.y <= 1 && pos.z > 0) ||
                        (bPos.x >= 0 && bPos.x <= 1 && bPos.y >= 0 && bPos.y <= 1 && bPos.z > 0))
                    {
                        int index = _planeList.FindIndex(x => x.planeID == plane.trackableId);
                        if (index == -1)
                            break;

                        planeGroup tempPlane = _planeList[index];
                        tempPlane.planeCenter = plane.center;
                        tempPlane.planeNormal = plane.normal;
                        tempPlane.center = plane.center;

                        for (int i = 0; i < tempPlane.valid.Count; i++)
                        {

                        }

                        int num = (plane.extents.x > plane.extents.y) ? (int)(plane.extents.x / 0.1f) : (int)(plane.extents.y / 0.1f);
                        num++;
                        if (num > tempPlane.valid.Count)
                        {
                            for (int i = 0; i < num - tempPlane.valid.Count; i++)
                            {
                                tempPlane.count.Add(0);
                                tempPlane.valid.Add(false);
                                tempPlane.maxPos.Add(plane.center);
                                tempPlane.minPos.Add(plane.center);
                            }
                        }

                        float comp = (plane.extents.x > plane.extents.y) ? plane.extents.x : plane.extents.y;

                        if (currPointCount > prevPointCount)
                        {
                            updatePlane = true;
                            tempPlane.pointIndex.Add(prevPointCount);
                            tempPlane.pointIndex.Add(currPointCount);

                            for (int i = prevPointCount; i < currPointCount; i++)
                            {
                                float angle = Vector3.Dot(plane.normal, _vertices[i] - plane.center);
                                float dist = 0.0f;

                                if (plane.alignment == PlaneAlignment.HorizontalDown || plane.alignment == PlaneAlignment.HorizontalUp)
                                {
                                    dist = (Mathf.Abs(plane.center.x - _vertices[i].x) > Mathf.Abs(plane.center.z - _vertices[i].z)) ?
                                        Mathf.Abs(plane.center.x - _vertices[i].x) : Mathf.Abs(plane.center.z - _vertices[i].z);
                                }
                                else if (plane.alignment == PlaneAlignment.Vertical)
                                {
                                    float xzdist = Vector2.Distance(new Vector2(_vertices[i].x, _vertices[i].z),
                                        new Vector2(plane.center.x, plane.center.z));
                                    dist = (Mathf.Abs(plane.center.y - _vertices[i].y) > xzdist) ? Mathf.Abs(plane.center.y - _vertices[i].y) : xzdist;
                                }

                                int distIndex = Mathf.Min((int)(dist / 0.1f), tempPlane.maxPos.Count - 1);

                                if (angle <= Mathf.Cos(Mathf.PI / 2.25f) && angle >= Mathf.Cos(Mathf.PI / 1.85f) &&
                                    dist < comp && plane.infinitePlane.GetDistanceToPoint(_vertices[i]) < 0.05f && distIndex < tempPlane.valid.Count)
                                {
                                    tempPlane.count[distIndex]++;
                                    Vector3 tempMax = tempPlane.maxPos[distIndex];
                                    Vector3 tempMin = tempPlane.minPos[distIndex];

                                    if (tempMax.x < _vertices[i].x)
                                        tempMax.x = _vertices[i].x;
                                    else if (tempMin.x > _vertices[i].x)
                                        tempMin.x = _vertices[i].x;

                                    if (tempMax.y < _vertices[i].y)
                                        tempMax.y = _vertices[i].y;
                                    else if (tempMin.y > _vertices[i].y)
                                        tempMin.y = _vertices[i].y;

                                    if (tempMax.z < _vertices[i].z)
                                        tempMax.z = _vertices[i].z;
                                    else if (tempMin.z > _vertices[i].z)
                                        tempMin.z = _vertices[i].z;

                                    tempPlane.maxPos[distIndex] = tempMax;
                                    tempPlane.minPos[distIndex] = tempMin;

                                    if (!tempPlane.valid[distIndex] && tempPlane.count[distIndex] > 5 + 3 * distIndex)
                                    {
                                        if ((plane.alignment == PlaneAlignment.HorizontalDown || plane.alignment == PlaneAlignment.HorizontalUp) &&
                                            (Vector2.Distance(new Vector2(tempPlane.maxPos[distIndex].x, tempPlane.maxPos[distIndex].z), new Vector2(tempPlane.minPos[distIndex].x, tempPlane.minPos[distIndex].z)) >=
                                            (distIndex + 1) * 0.1f * 2 - 0.05f &&
                                            Vector2.Distance(new Vector2(tempPlane.maxPos[distIndex].x, tempPlane.minPos[distIndex].z), new Vector2(tempPlane.minPos[distIndex].x, tempPlane.minPos[distIndex].z)) >=
                                            (distIndex + 1) * 0.1f - 0.05f &&
                                            Vector2.Distance(new Vector2(tempPlane.minPos[distIndex].x, tempPlane.maxPos[distIndex].z), new Vector2(tempPlane.minPos[distIndex].x, tempPlane.minPos[distIndex].z)) >=
                                            (distIndex + 1) * 0.1f - 0.05f))
                                        {
                                            tempPlane.valid[distIndex] = true;
                                        }
                                        else if (plane.alignment == PlaneAlignment.Vertical &&
                                            Vector3.Distance(tempPlane.maxPos[distIndex], tempPlane.minPos[distIndex]) >=
                                            (distIndex + 1) * 0.1f * 2 - 0.05f &&
                                            (Vector3.Distance(new Vector3(tempPlane.maxPos[distIndex].x, tempPlane.minPos[distIndex].y, tempPlane.maxPos[distIndex].z), tempPlane.minPos[distIndex]) >=
                                            (distIndex + 1) * 0.1f - 0.05f &&
                                            Vector3.Distance(new Vector3(tempPlane.minPos[distIndex].x, tempPlane.maxPos[distIndex].y, tempPlane.minPos[distIndex].z), tempPlane.minPos[distIndex]) >=
                                            (distIndex + 1) * 0.1f - 0.05f))
                                        {
                                            tempPlane.valid[distIndex] = true;
                                        }
                                    }
                                }
                            }

                            int detectNum = 0;
                            //generate plane
                            for (int i = 0; i < tempPlane.valid.Count; i++)
                            {
                                if (tempPlane.valid[i])
                                    detectNum++;
                                if ((tempPlane.valid[i] == false || i == tempPlane.valid.Count - 1) && detectNum >= tempPlane.valid.Count / 2)
                                {
                                    int validNum = tempPlane.valid[i] == false ? i - 1 : i;

                                    if (plane.alignment == PlaneAlignment.HorizontalDown || plane.alignment == PlaneAlignment.HorizontalUp)
                                    {
                                        float normalAngle1 = Vector3.Dot(plane.normal, new Vector3(0, 1, 0));
                                        float normalAngle2 = Vector3.Dot(plane.normal, new Vector3(0, -1, 0));
                                        if (normalAngle1 >= Mathf.Cos(Mathf.PI / 15.0f))
                                        {
                                            if (tempPlane.planeValid != true)
                                            {
                                                tempPlane.planeView = Instantiate(planePrefab, plane.center, new Quaternion(0, 0, 0, 0));
                                            }

                                            if (tempPlane.planeValid == true)
                                            {
                                                tempPlane.planeView.transform.position = plane.center;
                                            }

                                            tempPlane.planeView.transform.localScale = new Vector3(
                                            (tempPlane.maxPos[validNum].x - tempPlane.minPos[validNum].x) * 0.1f,
                                            1,
                                            (tempPlane.maxPos[validNum].z - tempPlane.minPos[validNum].z) * 0.1f);
                                        }
                                        else if (normalAngle2 >= Mathf.Cos(Mathf.PI / 15.0f))
                                        {
                                            if (tempPlane.planeValid != true)
                                            {
                                                tempPlane.planeView = Instantiate(planePrefab, plane.center, new Quaternion(0, 0, 0, 0));
                                                tempPlane.planeView.transform.Rotate(0, 180, 0);
                                            }

                                            if (tempPlane.planeValid == true)
                                            {
                                                tempPlane.planeView.transform.position = plane.center;
                                            }

                                            tempPlane.planeView.transform.localScale = new Vector3(
                                            (tempPlane.maxPos[validNum].x - tempPlane.minPos[validNum].x) * 0.1f,
                                            1,
                                            (tempPlane.maxPos[validNum].z - tempPlane.minPos[validNum].z) * 0.1f);
                                        }
                                    }
                                    else if (plane.alignment == PlaneAlignment.Vertical)
                                    {
                                        float normalAngle1 = Vector3.Dot(plane.normal, new Vector3(0, 0, 1));
                                        float normalAngle2 = Vector3.Dot(plane.normal, new Vector3(0, 0, -1));
                                        float normalAngle3 = Vector3.Dot(plane.normal, new Vector3(1, 0, 0));
                                        float normalAngle4 = Vector3.Dot(plane.normal, new Vector3(-1, 0, 0));

                                        if (normalAngle1 >= Mathf.Cos(Mathf.PI / 15.0f))
                                        {
                                            if (tempPlane.planeValid != true)
                                            {
                                                tempPlane.planeView = Instantiate(planePrefab, plane.center, new Quaternion(0, 0, 0, 0));
                                                tempPlane.planeView.transform.Rotate(90, 0, 0);
                                            }

                                            if (tempPlane.planeValid == true)
                                            {
                                                tempPlane.planeView.transform.position = plane.center;
                                            }

                                            tempPlane.planeView.transform.localScale = new Vector3(
                                                Vector2.Distance(new Vector2(tempPlane.maxPos[validNum].x, tempPlane.maxPos[validNum].z),
                                                new Vector2(tempPlane.minPos[validNum].x, tempPlane.minPos[validNum].z)) * 0.1f,
                                                1,
                                                (tempPlane.maxPos[validNum].y - tempPlane.minPos[validNum].y) * 0.1f);

                                        }
                                        else if (normalAngle2 >= Mathf.Cos(Mathf.PI / 15.0f))
                                        {
                                            if (tempPlane.planeValid != true)
                                            {
                                                tempPlane.planeView = Instantiate(planePrefab, plane.center, new Quaternion(0, 0, 0, 0));
                                                tempPlane.planeView.transform.Rotate(-90, 0, 0);
                                            }

                                            if (tempPlane.planeValid == true)
                                            {
                                                tempPlane.planeView.transform.position = plane.center;
                                            }

                                            tempPlane.planeView.transform.localScale = new Vector3(
                                                Vector2.Distance(new Vector2(tempPlane.maxPos[validNum].x, tempPlane.maxPos[validNum].z),
                                                new Vector2(tempPlane.minPos[validNum].x, tempPlane.minPos[validNum].z)) * 0.1f,
                                                1,
                                                (tempPlane.maxPos[validNum].y - tempPlane.minPos[validNum].y) * 0.1f);
                                        }
                                        else if (normalAngle3 >= Mathf.Cos(Mathf.PI / 15.0f))
                                        {
                                            if (tempPlane.planeValid != true)
                                            {
                                                tempPlane.planeView = Instantiate(planePrefab, plane.center, new Quaternion(0, 0, 0, 0));
                                                tempPlane.planeView.transform.Rotate(-90, 0, -90);
                                            }

                                            if (tempPlane.planeValid == true)
                                            {
                                                tempPlane.planeView.transform.position = plane.center;
                                            }

                                            tempPlane.planeView.transform.localScale = new Vector3(
                                                Vector2.Distance(new Vector2(tempPlane.maxPos[validNum].x, tempPlane.maxPos[validNum].z),
                                                new Vector2(tempPlane.minPos[validNum].x, tempPlane.minPos[validNum].z)) * 0.1f,
                                                1,
                                                (tempPlane.maxPos[validNum].y - tempPlane.minPos[validNum].y) * 0.1f);
                                        }
                                        else if (normalAngle4 >= Mathf.Cos(Mathf.PI / 15.0f))
                                        {
                                            if (tempPlane.planeValid != true)
                                            {
                                                tempPlane.planeView = Instantiate(planePrefab, plane.center, new Quaternion(0, 0, 0, 0));
                                                tempPlane.planeView.transform.Rotate(90, 0, 90);
                                            }

                                            if (tempPlane.planeValid == true)
                                            {
                                                tempPlane.planeView.transform.position = plane.center;
                                            }

                                            tempPlane.planeView.transform.localScale = new Vector3(
                                                Vector2.Distance(new Vector2(tempPlane.maxPos[validNum].x, tempPlane.maxPos[validNum].z),
                                                new Vector2(tempPlane.minPos[validNum].x, tempPlane.minPos[validNum].z)) * 0.1f,
                                                1,
                                                (tempPlane.maxPos[validNum].y - tempPlane.minPos[validNum].y) * 0.1f);
                                        }
                                    }

                                    tempPlane.planeValid = true;
                                    break;
                                }
                                //mahattan assumption
                            }
                        }

                        _planeList[index] = tempPlane;

                        break;
                    }
                }
            }

            if (updatePlane)
            {
                prevPointCount = currPointCount;
                updatePlane = false;
            }

            foreach (ARPlane plane in eventArgs.removed)
            {
                //do something
                int index = _planeList.FindIndex(x => x.planeID == plane.trackableId);
                if (index == -1)
                    continue;

                Destroy(_planeList[index].planeView);
                _planeList.RemoveAt(index);
            }
        }
    }

    void OnCameraFrameReceived(ARCameraFrameEventArgs eventArgs)
    {
        if (_cameraManager.TryAcquireLatestCpuImage(out XRCpuImage cameraImage))
        {
            using (cameraImage)
            {
                if (cameraImage.format == XRCpuImage.Format.AndroidYuv420_888)
                {
                    OnImageAvailable(cameraImage);
                }
                getImageFrame(cameraImage);
            }
        }
    }

    bool shouldAccumulate()
    {
        bool _shouldAccum = false;
        Transform cameraTrans = _cameraManager.transform;
        Vector3 pose = cameraTrans.position;
        Vector3 forward = cameraTrans.forward;

        if (Vector3.Dot(forward, _lastCameraForward) <= Mathf.Cos(Mathf.PI / 15.0f) || Vector3.Distance(pose, _lastCameraPose) >= 0.1)
        {
            _shouldAccum = true;

            _lastCameraPose = _cameraManager.transform.position;
            _lastCameraForward = _cameraManager.transform.forward;
        }

        return _shouldAccum;
    }

    void addOptList()
    {
        pointGroup tempGroup = new pointGroup(_imageTexture, 
            _cameraManager.transform.localToWorldMatrix, 
            DepthSource.DepthArray, 
            DepthSource.ConfidenceArray, 
            _cameraManager.transform.forward, 
            _cameraManager.transform.position, 
            _verticesCount);

        _optList.Add(tempGroup);
    }

    /// <summary>
    /// Converts a new CPU image into byte buffers and caches to be accessed later.
    /// </summary>
    /// <param name="image">The new CPU image to process.</param>
    private void OnImageAvailable(XRCpuImage image)
    {
        if (_cameraBufferY == null || _cameraBufferU == null || _cameraBufferV == null)
        {
            _cameraWidth = image.width;
            _cameraHeight = image.height;
            _rowStrideY = image.GetPlane(0).rowStride;
            _rowStrideUV = image.GetPlane(1).rowStride;
            _pixelStrideUV = image.GetPlane(1).pixelStride;
            _cameraBufferY = new byte[image.GetPlane(0).data.Length];
            _cameraBufferU = new byte[image.GetPlane(1).data.Length];
            _cameraBufferV = new byte[image.GetPlane(2).data.Length];
        }

        image.GetPlane(0).data.CopyTo(_cameraBufferY);
        image.GetPlane(1).data.CopyTo(_cameraBufferU);
        image.GetPlane(2).data.CopyTo(_cameraBufferV);
    }

    //Write Log
    public void writeFile()
    {
        string path = pathForDocumentsFile("pointCloudLog.txt");
        FileStream file = new FileStream(path, FileMode.Create, FileAccess.Write);
    
        StreamWriter sw = new StreamWriter(file);
        
        for (int i = 0; i < _verticesCount; i++)
        {
            if (_vertices[i].y < max_y - 0.1f || _vertices[i].y > min_y + 0.1f)
            {
                sw.Write(_vertices[i].x + "\t" + _vertices[i].y + "\t" + _vertices[i].z + "\n");
            }
        }

        sw.Close();
        file.Close();

        Debug.Log("write");
    }

    public string pathForDocumentsFile(string filename)
    { 
        if (Application.platform == RuntimePlatform.IPhonePlayer)
        {
            string path = Application.dataPath.Substring(0, Application.dataPath.Length - 5);
            path = path.Substring( 0, path.LastIndexOf( '/' ) );
            return Path.Combine(Path.Combine(path, "Documents" ), filename );
        }
        else if(Application.platform == RuntimePlatform.Android)
        {
            string path = Application.persistentDataPath; 
            path = path.Substring(0, path.LastIndexOf( '/' ) ); 
            return Path.Combine(path, filename);
        } 
        else 
        {
            string path = Application.dataPath; 
            path = path.Substring(0, path.LastIndexOf( '/' ) );
            return Path.Combine(path, filename);
        }
    }

/// <summary>
/// Computes 3D vertices from the depth map and updates mesh with the Point primitive type.
/// </summary>
private void UpdateRawPointCloud()
    {
        // Exits when ARCore is not ready.
        if (!_initialized || _cameraBufferY == null)
        {
            return;
        }
        
        // Color and depth images usually have different aspect ratios. The depth image corresponds
        // to the region of the camera image that is center-cropped to the depth aspect ratio.
        float depthAspectRatio = (float)DepthSource.DepthHeight / DepthSource.DepthWidth;
        int colorHeightDepthAspectRatio = (int)(_cameraWidth * depthAspectRatio);
        int colorHeightOffset = (_cameraHeight - colorHeightDepthAspectRatio) / 2;

        short[] depthArray = DepthSource.DepthArray;
        if (depthArray.Length != DepthSource.DepthWidth * DepthSource.DepthHeight)
        {
            // Depth array is not yet available.
            return;
        }

        byte[] confidenceArray = DepthSource.ConfidenceArray;
        bool noConfidenceAvailable = depthArray.Length != confidenceArray.Length;

        float gridArea = DepthSource.DepthWidth * DepthSource.DepthHeight;
        float spacing = Mathf.Sqrt(gridArea / 250.0f);
        int deltaX = (int)(Mathf.Round(DepthSource.DepthWidth / spacing));
        int deltaY = (int)(Mathf.Round(DepthSource.DepthHeight / spacing));
        
        // Creates point clouds from the depth map.
        //for (int y = 0; y < DepthSource.DepthHeight; y++)
        for (int y = 0; y < deltaY; y++)
        {
            float alterOffsetX = (float)(y % 2) * spacing / 2;
            //for (int x = 0; x < DepthSource.DepthWidth; x++)
            for (int x = 0; x < deltaX; x++)
            {
                Vector2 cameraPoint;
                cameraPoint.x = alterOffsetX + (float)(x + 0.5) * spacing;
                cameraPoint.y = (float)(y + 0.5) * spacing;

                int depthIndex = ((int)cameraPoint.y * DepthSource.DepthWidth) + (int)cameraPoint.x;
                float depthInM = depthArray[depthIndex] * DepthSource.MillimeterToMeter;
                float confidence = noConfidenceAvailable ? 1f : confidenceArray[depthIndex] / 255f;

                // Ignore missing depth values to improve runtime performance.
                if (depthInM == 0f || confidence <= 0.25f)
                {
                    continue;
                }

                // Computes world-space coordinates.
                Vector3 vertex = DepthSource.TransformVertexToWorldSpace(
                    DepthSource.ComputeVertex((int)cameraPoint.x, (int)cameraPoint.y, depthInM));
 
                int colorX = (int)cameraPoint.x * _cameraWidth / DepthSource.DepthWidth;
                int colorY = colorHeightOffset +
                    ((int)cameraPoint.y * colorHeightDepthAspectRatio / DepthSource.DepthHeight);
                int linearIndexY = (colorY * _rowStrideY) + colorX;
                int linearIndexUV = ((colorY / 2) * _rowStrideUV) + ((colorX / 2) * _pixelStrideUV);

                // Each channel value is an unsigned byte.
                byte channelValueY = _cameraBufferY[linearIndexY];
                byte channelValueU = _cameraBufferU[linearIndexUV];
                byte channelValueV = _cameraBufferV[linearIndexUV];

                byte[] rgb = ConvertYuvToRgb(channelValueY, channelValueU, channelValueV);
                byte confidenceByte = (byte)(confidence * 255f);
                Color32 color = new Color32(rgb[0], rgb[1], rgb[2], 255);

                Vector3 vecColor = new Vector3(rgb[0], rgb[1], rgb[2]);

                denoiseVertices.Add(vertex);
                denoiseColors.Add(vecColor);

                //Debug.Log("in" + ", " + _verticesCount + ", " + _verticesIndex);
            }
        }
        
        denoiseCount++;

        if (denoiseCount >= 3)
        {
            denoiseCheckSideline();

            for (int i = 0; i < denoiseGridArray.Length; i++)
            {
                if (denoiseGridArray[i].pointCount > 3)
                {
                    if (_verticesCount < _maxVerticesInBuffer - 1)
                    {
                        ++_verticesCount;
                        ++_vertextDrawCount;
                    }

                    // Replaces old vertices in the buffer after reaching the maximum capacity.
                    if (_verticesIndex >= _maxVerticesInBuffer)
                    {
                        _verticesIndex = 0;
                    }

                    _vertices[_verticesIndex] = denoiseGridArray[i].position;
                    //Debug.Log(denoiseGridArray[i].position);
                    Color32 tempColor = new Color32((byte)denoiseGridArray[i].color.x, (byte)denoiseGridArray[i].color.y, (byte)denoiseGridArray[i].color.z, 255);
                    _colors[_verticesIndex] = tempColor;
                    ++_verticesIndex;

                    if (_verticesCount == 0)
                    {
                        return;
                    }
                }
            }

            denoiseVertices = new List<Vector3>();
            denoiseColors = new List<Vector3>();
            denoiseCount = 0;
            currPointCount = _verticesCount;
        }

        if (isPCView)
        {
            // Assigns graphical buffers.
#if UNITY_2019_3_OR_NEWER
            _mesh.SetVertices(_vertices, 0, _verticesCount);
            _mesh.SetIndices(_indices, 0, _verticesCount, MeshTopology.Points, 0);
            _mesh.SetColors(_colors, 0, _verticesCount);
#else
        // Note that we recommend using Unity 2019.3 or above to compile this scene.
        List<Vector3> vertexList = new List<Vector3>();
        List<Color32> colorList = new List<Color32>();
        List<int> indexList = new List<int>();

        for (int i = 0; i < _verticesCount; ++i)
        {
            vertexList.Add(_vertices[i]);
            indexList.Add(_indices[i]);
            colorList.Add(_colors[i]);
        }

        _mesh.SetVertices(vertexList);
        _mesh.SetIndices(indexList.ToArray(), MeshTopology.Points, 0);
        _mesh.SetColors(colorList);
#endif // UNITY_2019_3_OR_NEWER

            MeshFilter meshFilter = GetComponent<MeshFilter>();
            meshFilter.mesh = _mesh;
        }
    }

    /// Converts a YUV color value into RGB. Input YUV values are expected in the range [0, 255].
    /// <param name="y">The pixel value of the Y plane in the range [0, 255].</param>
    /// <param name="u">The pixel value of the U plane in the range [0, 255].</param>
    /// <param name="v">The pixel value of the V plane in the range [0, 255].</param>
    /// <returns>RGB values are in the range [0.0, 1.0].</returns>
    private byte[] ConvertYuvToRgb(byte y, byte u, byte v)
    {
        float yFloat = y / 255.0f; // Range [0.0, 1.0].
        float uFloat = (u * 0.872f / 255.0f) - 0.436f; // Range [-0.436, 0.436].
        float vFloat = (v * 1.230f / 255.0f) - 0.615f; // Range [-0.615, 0.615].
        float rFloat = Mathf.Clamp01(yFloat + (1.13983f * vFloat));
        float gFloat = Mathf.Clamp01(yFloat - (0.39465f * uFloat) - (0.58060f * vFloat));
        float bFloat = Mathf.Clamp01(yFloat + (2.03211f * uFloat));
        byte r = (byte)(rFloat * 255f);
        byte g = (byte)(gFloat * 255f);
        byte b = (byte)(bFloat * 255f);
        return new[] { r, g, b };
    }

    public void checkSideline()
    {
        if (!gridCreated)
        {
            for (int i = 0; i < _verticesCount; i++)
            {
                if (max_x < _vertices[i].x)
                    max_x = _vertices[i].x;
                else if (min_x > _vertices[i].x)
                    min_x = _vertices[i].x;

                if (max_y < _vertices[i].y)
                    max_y = _vertices[i].y;
                else if (min_y > _vertices[i].y)
                    min_y = _vertices[i].y;

                if (max_z < _vertices[i].z)
                    max_z = _vertices[i].z;
                else if (min_z > _vertices[i].z)
                    min_z = _vertices[i].z;
            }

            gridLengthX = max_x - min_x;
            gridLengthY = max_y - min_y;
            gridLengthZ = max_z - min_z;

            gridSizeX = (int)(gridLengthX / 0.1);
            gridSizeY = (int)(gridLengthY / 0.1);
            gridSizeZ = (int)(gridLengthZ / 0.1);

            gridArray = new GridArray[gridSizeX * gridSizeY * gridSizeZ];
            boolGrid = new bool[gridSizeX * gridSizeY * gridSizeZ];

            for (int i = 0; i < _verticesCount; i++)
            {
                int a = (int)((float)(gridSizeX) * (_vertices[i].x - min_x) / gridLengthX);
                int b = (int)((float)(gridSizeY) * (_vertices[i].y - min_y) / gridLengthY);
                int c = (int)((float)(gridSizeZ) * (_vertices[i].z - min_z) / gridLengthZ);

                if (a >= 0 && a < gridSizeX && b >= 0 && b < gridSizeY && c >= 0 && b < gridSizeZ)
                {
                    int idx = convert3DIndex(a, b, c, gridSizeX, gridSizeY, gridSizeZ);
                    gridArray[idx].pointCount += 1;
                    if (gridArray[idx].pointCount >= 5)
                    {
                        gridArray[idx].occ = true;
                        boolGrid[idx] = true;
                    }
                }
            }

            for (int i = 0; i < gridSizeX; i++)
            {
                for (int j = 0; j < gridSizeY; j++)
                {
                    for (int k = 0; k < gridSizeZ; k++)
                    {
                        int idx = convert3DIndex(i, j, k, gridSizeX, gridSizeY, gridSizeZ);
                        float xSize = gridLengthX / (float)gridSizeX * (float)i;
                        float ySize = gridLengthY / (float)gridSizeY * (float)j;
                        float zSize = gridLengthZ / (float)gridSizeZ * (float)k;

                        gridArray[idx].position.x = (float)(min_x + (gridLengthX / (2 * (float)gridSizeX)) + xSize);
                        gridArray[idx].position.y = (float)(min_y + (gridLengthY / (2 * (float)gridSizeY)) + ySize);
                        gridArray[idx].position.z = (float)(min_z + (gridLengthZ / (2 * (float)gridSizeZ)) + zSize);
                    }
                }
            }
            //Reset();
            Debug.Log("x: " + gridLengthX + " y: " + gridLengthY + " z: " + gridLengthZ);

            //set communication
            GameObject.Find("Communicator").GetComponent<Communicator>().initialize(gridSizeX, gridSizeY, gridSizeZ);
            GameObject.Find("Communicator").GetComponent<Communicator>().updateOccupacyGrid(boolGrid);

            //set animation
            GameObject.Find("ArrowAnimation").GetComponent<ArrowAnimation>().Init(gridSizeX, gridSizeY, gridSizeZ, new Vector3(max_x, max_y, max_z),
                new Vector3(min_x, min_y, min_z), new Vector3(gridLengthX, gridLengthY, gridLengthZ));
            GameObject.Find("ParticleAnimation1").GetComponent<ParticleAnimation1>().Init(gridSizeX, gridSizeY, gridSizeZ, new Vector3(max_x, max_y, max_z),
                new Vector3(min_x, min_y, min_z), new Vector3(gridLengthX, gridLengthY, gridLengthZ));
            GameObject.Find("ParticleAnimation2").GetComponent<ParticleAnimation2>().Init(gridSizeX, gridSizeY, gridSizeZ, new Vector3(max_x, max_y, max_z),
                new Vector3(min_x, min_y, min_z), new Vector3(gridLengthX, gridLengthY, gridLengthZ));
            GameObject.Find("HeatmapAnimation").GetComponent<HeatmapAnimation>().Init(gridSizeX, gridSizeY, gridSizeZ, new Vector3(max_x, max_y, max_z),
                new Vector3(min_x, min_y, min_z), new Vector3(gridLengthX, gridLengthY, gridLengthZ));

            gridCreated = true;
        }
    }

    void denoiseCheckSideline()
    {
        float tempMax_x = 0;
        float tempMin_x = 0;

        float tempMax_y = 0;
        float tempMin_y = 0;

        float tempMax_z = 0;
        float tempMin_z = 0;

        for (int i = 0; i < denoiseVertices.Count; i++)
        {
            if (tempMax_x < denoiseVertices[i].x)
                tempMax_x = denoiseVertices[i].x;
            else if (tempMin_x > denoiseVertices[i].x)
                tempMin_x = denoiseVertices[i].x;

            if (tempMax_y < denoiseVertices[i].y)
                tempMax_y = denoiseVertices[i].y;
            else if (tempMin_y > denoiseVertices[i].y)
                tempMin_y = denoiseVertices[i].y;

            if (tempMax_z < denoiseVertices[i].z)
                tempMax_z = denoiseVertices[i].z;
            else if (tempMin_z > denoiseVertices[i].z)
                tempMin_z = denoiseVertices[i].z;
        }

        float tempGridLengthX = tempMax_x - tempMin_x;
        float tempGridLengthY = tempMax_y - tempMin_y;
        float tempGridLengthZ = tempMax_z - tempMin_z;

        int noise_gridSizeX = (int)(tempGridLengthX / 0.05);
        int noise_gridSizeY = (int)(tempGridLengthY / 0.05);
        int noise_gridSizeZ = (int)(tempGridLengthZ / 0.05);

        if (noise_gridSizeX == 0)
            noise_gridSizeX = 1;
        if (noise_gridSizeY == 0)
            noise_gridSizeY = 1;
        if (noise_gridSizeZ == 0)
            noise_gridSizeZ = 1;

        denoiseGridArray = new denoiseGrid[noise_gridSizeX * noise_gridSizeY * noise_gridSizeZ];

        for (int i = 0; i < denoiseVertices.Count; i++)
        {
            int a = (int)((float)(noise_gridSizeX) * (denoiseVertices[i].x - tempMin_x) / tempGridLengthX);
            int b = (int)((float)(noise_gridSizeY) * (denoiseVertices[i].y - tempMin_y) / tempGridLengthY);
            int c = (int)((float)(noise_gridSizeZ) * (denoiseVertices[i].z - tempMin_z) / tempGridLengthZ);

            if (a >= 0 && a < noise_gridSizeX && b >= 0 && b < noise_gridSizeY && c >= 0 && b < noise_gridSizeZ)
            {
                int idx = convert3DIndex(a, b, c, noise_gridSizeX, noise_gridSizeY, noise_gridSizeZ);
                denoiseGridArray[idx].pointCount += 1;
                denoiseGridArray[idx].position += denoiseVertices[i];
                denoiseGridArray[idx].color += denoiseColors[i];
            }
        }

        for (int i = 0; i < denoiseGridArray.Length; i++)
        {
            if (denoiseGridArray[i].position.x != 0 && denoiseGridArray[i].position.y != 0 && denoiseGridArray[i].position.z != 0)
            {
                denoiseGridArray[i].position /= denoiseGridArray[i].pointCount;
                denoiseGridArray[i].color /= denoiseGridArray[i].pointCount;
            }
        }
    }

    public void showGridview()
    {
        if (!editMode)
        {
            boxList = new List<GameObject>();
            for (int i = 0; i < gridArray.Length; i++)
            {
                if (gridArray[i].occ == true)
                {
                    GameObject tempBox = Instantiate(occGridbox, gridArray[i].position, Quaternion.identity);
                    boxList.Add(tempBox);
                }
            }

            editMode = true;
        }
    }

    public void stopGridView()
    {
        if (editMode)
        {
            foreach (GameObject box in boxList)
            {
                Destroy(box);
            }

            boxList.Clear();

            editMode = false;
        }
    }

    public void setPCView()
    {
        _vertextDrawCount = _verticesCount;
#if UNITY_2019_3_OR_NEWER
        //_mesh = new Mesh();
        //_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.SetVertices(_vertices, 0, _vertextDrawCount);
        _mesh.SetIndices(_indices, 0, _vertextDrawCount, MeshTopology.Points, 0);
        _mesh.SetColors(_colors, 0, _vertextDrawCount);
#else
        // Note that we recommend using Unity 2019.3 or above to compile this scene.
        List<Vector3> vertexList = new List<Vector3>();
        List<Color32> colorList = new List<Color32>();
        List<int> indexList = new List<int>();

        for (int i = 0; i < _verticesCount; ++i)
        {
            vertexList.Add(_vertices[i]);
            indexList.Add(_indices[i]);
            colorList.Add(_colors[i]);
        }

        _mesh.SetVertices(vertexList);
        _mesh.SetIndices(indexList.ToArray(), MeshTopology.Points, 0);
        _mesh.SetColors(colorList);
#endif // UNITY_2019_3_OR_NEWER

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = _mesh;

        isPCView = true;
    }

    public void clearPCView()
    {
        _vertextDrawCount = 0;
#if UNITY_2019_3_OR_NEWER
        //_mesh = new Mesh();
        //_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.SetVertices(_vertices, 0, _vertextDrawCount);
        _mesh.SetIndices(_indices, 0, _vertextDrawCount, MeshTopology.Points, 0);
        _mesh.SetColors(_colors, 0, _vertextDrawCount);
#else
        // Note that we recommend using Unity 2019.3 or above to compile this scene.
        List<Vector3> vertexList = new List<Vector3>();
        List<Color32> colorList = new List<Color32>();
        List<int> indexList = new List<int>();

        for (int i = 0; i < _verticesCount; ++i)
        {
            vertexList.Add(_vertices[i]);
            indexList.Add(_indices[i]);
            colorList.Add(_colors[i]);
        }

        _mesh.SetVertices(vertexList);
        _mesh.SetIndices(indexList.ToArray(), MeshTopology.Points, 0);
        _mesh.SetColors(colorList);
#endif // UNITY_2019_3_OR_NEWER

        MeshFilter meshFilter = GetComponent<MeshFilter>();
        meshFilter.mesh = _mesh;

        isPCView = false;
    }
    
    private int convert3DIndex(int x, int y, int z, int n_gridSizeX, int n_gridSizeY, int n_gridSizeZ)
    {
        int idx = x * n_gridSizeY * n_gridSizeZ + y * n_gridSizeZ + z;

        return idx;
    }

    //get functions
    public Vector3Int getGridIndex(float i, float j, float k)
    {
        int ind_x = (int)(gridSizeX * (i - min_x) / gridLengthX);
        int ind_y = (int)(gridSizeY * (j - min_y) / gridLengthY);
        int ind_z = (int)(gridSizeZ * (k - min_z) / gridLengthZ);

        if (ind_x >= gridSizeX)
            ind_x = gridSizeX - 1;
        else if (ind_x < 0)
            ind_x = 0;

        if (ind_y >= gridSizeY)
            ind_y = gridSizeY - 1;
        else if (ind_y < 0)
            ind_y = 0;

        if (ind_z >= gridSizeZ)
            ind_z = gridSizeZ - 1;
        else if (ind_z < 0)
            ind_z = 0;

        return new Vector3Int(ind_x, ind_y, ind_z);
    }

    public Vector3 getGridPosition(int a, int b, int c)
    {
        int indicies = convert3DIndex(a, b, c, gridSizeX, gridSizeY, gridSizeZ);
        Vector3 position = gridArray[indicies].position;

        return position;
    }

    public bool[] getBoolGrid()
    {
        return boolGrid;
    }

    public Vector3 getGridLength()
    {
        return new Vector3(gridLengthX, gridLengthY, gridLengthZ);
    }

    public Vector3Int getGridSize()
    {
        return new Vector3Int(gridSizeX, gridSizeY, gridSizeZ);
    }

    public Vector3 getGridMax()
    {
        return new Vector3(max_x, max_y, max_z);
    }

    public Vector3 getGridMin()
    {
        return new Vector3(min_x, min_y, min_z);
    }

    public bool getCreated()
    {
        return gridCreated;
    }

    public void toggleScan()
    {
        if (startScan)
        {
            startScan = false;
            GameObject.Find("StartScan").GetComponentInChildren<Text>().text = "Resume Scan";
        }
        else
        {
            startScan = true;
            GameObject.Find("StartScan").GetComponentInChildren<Text>().text = "Pause Scan";
        }
    }

    //Image match Opt functions
    unsafe void getImageFrame(XRCpuImage image)
    {
        var conversionParams = new XRCpuImage.ConversionParams
        {
            // Get the entire image.
            inputRect = new RectInt(0, 0, image.width, image.height),

            // Downsample by 2.
            //outputDimensions = new Vector2Int(image.width / 2, image.height / 2),
            outputDimensions = new Vector2Int(image.width, image.height),

            // Choose RGBA format.
            outputFormat = TextureFormat.RGBA32,

            // Flip across the vertical axis (mirror image).
            transformation = XRCpuImage.Transformation.MirrorX
        };

        // See how many bytes you need to store the final image.
        int size = image.GetConvertedDataSize(conversionParams);

        // Allocate a buffer to store the image.
        var buffer = new NativeArray<byte>(size, Allocator.Temp);

        // Extract the image data
        image.Convert(conversionParams, new IntPtr(buffer.GetUnsafePtr()), buffer.Length);

        // The image was converted to RGBA32 format and written into the provided buffer
        // so you can dispose of the XRCpuImage. You must do this or it will leak resources.
        image.Dispose();

        // At this point, you can process the image, pass it to a computer vision algorithm, etc.
        // In this example, you apply it to a texture to visualize it.

        // You've got the data; let's put it into a texture so you can visualize it.
        if (!_imageTexture)
        {
            _imageTexture = new Texture2D(
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y,
            conversionParams.outputFormat,
            false);

            _prevTexture = new Texture2D(
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y,
            conversionParams.outputFormat,
            false);

            _currTexture = new Texture2D(
            conversionParams.outputDimensions.x,
            conversionParams.outputDimensions.y,
            conversionParams.outputFormat,
            false);

            firstImage = true;
        }

        _imageTexture.LoadRawTextureData(buffer);
        _imageTexture.Apply();

        // Done with your temporary data, so you can dispose it.
        buffer.Dispose();
    }

    Mat calcEssen(Texture2D prev, Texture2D curr)
    {
        if (firstImage)
        {
            if (prev)
            {
                // Load your target image
                Mat targetImage = new Mat(prev.height, prev.width, CvType.CV_8UC3);
                targetImageGray = new Mat(prev.height, prev.width, CvType.CV_8UC3);
                Utils.texture2DToMat(prev, targetImage);
                Imgproc.cvtColor(targetImage, targetImageGray, Imgproc.COLOR_RGB2GRAY);

                // Init ORB feature
                targetKeyPoints = new MatOfKeyPoint();
                targetDescriptors = new Mat();
                feature = ORB.create();
                feature.detectAndCompute(targetImageGray, new Mat(), targetKeyPoints, targetDescriptors);

                // Init brute-force matcher
                matcher = new BFMatcher(Core.NORM_HAMMING);
            }

            if (curr && prev)
            {
                Mat in_frame = new Mat(curr.height, curr.width, CvType.CV_8UC3);
                Utils.texture2DToMat(curr, in_frame);
                Imgproc.cvtColor(in_frame, in_frame, Imgproc.COLOR_RGB2GRAY);

                // Extract features
                keyPoints = new MatOfKeyPoint();
                descriptors = new Mat();
                feature.detectAndCompute(in_frame, new Mat(), keyPoints, descriptors);

                // Match features
                MatOfDMatch matches = new MatOfDMatch();
                matcher.match(targetDescriptors, descriptors, matches, new Mat());

                DMatch[] tempMatch = matches.toArray();

                float[] matchArray = new float[tempMatch.Length];
                float mean = 0;

                for (int i = 0; i < tempMatch.Length; i++)
                {
                    matchArray[i] = tempMatch[i].distance;
                }
                Array.Sort(matchArray);
                for (int i = 0; i < matchArray.Length / 10; i++)
                {
                    mean += matchArray[i];
                }
                mean /= matchArray.Length / 10;

                //Debug.Log("distance of matches: " + mean);

                Dictionary<int, float> matchDic = new Dictionary<int, float>();

                for (int i = 0; i < tempMatch.Length; i++)
                {
                    matchDic.Add(i, tempMatch[i].distance);
                }
                var newMatchDic = matchDic.OrderBy(x => x.Value);

                //train
                KeyPoint[] tempKey = keyPoints.toArray();
                List<Point> listPoint = new List<Point>();

                //query
                KeyPoint[] tempKeyTarget = targetKeyPoints.toArray();
                List<Point> listPointTarget = new List<Point>();
                int num = 0;
                foreach (var dictionary in newMatchDic)
                {
                    listPoint.Add(tempKey[tempMatch[dictionary.Key].trainIdx].pt);
                    listPointTarget.Add(tempKeyTarget[tempMatch[dictionary.Key].queryIdx].pt);

                    //use these infos
                    //dist = tempMatch[dictionary.Key].distance; use <= 30 ?
                    //px = tempKey[tempMatch[dictionary.Key].trainIdx].pt.x; //curr
                    //py = tempKey[tempMatch[dictionary.Key].trainIdx].pt.y;
                    //px2 = tempKeyTarget[tempMatch[dictionary.Key].queryIdx].pt.x; //prev
                    //py2 = tempKeyTarget[tempMatch[dictionary.Key].queryIdx].pt.y;

                    num++;
                    if (dictionary.Value >= 30 || num >= 20)
                    {
                        break;
                    }
                }

                Point[] tempPoint = listPoint.ToArray();
                MatOfPoint2f temp2f = new MatOfPoint2f(tempPoint);

                Point[] tempPointTarget = listPointTarget.ToArray();
                MatOfPoint2f temp2fTarget = new MatOfPoint2f(tempPointTarget);

                XRCameraIntrinsics intrinsics;
                _cameraManager.TryGetIntrinsics(out intrinsics);
                Point prin = new Point();
                prin.x = intrinsics.principalPoint.x;
                prin.y = intrinsics.principalPoint.y;

                Mat essen = new Mat();
                essen = Calib3d.findEssentialMat(temp2fTarget, temp2f, intrinsics.focalLength.x, prin, Calib3d.RANSAC);

                Mat R = new Mat();
                Mat T = new Mat();
                Calib3d.recoverPose(essen, temp2fTarget, temp2f, R, T, intrinsics.focalLength.x, prin);
                
               // return R;
                Mat prevDirNew = Mat.zeros(3, 3, CvType.CV_64F);
                prevDirNew.put(0, 2, prevDir.x);
                prevDirNew.put(1, 2, prevDir.y);
                prevDirNew.put(2, 2, prevDir.z);

                Mat newDir = R.t() * prevDirNew;
                Vector3 newDirVec = new Vector3((float)newDir.get(0, 2)[0], (float)newDir.get(1, 2)[0], (float)newDir.get(2, 2)[0]);

                float newY = prevDir.y + (prevDir.y - newDirVec.y);
                Vector3 newYvec = new Vector3(newDirVec.x, newY, newDirVec.z);
                Debug.Log("prevDir: " + prevDir.x + ", " + prevDir.y + ", " + prevDir.z);

                if (currDir.z >= 0)
                    Debug.Log("newDirVec: " + newDirVec.x + ", " + newY + ", " + newDirVec.z);
                else
                    Debug.Log("newDirVec: " + newDirVec.x + ", " + newDirVec.y + ", " + newDirVec.z);

                Debug.Log("currDir: " + currDir.x + ", " + currDir.y + ", " + currDir.z);

                if (currDir.z >= 0)
                    Debug.Log("DirDist: " + Vector3.Distance(newYvec, currDir));
                else
                    Debug.Log("DirDist: " + Vector3.Distance(newDirVec, currDir));

                return Mat.eye(3, 3, CvType.CV_64F);
            }
        }

        return Mat.eye(3, 3, CvType.CV_64F);
    }

    float calcMatch(Texture2D prev, Texture2D curr)
    {
        if (firstImage)
        {
            if (prev)
            {
                // Load your target image
                Mat targetImage = new Mat(prev.height, prev.width, CvType.CV_8UC3);
                targetImageGray = new Mat(prev.height, prev.width, CvType.CV_8UC3);
                Utils.texture2DToMat(prev, targetImage);
                Imgproc.cvtColor(targetImage, targetImageGray, Imgproc.COLOR_RGB2GRAY);

                // Init ORB feature
                targetKeyPoints = new MatOfKeyPoint();
                targetDescriptors = new Mat();
                feature = ORB.create();
                feature.detectAndCompute(targetImageGray, new Mat(), targetKeyPoints, targetDescriptors);

                // Init brute-force matcher
                matcher = new BFMatcher(Core.NORM_HAMMING);
            }

            if (curr && prev)
            {
                Mat in_frame = new Mat(curr.height, curr.width, CvType.CV_8UC3);
                Utils.texture2DToMat(curr, in_frame);
                Imgproc.cvtColor(in_frame, in_frame, Imgproc.COLOR_RGB2GRAY);

                // Extract features
                keyPoints = new MatOfKeyPoint();
                descriptors = new Mat();
                feature.detectAndCompute(in_frame, new Mat(), keyPoints, descriptors);

                // Match features
                MatOfDMatch matches = new MatOfDMatch();
                matcher.match(targetDescriptors, descriptors, matches, new Mat());

                DMatch[] tempMatch = matches.toArray();

                float[] matchArray = new float[tempMatch.Length];
                float mean = 0;

                for (int i = 0; i < tempMatch.Length; i++)
                {
                    matchArray[i] = tempMatch[i].distance;
                }
                Array.Sort(matchArray);
                for (int i = 0; i < matchArray.Length / 10; i++)
                {
                    mean += matchArray[i];
                }
                mean /= matchArray.Length / 10;
                return mean;
                //if (mean <= 0.3)
                //    return true;
                //else
                //    return false;
            }
        }

        return 0;
    }

    void graphOptimization()
    {
        int listNum = _optList.Count;

        for(int i = listNum-1; i > 0; i--)
        {
            Vector2 cameraPoint = new Vector2();

            int depthIndex = ((int)cameraPoint.y * DepthSource.DepthWidth) + (int)cameraPoint.x;
            float depthInM = 0; // = depthArray[depthIndex] * DepthSource.MillimeterToMeter;

            // Computes world-space coordinates.
            Vector3 vertex = DepthSource.TransformVertexToWorldSpace(
                DepthSource.ComputeVertex((int)cameraPoint.x, (int)cameraPoint.y, depthInM));
        }
    }
}