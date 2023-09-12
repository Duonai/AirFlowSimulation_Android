using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ParticleAnimation2 : MonoBehaviour
{
    public ARCameraManager _cameraManager;
    public Simulation sim;

    public float particleSpeed = 2.0f;

    bool animationStarted = false;

    //float particleSize = 4.0f;

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

        sim.occupancy = GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getBoolGrid();
    }

    // Start is called before the first frame update
    void Start()
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        ResetMesh();
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
            UpdateParticles();
        }
    }
    public void setStartParticle()
    {
        animationStarted = true;
        Debug.Log("particle2 set");
        
        StartAnimation();
    }

    public void StopParticleAnimation()
    {
        if (animationStarted)
        {
            animationStarted = false;
            ResetMesh();
        }
    }

    void CreateParticles(int i, int j, int k)
    {
        for (int ii = 0; ii < 2; ii++)
        {
            float maxRandomOffset = 0.4f;
            Vector3 createPos = new Vector3(i, j, k) + new Vector3(Random.Range(-maxRandomOffset, maxRandomOffset),
                Random.Range(-maxRandomOffset, maxRandomOffset),
                Random.Range(-maxRandomOffset, maxRandomOffset));

            //Vector3 createPos = new Vector3(i, j, k);
            //if (ii == 0)
            //    createPos -= new Vector3(0.2f, 0.2f, 0.2f);
            //else createPos += new Vector3(0.2f, 0.2f, 0.2f);

            sim.no_stick(ref createPos, 0.001f);

            if (_verticesCount < _maxVerticesInBuffer - 1)
            {
                _vertices[_verticesCount] = sim.GridToWorldPos(createPos);
                _indices[_verticesCount] = _verticesCount;
                //float temp = 0;
                //sim.getVelocityAndTemp(createPos, ref temp);
                //_colors[_verticesCount] = sim.tempToColor(temp);
                _colors[_verticesCount] = new Color32(255, 255, 255, 0);
                ++_verticesCount;
            }
        }

    }

    void StartAnimation()
    {
        for (int i = 0; i < gridSizeX; i++)
        {
            for (int j = 0; j < gridSizeY; j++)
            {
                for (int k = 0; k < gridSizeZ; k++)
                {
                    if (!sim.occupancy[sim.IDX(i, j, k)])
                    {
                        CreateParticles(i, j, k);
                    }
                }
            }
        }
        Debug.Log("particle2 created");
    }
    
    void ShowMesh()
    {
#if UNITY_2019_3_OR_NEWER
        //_mesh = new Mesh();
        //_mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
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

    void UpdateParticles()
    {
        if (animationStarted)
        {
            for (int i = 0; i < _verticesCount; i++)
            {
                Vector3 particlePos = sim.WorldToGridPos(_vertices[i]);
                float temp = 0.0f;
                Vector3 dir = sim.getVelocityAndTemp(particlePos, ref temp);
                //float vel = dir.magnitude;

                float largeStep = 0.1f * 30;
                float smallStep = 0.04f * 30;
                bool rotFlag = false;

                Vector3 largePos = //particlePos + dir * particleSpeed * Time.deltaTime;
                    particlePos + dir * 0.25f;
                sim.no_stick(ref largePos, 0.001f);
                _vertices[i] = sim.GridToWorldPos(largePos);


                Color color = sim.tempToColor(temp);
                if (color.g == 0.0f)
                {
                    //if not complete_occlude;
                    color.a = 1.0f;
                }
                else
                {
                    color.a = 1.0f;
                }

                float L = 1.5f;
                Vector3 At = _cameraManager.transform.forward;
                Vector3 v0 = At * L;
                Vector3 v1 = At * (L + 1.2f);
                Vector3 p0 = _cameraManager.transform.position + v0;
                Vector3 p1 = _cameraManager.transform.position + v1;
                Vector3 pv0 = p0 - _vertices[i];
                Vector3 pv1 = p1 - _vertices[i];

                //debugPrinter.Log("At: " + At, "", LogType.Log);

                if (Mathf.Acos(Vector3.Dot(v0, pv0) / (v0.magnitude * pv0.magnitude)) < Mathf.PI / 2 || 
                    Mathf.Acos(Vector3.Dot(v1, pv1) / (v1.magnitude * pv1.magnitude)) > Mathf.PI / 2)
                {
                    //Debug.Log("alpha");
                    color.a = 0.0f;
                }

                _colors[i] = color;
            }
        }

        ShowMesh();
    }

    public void ResetMesh()
    {
        _verticesCount = 0;
        _verticesIndex = 0;
        ShowMesh();
    }
}
