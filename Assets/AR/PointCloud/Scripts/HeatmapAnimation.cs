using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;
public class HeatmapAnimation: MonoBehaviour
{
    public ARCameraManager _cameraManager;
    public Simulation sim;

    float timer = 0;

    bool animationStarted = false;
    bool planeCreated = false;

    public int volume_spacing = 2;
    public int plane_num = 5;
    public float nearNum = 0.75f;
    float u_spacing, v_spacing, at_spacing;

    int ACType = 0;
    Vector3Int currentACIndex;
    int gridSizeX;
    int gridSizeY;
    int gridSizeZ;

    private const int _maxVerticesInBuffer = 1000000;
    private Vector3[] _vertices = new Vector3[_maxVerticesInBuffer];
    private int _verticesCount = 0;
    private int _verticesIndex = 0;
    private int[] _indices = new int[_maxVerticesInBuffer];
    private Color32[] _colors = new Color32[_maxVerticesInBuffer];
    Mesh _mesh;

    public void Init(int gridSizeX, int gridSizeY, int gridSizeZ, Vector3 max, Vector3 min, Vector3 length)
    {
        sim = new Simulation();
        sim.Init(new Vector3Int(gridSizeX, gridSizeY, gridSizeZ), max, min, length, 0);

        this.gridSizeX = gridSizeX;
        this.gridSizeY = gridSizeY;
        this.gridSizeZ = gridSizeZ;

        //this.camera = camera;
    }

    // Start is called before the first frame update
    void Start()
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        ResetMesh();

        u_spacing = nearNum * 0.8f;
        v_spacing = nearNum * 0.6f;
        at_spacing = 0.2f;

        sim = new Simulation();
        //sim.Init(new Vector3Int(60, 27, 60), 0);

        //StartAnimation();
    }
    public void setStartPlane()
    {
        animationStarted = true;
        Debug.Log("planes set");

        StartAnimation();
    }

    public void StopPlaneAnimation()
    {
        if (animationStarted)
        {
            animationStarted = false;
            planeCreated = false;
            ResetMesh();
            ShowMesh();
            //timer = 50;
        }
    }

    // Update is called once per frame
    void Update()
    {
        if (animationStarted)
        {
            if (GameObject.Find("Communicator").GetComponent<Communicator>().getConnectDone() && GameObject.Find("Communicator").GetComponent<Communicator>().pauseFlag)
            {
                sim.velocity = GameObject.Find("Communicator").GetComponent<Communicator>().getVelocity().Clone() as float[];
                sim.temperature = GameObject.Find("Communicator").GetComponent<Communicator>().getTemperature().Clone() as float[];
            }
            UpdatePlanes();
        }
    }

    void ResetMesh()
    {
        _verticesCount = 0;
        _verticesIndex = 0;
        ShowMesh();
    }

    void StartAnimation()
    {
        _verticesCount = plane_num * volume_spacing * volume_spacing;
        _verticesIndex = 0;

        for (int k = 0; k < plane_num; k++)
        {
            for (int i = 1; i < volume_spacing; i++)
            {
                for (int j = 1; j < volume_spacing; j++)
                {
                    _indices[_verticesIndex++] = PlanePointIdx(k, i - 1, j - 1);
                    _indices[_verticesIndex++] = PlanePointIdx(k, i - 1, j);
                    _indices[_verticesIndex++] = PlanePointIdx(k, i, j);
                    _indices[_verticesIndex++] = PlanePointIdx(k, i, j - 1);
                }
            }
        }
        planeCreated = true;
        Debug.Log("planes created, "+_verticesCount + ":"+_verticesIndex);
    }

    int PlanePointIdx(int plane_idx,int u_idx, int v_idx)
    {
        return (plane_idx * volume_spacing * volume_spacing + u_idx * volume_spacing + v_idx);
    }

    void CreatePlanes()
    {

    }

    void UpdatePlanes()
    {
        //Matrix4x4 viewMatrix = camera.worldToCameraMatrix;
        Vector3 uView = _cameraManager.transform.right;
        Vector3 vView = _cameraManager.transform.up;
        Vector3 at_space = _cameraManager.transform.forward * at_spacing;

        //Vector3 u = uView * u_spacing * 2 / (volume_spacing - 1);
        //Vector3 v = vView * v_spacing * 2 / (volume_spacing - 1);
        //Vector3 n = viewMatrix.GetColumn(2);
        //Vector3 pivot = _cameraManager.transform.position + _cameraManager.transform.forward * nearNum - (u + v) * (volume_spacing - 1) / 2.0f;
        
        //debugPrinter.Log("Pivot: " + pivot, "", LogType.Log);

        for (int k = 0; k < plane_num; k++)
        {
            u_spacing = (1 + at_spacing / nearNum * k) * nearNum * 0.8f;
            v_spacing = (1 + at_spacing / nearNum * k) * nearNum * 0.6f;
            Vector3 u = uView * u_spacing * 2 / (volume_spacing - 1);
            Vector3 v = vView * v_spacing * 2 / (volume_spacing - 1);
            Vector3 pivot = _cameraManager.transform.position + _cameraManager.transform.forward * nearNum - (u + v) * (volume_spacing - 1) / 2.0f;

            for (int i = 0; i < volume_spacing; i++)
            {
                for (int j = 0; j < volume_spacing; j++)
                {
                    int idx = PlanePointIdx(k, i, j);
                    Vector3 worldPos = pivot + u * i + v * j + at_space * k;
                    float temp = 0;

                    Vector3 gridPos = sim.WorldToGridPos(worldPos);
                    sim.getVelocityAndTemp(gridPos, ref temp);
                    _vertices[idx] = worldPos;
                    _colors[idx] = sim.tempToColor(temp);

                    //if (_vertices[idx].x >= sim.gd.maxVec.x || _vertices[idx].x <= sim.gd.minVec.x || _vertices[idx].y >= sim.gd.maxVec.y || _vertices[idx].y <= sim.gd.minVec.y || 
                    //    _vertices[idx].z >= sim.gd.maxVec.z || _vertices[idx].z <= sim.gd.minVec.z)
                    if (gridPos.x >= sim.gd.gridSizeX || gridPos.x <= 0 || gridPos.y >= sim.gd.gridSizeY || gridPos.y <= 0 ||
                        gridPos.z >= sim.gd.gridSizeZ || gridPos.z <= 0)
                    {
                        _colors[idx] = new Color32(0, 0, 0, 0);
                    }
                    //_colors[idx] = new Color32((byte)255, 0, 0, 255);
                    //new Color32((byte)(255 * i / volume_spacing), (byte)(255 * j / volume_spacing), 0, (byte)(255 * k / plane_num));
                    //new Color32(255, 255, 0, (byte)(50 * k));
                }
            }
        }
        _mesh.RecalculateBounds();
        ShowMesh();
    }

    void ShowMesh()
    {
#if UNITY_2019_3_OR_NEWER
        //_mesh = new Mesh();
        //_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        _mesh.SetVertices(_vertices, 0, _verticesCount);
        _mesh.SetIndices(_indices, 0, _verticesIndex, MeshTopology.Quads, 0);
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
