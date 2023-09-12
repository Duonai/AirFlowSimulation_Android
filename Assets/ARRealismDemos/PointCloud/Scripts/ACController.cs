using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ACController : MonoBehaviour
{
    ARRaycastManager rayManager;

    public GameObject ceilingAC;
    public GameObject ceilingBlade1;
    public GameObject ceilingBlade2;
    public GameObject ceilingBlade3;
    public GameObject ceilingBlade4;
    GameObject instCeilingAC;
    GameObject instCeilingBlade1;
    GameObject instCeilingBlade2;
    GameObject instCeilingBlade3;
    GameObject instCeilingBlade4;

    public GameObject towerAC;
    GameObject instTowerAC;

    Vector3 currentPosition;
    uint currentACModel = 2;
    uint currentAngle = 1;
    uint currentSpeed = 1;
    public Vector3Int acIndex;
    bool acInstalled = false;

    // Start is called before the first frame update
    void Start()
    {
        rayManager = GameObject.Find("XR Origin").GetComponent<ARRaycastManager>();
        ceilingAC.transform.localScale = new Vector3(0.032f, 0.032f, 0.032f);
        ceilingBlade1.transform.localScale = new Vector3(0.032f, 0.032f, 0.032f);
        ceilingBlade2.transform.localScale = new Vector3(0.032f, 0.032f, 0.032f);
        ceilingBlade3.transform.localScale = new Vector3(0.032f, 0.032f, 0.032f);
        ceilingBlade4.transform.localScale = new Vector3(0.032f, 0.032f, 0.032f);

        towerAC.transform.localScale = new Vector3(0.0385f, 0.0385f, 0.0385f);
    }

    // Update is called once per frame
    void Update()
    {
        if (GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getCreated())
        {
            if (Input.touchCount > 0 && !acInstalled)
            {
                List<ARRaycastHit> hitInfos = new List<ARRaycastHit>();
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began && rayManager.Raycast(touch.position, hitInfos, TrackableType.Planes))
                {
                    //for (int i = 0; i < hitInfos.Count; i++)
                    {
                        Vector3 maxSize = GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getGridMax();
                        Vector3 minSize = GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getGridMin();
                        //if (Mathf.Abs(hitInfos[i].pose.position.y - maxSize.y) < 0.15)
                        {
                            Vector3 position = hitInfos[0].pose.position;
                            instCeilingAC = Instantiate(ceilingAC, new Vector3(position.x, maxSize.y - 0.03f, position.z), new Quaternion(0, 0, 0, 0));
                            instCeilingBlade1 = Instantiate(ceilingBlade1, new Vector3(position.x - 0.36f, maxSize.y - 0.03f - 0.009f, position.z), new Quaternion(0, 0, 0, 0));
                            instCeilingBlade2 = Instantiate(ceilingBlade2, new Vector3(position.x, maxSize.y - 0.03f - 0.009f, position.z - 0.36f), new Quaternion(0, 0, 0, 0));
                            instCeilingBlade3 = Instantiate(ceilingBlade3, new Vector3(position.x + 0.36f, maxSize.y - 0.03f - 0.009f, position.z), new Quaternion(0, 0, 0, 0));
                            instCeilingBlade4 = Instantiate(ceilingBlade4, new Vector3(position.x, maxSize.y - 0.03f - 0.009f, position.z + 0.36f), new Quaternion(0, 0, 0, 0));

                            instCeilingAC.transform.RotateAround(instCeilingBlade2.transform.position, new Vector3(0, 0, 1), 180);

                            instCeilingBlade1.transform.RotateAround(instCeilingBlade1.transform.position, new Vector3(0, 0, 1), 180);
                            instCeilingBlade1.transform.RotateAround(instCeilingBlade1.transform.position, new Vector3(0, 1, 0), 90);

                            instCeilingBlade2.transform.RotateAround(instCeilingBlade2.transform.position, new Vector3(0, 0, 1), 180);

                            instCeilingBlade3.transform.RotateAround(instCeilingBlade3.transform.position, new Vector3(0, 0, 1), 180);
                            instCeilingBlade3.transform.RotateAround(instCeilingBlade3.transform.position, new Vector3(0, 1, 0), -90);

                            instCeilingBlade4.transform.RotateAround(instCeilingBlade4.transform.position, new Vector3(0, 0, 1), 180);
                            instCeilingBlade4.transform.RotateAround(instCeilingBlade4.transform.position, new Vector3(0, 1, 0), 180);

                            acIndex = GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getGridIndex(position.x, maxSize.y - 0.03f, position.z);
                            currentPosition = new Vector3(position.x, maxSize.y - 0.03f, position.z);
                            GameObject.Find("Communicator").GetComponent<Communicator>().updateACInfo(2, new int[] { acIndex.x, acIndex.y, acIndex.z });
                            GameObject.Find("Communicator").GetComponent<Communicator>().updateACReset();

                            GameObject.Find("ArrowAnimation").GetComponent<ArrowAnimation>().updateACIndex(new int[] { acIndex.x, acIndex.y, acIndex.z });
                            GameObject.Find("ParticleAnimation1").GetComponent<ParticleAnimation1>().updateACIndex(new int[] { acIndex.x, acIndex.y, acIndex.z });
                            Debug.Log("index: " + acIndex.x + ", " + acIndex.y +", "+ acIndex.z);
                            //GameObject.Find("Communicator").GetComponent<Communicator>().ConnectToServer();

                            acInstalled = true;
                            //break;
                        }

                        {
                            Vector3 position = hitInfos[0].pose.position;
                            instTowerAC = Instantiate(towerAC, new Vector3(position.x, minSize.y, position.z), new Quaternion(0, 0, 0, 0));

                            acIndex = GameObject.Find("RawPointCloudBlender").GetComponent<RawPointCloudBlender>().getGridIndex(position.x, minSize.y, position.z);
                            currentPosition = new Vector3(position.x, minSize.y, position.z);
                            GameObject.Find("Communicator").GetComponent<Communicator>().updateACInfo(4, new int[] { acIndex.x, acIndex.y, acIndex.z });
                            GameObject.Find("Communicator").GetComponent<Communicator>().updateACReset();

                            GameObject.Find("ArrowAnimation").GetComponent<ArrowAnimation>().updateACIndex(new int[] { acIndex.x, acIndex.y, acIndex.z });
                            GameObject.Find("ParticleAnimation1").GetComponent<ParticleAnimation1>().updateACIndex(new int[] { acIndex.x, acIndex.y, acIndex.z });
                            Debug.Log("index: " + acIndex.x + ", " + acIndex.y + ", " + acIndex.z);
                            //GameObject.Find("Communicator").GetComponent<Communicator>().ConnectToServer();

                            acInstalled = true;
                            //break;
                        }
                    }
                }
            }
        }
    }
}
