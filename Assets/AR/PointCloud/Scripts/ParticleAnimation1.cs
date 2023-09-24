using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine.UI;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ParticleAnimation1 : MonoBehaviour
{
    public ARCameraManager _cameraManager;
    public Simulation sim;

    //public GameObject particleObj;
    //public GameObject particleObj2D;
    public float particleSpeed = 2.0f;
    public float createPeriod = 1.0f;
    //public Material mat, mat2d;
    //public bool render2d = true;

    //float timer = 0;

    bool animationStarted = false;
    //Vector3[] ventLocation;
    int ACType = 0;
    Vector3Int currentACIndex;

    private const int _maxVerticesInBuffer = 1000000;
    private Vector3[] _vertices = new Vector3[_maxVerticesInBuffer];
    private int _verticesCount = 0;
    private int _verticesIndex = 0;
    private int[] _indices = new int[_maxVerticesInBuffer];
    private Color32[] _colors = new Color32[_maxVerticesInBuffer];
    Mesh _mesh;

    int gridSizeX;
    int gridSizeY;
    int gridSizeZ;

    int timer = 0; //timer

    public void Init(int gridSizeX, int gridSizeY, int gridSizeZ, Vector3 max, Vector3 min, Vector3 length)
    {
        sim = new Simulation();
        sim.Init(new Vector3Int(gridSizeX, gridSizeY, gridSizeZ), max, min, length, 0);

        this.gridSizeX = gridSizeX;
        this.gridSizeY = gridSizeY;
        this.gridSizeZ = gridSizeZ;
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

    public void ResetMesh()
    {
        _verticesCount = 0;
        _verticesIndex = 0;
        ShowMesh();
    }

    public void setStartParticle()
    {
        animationStarted = true;
        Debug.Log("particle1 set");
    }

    public void StopParticleAnimation()
    {
        if (animationStarted)
        {
            animationStarted = false;
            ResetMesh();
            timer = 50;
        }
    }
    void CreateParticles(int i, int j, int k)
    {

        for (int t = 0; t < 27; t++)
        {
            float maxRandomOffset = 0.4f;
            Vector3 createPos = new Vector3(i, j, k) + new Vector3(Random.Range(-maxRandomOffset, maxRandomOffset),
                Random.Range(-maxRandomOffset, maxRandomOffset),
                Random.Range(-maxRandomOffset, maxRandomOffset));
            sim.no_stick(ref createPos, 0.001f);

            if (_verticesCount < _maxVerticesInBuffer - 1)
            {
                _vertices[_verticesCount] = sim.GridToWorldPos(createPos);
                _indices[_verticesCount] = _verticesCount;
                float temp = 0;
                sim.getVelocityAndTemp(createPos, ref temp);
                _colors[_verticesCount] = sim.tempToColor(temp);
                new Color32(255, 255, 255, 255);
                ++_verticesCount;
            }

        }

    }

    void StartAnimation()
    {
        int ind_x = currentACIndex.x;
        int ind_y = currentACIndex.y;
        int ind_z = currentACIndex.z;

        for (int i = -2; i <= 2; i++)
        {
            if (ind_x + i > -1 && ind_x + i < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z - 4 > -1 && ind_z - 4 < gridSizeZ)
            {
                CreateParticles(ind_x + i, ind_y, ind_z - 4);
            }
        }
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x + i > -1 && ind_x + i < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z + 4 > -1 && ind_z + 4 < gridSizeZ)
                CreateParticles(ind_x + i, ind_y, ind_z + 4);

        }
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x - 4 > -1 && ind_x - 4 < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z + i > -1 && ind_z + i < gridSizeZ)
                CreateParticles(ind_x - 4, ind_y, ind_z + i);

        }
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x + 4 > -1 && ind_x + 4 < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z + i > -1 && ind_z + i < gridSizeZ)
                CreateParticles(ind_x + 4, ind_y, ind_z + i);

        }
        Debug.Log("particle1 created");
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
            timer += 1;
            if (timer >= 50)
            {
                StartAnimation();
                timer = 0;
            }

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
                    particlePos + dir * 0.35f;
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

                if (Vector3.Distance(_cameraManager.transform.position, _vertices[i]) < 0.35)
                {
                    color.a = 0.0f;
                }

                _colors[i] = color;
            }
            ShowMesh();
        }

    }

    public void updateACIndex(int[] idx)
    {
        currentACIndex.x = idx[0];
        currentACIndex.y = idx[1];
        currentACIndex.z = idx[2];
    }
}
