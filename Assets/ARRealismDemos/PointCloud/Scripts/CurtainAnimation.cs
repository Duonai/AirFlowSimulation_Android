using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CurtainAnimation : MonoBehaviour
{

    public Simulation sim;

    public float particleSpeed = 0.3f;


    public float minWidth = 0.04f;
    public float maxWidth = 0.4f;
    public float alpha = 2.0f;
    public float beta = 1.3f;
    public float delta = 0.8f;
    public float zeta = 0.8f;
    public float gamma = 0.43f;

    float timer = 0;

    bool animationStarted = false;
    bool surfaceCreated = false;


    private const int _maxVerticesInBuffer = 1000000;
    private Vector3[] _vertices = new Vector3[_maxVerticesInBuffer];
    private int _verticesCount = 0;
    private int _verticesIndex = 0;
    private int[] _indices = new int[_maxVerticesInBuffer];
    private Color32[] _colors = new Color32[_maxVerticesInBuffer];
    Mesh _mesh;

    int maxNodeCnt = 70;
    int stLen = 360;
    struct surface
    {
        public Vector3[,] gridPos;
        public int[,] nextIdx;
        public float[] areaZero;
        public int[,] pred;
        public int[,] succ;
        public bool[,] valid;
        public float[,] opacity;
    }

    static int surfaceCnt = 4;
    surface[] surfaces = new surface[surfaceCnt];

    int ACType = 0;
    Vector3Int currentACIndex;
    int gridSizeX;
    int gridSizeY;
    int gridSizeZ;


    public void Init(int gridSizeX, int gridSizeY, int gridSizeZ, Vector3 max, Vector3 min, Vector3 length)
    {
        sim = new Simulation();
        sim.Init(new Vector3Int(gridSizeX, gridSizeY, gridSizeZ), max, min, length, 0);

        this.gridSizeX = gridSizeX;
        this.gridSizeY = gridSizeY;
        this.gridSizeZ = gridSizeZ;


        timer = 0;



        for(int i=0;i< surfaceCnt; i++)
        {
            surfaces[i].gridPos = new Vector3[maxNodeCnt, stLen];
            surfaces[i].nextIdx = new int[maxNodeCnt, stLen];
            surfaces[i].areaZero = new float[maxNodeCnt];
            surfaces[i].pred = new int[maxNodeCnt, stLen];
            surfaces[i].succ = new int[maxNodeCnt, stLen];
            surfaces[i].valid = new bool[maxNodeCnt, stLen];
            surfaces[i].opacity = new float[maxNodeCnt, stLen];
        }
    }

    public void updateACIndex(int[] idx)
    {
        currentACIndex.x = idx[0];
        currentACIndex.y = idx[1];
        currentACIndex.z = idx[2];
    }

    // Start is called before the first frame update
    void Start()
    {
        _mesh = new Mesh();
        _mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
        ResetMesh();

        sim = new Simulation();
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
                //timer += Time.deltaTime;
                //if (timer >= createPeriod)
                //{
                //    CreateParticles();
                //    timer -= createPeriod;
                //}
            }
            timer += Time.deltaTime;
            UpdateSurface();
        }
    }
    void ResetMesh()
    {
        _verticesCount = 0;
        _verticesIndex = 0;
    }



    void StartAnimation()
    {
        int interval = 4;

        for(int i = 0; i < 17; i++)
        {
            int ii = interval * i;
            surfaces[0].gridPos[ii, 0] = new Vector3(currentACIndex.x - 5 - 0.2f * Mathf.Sin((i + 2 * timer) * Mathf.PI / 4.0f), currentACIndex.y - 1, currentACIndex.z + i / 4.0f - 2);
            surfaces[1].gridPos[ii, 0] = new Vector3(currentACIndex.x + 5 + 0.2f * Mathf.Sin((i - 2 * timer) * Mathf.PI / 4.0f), currentACIndex.y - 1, currentACIndex.z + i / 4.0f - 2);
            surfaces[2].gridPos[ii, 0] = new Vector3(currentACIndex.x + i / 4.0f - 2, currentACIndex.y - 1, currentACIndex.z - 5 - 0.2f * Mathf.Sin((i - 2 * timer) * Mathf.PI / 4.0f));
            surfaces[3].gridPos[ii, 0] = new Vector3(currentACIndex.x + i / 4.0f - 2, currentACIndex.y - 1, currentACIndex.z + 5 + 0.2f * Mathf.Sin((i + 2 * timer) * Mathf.PI / 4.0f));

            for(int s = 0; s < 4; s++) {
                surfaces[s].gridPos[ii, 0] += new Vector3(0.5f, 0, 0.5f);
                surfaces[s].nextIdx[ii, 0] = i == 16 ? -1 : ii + interval;
            }
        }

        for(int s = 0; s < 4; s++)
        {
            for(int i = 0; i < 16; i++)
            {
                int ii = interval * i;
                surfaces[s].areaZero[ii] = (surfaces[s].gridPos[ii, 0] - surfaces[s].gridPos[ii + interval, 0]).magnitude;
                surfaces[s].valid[ii, 0] = true;
                surfaces[s].opacity[ii, 0] = 1.0f;
            }
            surfaces[s].opacity[interval * 16, 0] = 1.0f;
        }
        surfaceCreated = true;
    }

    int PlanePointIdx(int s, int i, int j)
    {
        return 0;
    }

    void CreateSurface()
    {

    }

    void UpdateSurface()
    {
        if (!surfaceCreated)
        {
            StartAnimation();
            surfaceCreated = true;
        }
        for(int s = 0; s < surfaceCnt; s++)
        {
            for(int j = 1; j < stLen; j++)
            {
                int i_1 = 0;
                int i = 0;
                for (i = 0; i != -1; i_1 = i, i = surfaces[s].nextIdx[i,j - 1])
                {
                    Vector3 vel = sim.getVelocity(surfaces[s].gridPos[i, j - 1]);
                    surfaces[s].gridPos[i, j] = surfaces[s].gridPos[i, j - 1] + vel * particleSpeed;
                    sim.no_stick(ref surfaces[s].gridPos[i, j]);

                    surfaces[s].pred[i, j] = i;
                    surfaces[s].succ[i, j - 1] = i;
                    surfaces[s].valid[i, j] = surfaces[s].valid[i, j - 1];

                    if (i != 0)
                        surfaces[s].nextIdx[i_1, j] = i;
                }
                surfaces[s].nextIdx[i_1, j] = -1;

                {   //split
                    i_1 = i = 0;
                    for(int i1 = surfaces[s].nextIdx[0, j]; i1 != -1; i_1 = i, i = i1, i1 = surfaces[s].nextIdx[i1, j])
                    {
                        if (i1 - i <= 1)
                            continue;
                        if (!surfaces[s].valid[i, j])
                            continue;

                        int i_new = (i + i1) / 2;
                        int i2 = surfaces[s].nextIdx[i1, j];

                        bool splitFlag = false;

                        //alpha
                        if ((surfaces[s].gridPos[i, j] - surfaces[s].gridPos[i1, j]).magnitude > maxWidth)
                            splitFlag = true;

                        //beta
                        if (i2 != -1 && surfaces[s].valid[i_1, j] && surfaces[s].valid[i, j] && surfaces[s].valid[i1, j] &&
                            Curv(surfaces[s].gridPos[i_1, j], surfaces[s].gridPos[i, j], surfaces[s].gridPos[i1, j])
                            + Curv(surfaces[s].gridPos[i, j], surfaces[s].gridPos[i1, j], surfaces[s].gridPos[i2, j]) > beta)
                            splitFlag = true;

                        if (!splitFlag)
                            continue;

                        if (i == 0 || i2 == -1)
                        {
                            surfaces[s].gridPos[i_new, j] = (surfaces[s].gridPos[i, j] + surfaces[s].gridPos[i1, j]) / 2.0f;
                            sim.no_stick(ref surfaces[s].gridPos[i_new, j]);
                        }
                        else
                        {
                            surfaces[s].gridPos[i_new, j] = (surfaces[s].gridPos[i, j] + surfaces[s].gridPos[i1, j]) * 9.0f / 16.0f
                                                            - (surfaces[s].gridPos[i_1, j] + surfaces[s].gridPos[i2, j]) / 16.0f;
                            sim.no_stick(ref surfaces[s].gridPos[i_new, j]);
                        }
                        float d1 = (surfaces[s].gridPos[i, j] - surfaces[s].gridPos[i_new, j]).magnitude;
                        float d2 = (surfaces[s].gridPos[i_new, j] - surfaces[s].gridPos[i1, j]).magnitude;

                        surfaces[s].areaZero[i_new] = surfaces[s].areaZero[i] * d1 / (d1 + d2);
                        surfaces[s].areaZero[i] = surfaces[s].areaZero[i] - surfaces[s].areaZero[i_new];

                        surfaces[s].nextIdx[i, j] = i_new;
                        surfaces[s].pred[i_new, j] = i1;
                        surfaces[s].valid[i_new, j] = true;
                        surfaces[s].nextIdx[i_new, j] = i1;
                    }
                }   //split end

                {   //merge
                    i_1 = 0;
                    i = surfaces[s].nextIdx[0, j];
                    if (i == -1) continue;
                    for(int i1 = surfaces[s].nextIdx[i, j]; i1 != -1; i = i1, i1 = surfaces[s].nextIdx[i1, j])
                    {
                        int i2 = surfaces[s].nextIdx[i1, j];
                        if (i2 == -1)
                            break;

                        if(!surfaces[s].valid[i_1,j]|| !surfaces[s].valid[i, j]|| !surfaces[s].valid[i1, j])
                        {
                            i_1 = i;
                            continue;
                        }
                        bool mergeFlag = false;
                        float sumArea = surfaces[s].areaZero[i_1] + surfaces[s].areaZero[i];

                        //delta and zeta
                        if ((surfaces[s].gridPos[i_1, j] - surfaces[s].gridPos[i, j]).magnitude + (surfaces[s].gridPos[i1, j] - surfaces[s].gridPos[i, j]).magnitude < 2 * minWidth)
                        //    if (Curv(surfaces[s].gridPos[i_1, j], surfaces[s].gridPos[i, j], surfaces[s].gridPos[i1, j]) < zeta)
                                mergeFlag = true;

                        if (!mergeFlag)
                        {
                            i_1 = i;
                            continue;
                        }

                        int pre = surfaces[s].pred[i, j];
                        while (surfaces[s].succ[pre, j - 1] == i)
                        {
                            surfaces[s].succ[pre, j - 1] = i_1;
                            pre = surfaces[s].nextIdx[pre, j - 1];
                        }

                        surfaces[s].areaZero[i_1] = sumArea;
                        surfaces[s].areaZero[i] = 0;

                        surfaces[s].valid[i, j] = false;
                        surfaces[s].nextIdx[i_1, j] = i1;
                    }
                }   //merge end

                {   //validity check
                    i = 0;
                    for(int i1 = surfaces[s].nextIdx[0, j]; i1 != -1; i = i1, i1 = surfaces[s].nextIdx[i1, j])
                    {
                        int _i = surfaces[s].pred[i, j];
                        int _i1 = surfaces[s].pred[i1, j];

                        surfaces[s].opacity[i, j] = 0;

                        if (_i == _i1)
                            continue;
                        if (!surfaces[s].valid[i, j])
                            continue;

                        float d1 = (surfaces[s].gridPos[i, j] - surfaces[s].gridPos[i1, j]).magnitude;
                        float d2 = (surfaces[s].gridPos[_i, j - 1] - surfaces[s].gridPos[_i1, j - 1]).magnitude;
                        float d3 = (surfaces[s].gridPos[i, j] - surfaces[s].gridPos[_i, j - 1]).magnitude;

                        //gamma
                        if (d1 - d2 > gamma * d3)
                            surfaces[s].valid[i, j] = false;

                        //if (d1 > 3 * surfaces[s].areaZero[i])
                        //    surfaces[s].valid[i, j] = false;
                    }
                    surfaces[s].opacity[i, j] = 0;

                }   //validity check end

                {   //opacity
                    i = 0;
                    for(int i1= surfaces[s].nextIdx[0,j]; i1 != -1; i = i1, i1 = surfaces[s].nextIdx[i1, j])
                    {
                        if (!surfaces[s].valid[i, j])
                            continue;

                        float area = 0;
                        float areaZero = surfaces[s].areaZero[i];

                        {
                            area = (surfaces[s].gridPos[i1, j] - surfaces[s].gridPos[i, j]).magnitude;
                        }

                        float opacity = 0;
                        if (area <= 1e-9)
                            opacity = 1.0f;
                        else
                        {
                            opacity = areaZero / area;
                            if (opacity > 1)
                                opacity = 1;
                            opacity = opacity * opacity;
                        }

                        if (surfaces[s].opacity[i, j] == 0)
                            surfaces[s].opacity[i, j] = opacity;
                        else
                            surfaces[s].opacity[i, j] = (surfaces[s].opacity[i, j] + opacity) / 2;
                        surfaces[s].opacity[i1, j] = opacity;
                    }
                }   //opacity end

            }

        }

        AssignMeshIndex();
        ShowMesh();

    }

    void AssignMeshIndex()
    {
        ResetMesh();
        for (int s = 0; s < surfaceCnt; s++)
        {
            for (int j = 0; j < stLen; j++)
            {
                int i = 0;
                for (int i1 = surfaces[s].nextIdx[0, j]; i1 != -1; i = i1, i1 = surfaces[s].nextIdx[i1, j])
                {
                    int _i1 = surfaces[s].pred[i1, j];
                    int i_ = surfaces[s].succ[i, j];

                    if (!surfaces[s].valid[i, j])
                        continue;

                    float colori = 0;
                    float colori1 = 0;
                    sim.getVelocityAndTemp(surfaces[s].gridPos[i, j], ref colori);
                    sim.getVelocityAndTemp(surfaces[s].gridPos[i1, j], ref colori1);

                    if (j != 0)
                    {
                        float color_i1 = 0;
                        sim.getVelocityAndTemp(surfaces[s].gridPos[_i1, j - 1], ref color_i1);



                    }
                    if (j != stLen - 1)
                    {
                        float colori_ = 0;
                        sim.getVelocityAndTemp(surfaces[s].gridPos[i_, j + 1], ref colori_);



                    }
                }
            }

        }
    }

    float Curv(Vector3 u, Vector3 v, Vector3 w)
    {
        return ((Vector3.Dot(u - v, w - v) / ((u - v).magnitude * (w - v).magnitude)) + 1) / 2.0f;
    }

    public void setStartSurface()
    {
        animationStarted = true;
    }

    public void StopSurfaceAnimation()
    {
        if (animationStarted)
        {
            animationStarted = false;
            surfaceCreated = false;
            ResetMesh();
            timer = 0;
        }
    }


    void ShowMesh()
    {
        _mesh.RecalculateBounds();

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
