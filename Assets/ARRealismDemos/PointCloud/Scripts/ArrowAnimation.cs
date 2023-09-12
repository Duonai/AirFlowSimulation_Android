using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ArrowAnimation : MonoBehaviour
{
    public Simulation sim;

    public GameObject arrowObj;
    public float arrowSpeed = 30.0f;
    public float createPeriod = 2.5f;

    List<Arrow> arrows = new List<Arrow>();
    //List<Arrow> destroybuffer = new List<Arrow>();
    float timer = 50;

    int gridSizeX;
    int gridSizeY;
    int gridSizeZ;

    int ACType = 0;
    bool animationStarted = false;
    Vector3Int currentACIndex;

    class Arrow
    {
        public GameObject arrowObj;
        public Vector3 position;
    };

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
        //StartAnimation();
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

            UpdateArrows();
        }
    }

    public void setStartArrow()
    {
        animationStarted = true;
    }

    public void StopArrowAnimation()
    {
        if (animationStarted)
        {
            animationStarted = false;
            foreach (Arrow arrow in arrows)
            {
                Destroy(arrow.arrowObj);
            }
            arrows.Clear();
            timer = 50;
        }
    }

    void RemoveArrows()
    {

    }

    void CreateArrows(int i, int j, int k)
    {
        Vector3 createPos = GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getGridPosition(i, j, k);
        createPos.y = createPos.y - 0.05f;
        GameObject newArrowObj = Instantiate(arrowObj, this.transform);
        newArrowObj.transform.localPosition = createPos;
        newArrowObj.transform.localRotation = Quaternion.identity;
        newArrowObj.transform.localScale = arrowObj.transform.localScale;

        Arrow newArrow = new Arrow();
        newArrow.arrowObj = newArrowObj;
        newArrow.position = sim.WorldToGridPos(createPos);
        arrows.Add(newArrow);
        
        //Debug.Log("newArrows");
    }

    void StartAnimation()
    {
        int ind_x = currentACIndex.x;
        int ind_y = currentACIndex.y;
        int ind_z = currentACIndex.z;
        
        //ind_y -= 1;
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x + i > -1 && ind_x + i < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z - 4 > -1 && ind_z - 4 < gridSizeZ)
            {
                CreateArrows(ind_x + i, ind_y, ind_z - 4);
            }
        }
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x + i > -1 && ind_x + i < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z + 4 > -1 && ind_z + 4 < gridSizeZ)
                CreateArrows(ind_x + i, ind_y, ind_z + 4);

        }
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x - 4 > -1 && ind_x - 4 < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z + i > -1 && ind_z + i < gridSizeZ)
                CreateArrows(ind_x - 4, ind_y, ind_z + i);

        }
        for (int i = -2; i <= 2; i++)
        {
            if (ind_x + 4 > -1 && ind_x + 4 < gridSizeX && ind_y > -1 && ind_y < gridSizeY && ind_z + i > -1 && ind_z + i < gridSizeZ)
                CreateArrows(ind_x + 4, ind_y, ind_z + i);

        }
        //Debug.Log("newArrows");
    }

    void UpdateArrows()
    {
        if (animationStarted)
        {
            timer += 1;
            if (timer >= 50)
            {
                //Debug.Log("newArrows");
                StartAnimation();
                timer = 0;
            }
            foreach (Arrow arrow in arrows)
            {
                //float temp = sim.getTemperature
                float temp = 0.0f;
                Vector3 dir = sim.getVelocityAndTemp(arrow.position, ref temp);
                float vel = dir.magnitude;
                //Debug.Log("dir: " + dir.x + ", " + dir.y + ", " + dir.z);
                //Debug.Log("temp: " + temp);
                //if (vel < 0.01f)
                //{
                //    arrows.Remove(arrow);
                //    Destroy(arrow.arrowObj);
                //    continue;
                //}

                float largeStep = 0.055f * 30;
                float smallStep = 0.035f * 30;
                bool rotFlag = false;
                //Vector3 largePos = arrow.position + dir * largeStep * Time.deltaTime;
                Vector3 largePos = arrow.position + dir * 1.2f;
                //float rotVal = 0;
                //Vector3 rotPos = new Vector3(Mathf.Cos(rotVal) * largePos.x + Mathf.Sin(rotVal) * largePos.z,
                //                                    largePos.y,
                //                                    -Mathf.Sin(rotVal) * largePos.x + Mathf.Cos(rotVal) * largePos.z);

                sim.no_stick(ref largePos);

                Color color = sim.tempToColor(temp);
                if (color.g == 0.0f)
                {
                    //if not complete_occlude;
                    color.a = 0.7f;
                }
                else
                {
                    color.a = 1.0f;
                }

                arrow.arrowObj.GetComponentInChildren<Renderer>().material.color = color;

                Vector3 velocity = sim.GridToWorldDir(dir);
                float theta = Mathf.Acos(velocity.y / velocity.magnitude);
                Vector3 qaxis = (new Vector3(velocity.z, 0, -velocity.x)).normalized;

                //arrow.color = color
                //Debug.Log(string.Format("{0}", vel));

                arrow.arrowObj.transform.localPosition = sim.GridToWorldPos(largePos);
                //arrow.arrowObj.transform.rotation = sim.velToRot(dir);
                arrow.arrowObj.transform.localRotation = sim.G2W * Quaternion.AngleAxis(theta * Mathf.Rad2Deg, qaxis);
                arrow.position = largePos;
            }
        }
    }

    public void updateACIndex(int[] idx)
    {
        currentACIndex.x = idx[0];
        currentACIndex.y = idx[1];
        currentACIndex.z = idx[2];
    }
}
