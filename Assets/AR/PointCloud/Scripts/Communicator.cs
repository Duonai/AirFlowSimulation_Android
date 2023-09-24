using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Net.Sockets;
using System.Net;
using System.IO;
using System.Threading;
using System.Linq;
using System;

public class Communicator : MonoBehaviour
{
    private Socket m_Client;
    public string m_Ip = "163.152.161.221";
    public int m_Port = 1235;
    private IPEndPoint m_ServerIpEndPoint;
    private EndPoint m_RemoteEndPoint;

    //grid info
    int gridSizeX;
    int gridSizeY;
    int gridSizeZ;
    int gridSize3;

    //send data - AC info
    int acType = 2;
    int[] acPosition = new int[] { 0, 0, 0 };
    int acDirection = 0;
    int acVentLevel = 1;
    int acVentSpeed = 1;
    bool acReset = false;
    float currentTemp = 0.0f;
    float targetTemp = 1.0f;

    //send data - occ grid
    bool[] occupancyGrid;
    bool[] occupancyGridTemp;
    uint[] changeOccIndex;

    //send data - BIM
    float distXR = 0.0f;
    float distXL = 0.0f;
    float distYU = 0.0f;
    float distYD = 0.0f;
    float roomSizeX = 0.0f;
    float roomSizeY = 0.0f;
    float roomSizeZ = 0.0f;
    ushort roomNum = 0;

    //Send & Receive data for graph
    bool drawGraph = false;
    ushort[] targetPosition = new ushort[] { 0, 0, 0 };
    float[] graphInfo;

    //Receive data - physics data
    float[] velocity;
    float[] temperature;
    float[] tempPhysicsValue;
    Packet phyPacket;
    bool occupancyUpdated = false;
    bool occupancySent = false;
    bool acInfoUpdated = true;
    byte timeLater = 0x00;
    bool jumpSet = false;
    bool jumpDone = true;
    bool graphDone = true;
    bool loadDone = false;
    bool connectDone = false;

    bool spaceStart = false;

    Thread thread;

    public bool pauseFlag = false;

    enum Types : byte
    {
        initData,
        provideSpaceInfo = 0x10,
        provideOccupancy = 0x11,
        provideACInfo = 0x14,
        requestPhysicalValue = 0x12,
        storeBIM = 0x16,
        requestBIM = 0x17,
        requestGraph = 0x18,
        other = 0x15
    }

    Types connectType;

    private void Start()
    {
        gridSizeX = 64;
        gridSizeY = 64;
        gridSizeZ = 64;
        gridSize3 = gridSizeX * gridSizeY * gridSizeZ;
        acPosition = new int[] { gridSizeX / 2, gridSizeY - 1, gridSizeZ / 2 };

        connectType = Types.initData;
    }

    public void initialize(int gridSizeX, int gridSizeY, int gridSizeZ)
    {
        this.gridSizeX = gridSizeX;
        this.gridSizeY = gridSizeY;
        this.gridSizeZ = gridSizeZ;
        this.gridSize3 = gridSizeX * gridSizeY * gridSizeZ;
        acPosition = new int[] { gridSizeX / 2, gridSizeY - 1, gridSizeZ / 2 };

        tempPhysicsValue = new float[gridSize3 * 4];
        velocity = new float[gridSize3 * 3];
        temperature = new float[gridSize3];
        occupancyGrid = new bool[gridSize3];
        
        connectType = Types.provideSpaceInfo;
    }

    public void initializeForBIM(int gridSizeX, int gridSizeY, int gridSizeZ, float distXR, float distXL, float distYU, float distYD,
        float roomSizeX, float roomSizeY, float roomSizeZ, ushort roomNum)
    {
        this.gridSizeX = gridSizeX;
        this.gridSizeY = gridSizeY;
        this.gridSizeZ = gridSizeZ;
        this.gridSize3 = gridSizeX * gridSizeY * gridSizeZ;

        this.distXR = distXR;
        this.distXL = distXL;
        this.distYU = distYU;
        this.distYD = distYD;

        this.roomSizeX = roomSizeX;
        this.roomSizeY = roomSizeY;
        this.roomSizeZ = roomSizeZ;

        this.roomNum = roomNum;

        occupancyGrid = new bool[gridSize3];

        connectType = Types.storeBIM;
    }

    public void storeTimeLate(int time)
    {
        jumpSet = true;
        timeLater = (byte)time;
        Debug.Log("store time");
    }

    public void resetTimerLater()
    {
        timeLater = 0;
    }

    public float[] getVelocity()
    {
        return velocity;
    }

    public float[] getTemperature()
    {
        return temperature;
    }

    public float[] getDistance()
    {
        float[] data = new float[] { distXR, distXL, distYU, distYD };
        return data;
    }

    public float[] getRoomSize()
    {
        float[] data = new float[] { roomSizeX, roomSizeY, roomSizeZ };
        return data;
    }

    public float[] getGridSize()
    {
        float[] data = new float[] { gridSizeX, gridSizeY, gridSizeZ };
        return data;
    }

    public bool[] getOccupancyGrid()
    {
        return occupancyGrid;
    }

    public float[] getGraphInfo()
    {
        return graphInfo;
    }

    public bool getJumpDone()
    {
        return jumpDone;
    }

    public bool getJumpSet()
    {
        return jumpSet;
    }

    public bool getGraphDone()
    {
        return graphDone;
    }

    public bool getLoadDone()
    {
        return loadDone;
    }

    public bool getConnectDone()
    {
        return connectDone;
    }

    public void setRequestBIMType()
    {
        connectType = Types.requestBIM;
    }

    public void setRoomNum(ushort RoomNum)
    {
        roomNum = RoomNum;
    }

    // update ac info
    public void updateACPosition(int posx, int posy, int posz)
    {
        if (acPosition[0] != posx || acPosition[1] != posy || acPosition[2] != posz)
        {
            acPosition[0] = posx;
            acPosition[1] = posy;
            acPosition[2] = posz;
            acInfoUpdated = true;
        }
    }

    public void updateACVentLevel(int ventLevel)
    {
        if (acVentLevel != ventLevel && ventLevel > 0 && ventLevel < 5)
        {
            acVentLevel = ventLevel;
            acInfoUpdated = true;
        }
    }

    public void updateACVentSpeed(int ventSpeed)
    {
        if (acVentSpeed != ventSpeed && ventSpeed > 0 && ventSpeed < 5)
        {
            acVentSpeed = ventSpeed;
            acInfoUpdated = true;
        }
    }

    public void updateACReset()
    {
        acReset = true;
        acInfoUpdated = true;
    }

    public void updateACInfo(int type, int[] position)
    {
        if (acType != type)
        {
            acType = type;
            acInfoUpdated = true;
        }

        if (acPosition[0] != position[0] || acPosition[1] != position[1] || acPosition[2] != position[2])
        {
            acPosition[0] = position[0];
            acPosition[1] = position[1];
            acPosition[2] = position[2];
            acInfoUpdated = true;
        }
    }

    public void updateOccupacyGrid(bool[] grid) //thread 분리
    {
        occupancyGridTemp = grid;
        changeOccIndex = new uint[] { };
        if (occupancyGridTemp != null && !occupancyGrid.SequenceEqual(occupancyGridTemp) && occupancySent)
        {
            List<uint> tempList = new List<uint>();

            for (int i = 0; i < occupancyGrid.Length; i++)
            {
                if (occupancyGrid[i] != occupancyGridTemp[i])
                    tempList.Add((uint)i);
            }
            
            occupancyGrid = occupancyGridTemp.Clone() as bool[];

            changeOccIndex = tempList.ToArray();
        }
        else if (!occupancySent)
        {
            occupancyGrid = occupancyGridTemp.Clone() as bool[];
            return;
        }

        occupancyGridTemp = null;
        if (changeOccIndex.Length > 0)
            occupancyUpdated = true;
    }

    public void updateTargetPosForGraph(ushort X, ushort Y, ushort Z)
    {
        targetPosition[0] = X;
        targetPosition[1] = Y;
        targetPosition[2] = Z;

        graphDone = false;
        drawGraph = true;
    }

    public void ConnectToServer()
    {
        // 이미 연결되었다면 함수 무시
        if (connectDone) return;

        // 소켓 생성
        try
        {
            m_ServerIpEndPoint = new IPEndPoint(IPAddress.Parse(m_Ip), m_Port);
            m_Client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);

            m_Client.Connect(m_ServerIpEndPoint);
            connectDone = true;
        }
        catch (Exception e)
        {
            Debug.Log($"소켓에러 : {e.Message}");
        }
        m_Client.NoDelay = false;
        m_Client.Blocking = true;
        m_Client.ReceiveTimeout = 10000;
        m_Client.ReceiveBufferSize = 10000;

        startWork(connectType);
    }

    void Update()
    {

    }

    void startWork(Types type)
    {
        switch (type)
        {
            case Types.provideSpaceInfo:
                if (sendSpaceInfo())
                {
                    receive();
                }
                break;
            case Types.storeBIM:
                if (sendSaveBIM())
                {
                    receive();
                }
                break;
            case Types.requestBIM:
                if (sendRequestBIM())
                {
                    receive();
                }
                if (sendSpaceInfo())
                {
                    receive();
                }
                break;
            default:
                Debug.Log("error:start type error");
                break;
        }

        Invoke("spaceStartSet", 2);
    }

    void spaceStartSet()
    {
        spaceStart = true;
        thread = new Thread(new ThreadStart(work));
        thread.Start();
    }

    void work()
    {
        while (true)
        {
            if (connectType != Types.storeBIM && spaceStart)
            {
                if (send())
                    receive();
            }
        }
    }

    private bool send()
    {
        if (acInfoUpdated)
        {
            acInfoUpdated = false;
            return sendACInfo();
        }

        if (drawGraph)
        {
            drawGraph = false;
            return sendRequestGraph();
        }

        if (occupancyUpdated || !occupancySent)
        {
            return selectOccType();
        }

        //Debug.Log("sendRequestPhysicsData type");
        return sendRequestPhysicsData();
    }

    private bool selectOccType()
    {
        if (!occupancySent)
        {
            bool rst = sendOccupancy();
            occupancySent = rst;
            return rst;
        }
        occupancyUpdated = false;
        return sendOccupancyIndex();
    }

    private bool sendSpaceInfo()
    {
        Packet msg = new Packet(); //???
        msg.init(sizeof(int) * 3 + sizeof(int) + 1);
        msg.push_byte((byte)Types.provideSpaceInfo);
        msg.push_int32(gridSizeX);
        msg.push_int32(gridSizeY);
        msg.push_int32(gridSizeZ);

        return sendData(msg);
    }

    private bool sendACInfo()
    {
        Packet msg = new Packet(); //???
        msg.init(sizeof(int) * acPosition.Length +
            sizeof(int) * 4 +
            sizeof(bool) +
            sizeof(float) * 2 +
            sizeof(int) + 1);
        msg.push_byte((byte)Types.provideACInfo);

        msg.push_int32(acType);
        msg.push_int32(acDirection);

        for (int i = 0; i < acPosition.Length; i++)
            msg.push_int32(acPosition[i]);

        msg.push_int32(acVentLevel);
        msg.push_int32(acVentSpeed);

        msg.push_bool(acReset);

        msg.push_single(currentTemp);
        msg.push_single(targetTemp);

        return sendData(msg);
    }

    private bool sendOccupancy()
    {
        Packet msg = new Packet();
        msg.init(occupancyGrid.Length + sizeof(int) + 1);
        msg.push_byte((byte)Types.provideOccupancy);

        for (int i = 0; i < occupancyGrid.Length; i++)
        {
            msg.push_bool(occupancyGrid[i]);
            //Debug.Log("true num: " + i);
        }

        return sendData(msg);
    }

    private bool sendOccupancyIndex()
    {
        Packet msg = new Packet();
        msg.init(changeOccIndex.Length * sizeof(int) + sizeof(int) + 1);
        msg.push_byte((byte)Types.provideOccupancy);

        for (int i = 0; i < changeOccIndex.Length; i++)
            msg.push_UInt32(changeOccIndex[i]);

        return sendData(msg);
    }

    private bool sendRequestPhysicsData()
    {
        Packet msg = new Packet();
        msg.init(sizeof(int) + 1 + 1);
        msg.push_byte((byte)Types.requestPhysicalValue);
        msg.push_byte(timeLater);
        if (jumpSet)
            jumpDone = false;
        resetTimerLater();

        return sendData(msg);
    }

    private bool sendRequestBIM()
    {
        Packet msg = new Packet();
        msg.push_byte((byte)Types.requestBIM);
        msg.push_UInt16(roomNum);

        return sendData(msg);
    }

    private bool sendSaveBIM()
    {
        Packet msg = new Packet();
        msg.init(sizeof(int) + sizeof(ushort) * (3 + 1) + sizeof(float) * (4 + 3) +
            occupancyGrid.Length + 1);
        msg.push_byte((byte)Types.storeBIM);

        msg.push_UInt16((ushort)gridSizeX);
        msg.push_UInt16((ushort)gridSizeY);
        msg.push_UInt16((ushort)gridSizeZ);

        msg.push_single(distXL);
        msg.push_single(distXR);
        msg.push_single(distYU);
        msg.push_single(distYD);

        msg.push_single(roomSizeX);
        msg.push_single(roomSizeY);
        msg.push_single(roomSizeZ);

        msg.push_UInt16(roomNum);

        msg.push_bool_array(occupancyGrid);

        return sendData(msg);
    }

    private bool sendRequestGraph()
    {
        Packet msg = new Packet();
        msg.init(sizeof(int) + sizeof(ushort) * 3 + 1);
        msg.push_byte((byte)Types.requestGraph);

        msg.push_UInt16(targetPosition[0]);
        msg.push_UInt16(targetPosition[1]);
        msg.push_UInt16(targetPosition[2]);

        return sendData(msg);
    }

    private bool sendData(Packet msg)
    {
        if (!connectDone)
            return false;

        //byte[] length = new byte[] { msg.get_buffer()[13] , msg.get_buffer()[14] , msg.get_buffer()[15] , msg.get_buffer()[16] };
        // Debug.Log("length: " + BitConverter.ToUInt32(length, 0));

        //byte[] tempBuffer = new byte[5];
        int tempLen = 0;
        int tempSize = 1440;
        if (true)
        {
            if (msg.get_buffer().Length > 1440)
            {
                while (true)
                {
                    try
                    {
                        m_Client.Send(msg.get_buffer(), tempLen, tempSize, SocketFlags.None);
                        //Debug.Log("templen: " + tempLen);
                    }
                    catch (Exception e)
                    {
                        Debug.Log($"send Error : {e.Message}");
                        return false;
                    }
                    if (tempSize < 1440)
                        break;
                    tempLen += 1440;
                    if (tempLen + 1440 > msg.buffer_length)
                        tempSize = msg.buffer_length - tempLen;
                }
            }
            else
            {
                try
                {
                    m_Client.Send(msg.get_buffer(), 0, msg.get_buffer().Length, SocketFlags.None);
                }
                catch (Exception e)
                {
                    Debug.Log($"send Error : {e.Message}");
                    return false;
                }
            }
        }

        return true;
    }

    private void receive()
    {
        byte[] rcvbuf;
        List<byte> rcvList = new List<byte>();
        int head = 0;
        int rcvLen = 0;
        uint len = 0;
        bool lenDone = false;
        byte[] src = new byte[10000];
        int receive = 0;
        while (true)
        {
            receive = m_Client.Receive(src);
            //Debug.Log("receive: " + receive);
            if (src != null)
            {
                rcvLen = receive;
                head += rcvLen;
                for (int i = 0; i < receive; i++)
                    rcvList.Add(src[i]);
                if (!lenDone && head >= 4)
                {
                    len = ((uint)rcvList[0] << 24 | (uint)rcvList[1] << 16 | (uint)rcvList[2] << 8 | (uint)rcvList[3]);
                    //Debug.Log("len: " + len);
                    //Debug.Log("head: " + head);
                    lenDone = true;
                }
            }
            if (head >= (int)len + sizeof(int))
            {
                rcvbuf = rcvList.ToArray();
                break;
            }
        }
        //Debug.Log("len: " + len);
        //Chat.instance.ShowMessage("len: " + len);
        Packet rcvData = new Packet();
        rcvData.init(rcvbuf, (int)len + sizeof(int));
        process(rcvData);
    }

    private void process(Packet data)
    {
        byte type = data.pop_byte()[0];
        switch (type)
        {
            case (byte)Types.provideSpaceInfo:
                {
                    byte[] msg = data.get_body();
                    //Debug.Log(System.Text.Encoding.UTF8.GetString(msg));
                    break;
                }
            case (byte)Types.provideOccupancy:
                {
                    byte[] msg = data.get_body();
                    //Debug.Log(System.Text.Encoding.UTF8.GetString(msg));
                    break;
                }
            case (byte)Types.provideACInfo:
                {
                    acReset = false;
                    byte[] msg = data.get_body();
                    //Debug.Log(System.Text.Encoding.UTF8.GetString(msg));
                    break;
                }
            case (byte)Types.storeBIM:
                {
                    byte[] msg = data.get_body();
                    //Debug.Log(System.Text.Encoding.UTF8.GetString(msg));
                    break;
                }
            case (byte)Types.requestBIM:
                {
                    gridSizeX = (int)data.pop_UInt16();
                    gridSizeY = (int)data.pop_UInt16();
                    gridSizeZ = (int)data.pop_UInt16();
                    gridSize3 = gridSizeX * gridSizeY * gridSizeZ;

                    acPosition = new int[] { gridSizeX / 2, gridSizeY - 1, gridSizeZ / 2 };

                    occupancyGrid = new bool[gridSize3];
                    tempPhysicsValue = new float[gridSize3 * 4];
                    velocity = new float[gridSize3 * 3];
                    temperature = new float[gridSize3];

                    distXL = data.pop_single();
                    distXR = data.pop_single();
                    distYU = data.pop_single();
                    distYD = data.pop_single();

                    roomSizeX = data.pop_single();
                    roomSizeY = data.pop_single();
                    roomSizeZ = data.pop_single();

                    occupancyGrid = data.pop_bool_array(gridSize3);

                    loadDone = true;
                    break;
                }
            case (byte)Types.requestGraph:
                {
                    int graphInfoLen = data.get_body_length() / sizeof(float);

                    List<float> graphList = new List<float>();

                    for (int i = 0; i < graphInfoLen; i++)
                        graphList.Add(data.pop_single());

                    graphInfo = graphList.ToArray();

                    graphDone = true;

                    break;
                }
            case (byte)Types.requestPhysicalValue:
                {
                    phyPacket = data;

                    float[,] direction = new float[gridSize3, 3];
                    float[] scale = new float[gridSize3];

                    if (phyPacket != null)
                    {
                        double directionCount = (double)gridSize3 * 1.5 / 3;
                        for (int i = 0; i < (int)directionCount; i++)
                        {
                            byte x1 = phyPacket.pop_byte()[0];
                            int x2 = x1 >> 4;
                            direction[i * 2, 0] = (float)x2 / 15.0f * 2.0f - 1.0f;
                            int x3 = x1 & 0b00001111;
                            direction[i * 2, 1] = (float)x3 / 15.0f * 2.0f - 1.0f;

                            byte x4 = phyPacket.pop_byte()[0];
                            int x5 = x4 >> 4;
                            direction[i * 2, 2] = (float)x5 / 15.0f * 2.0f - 1.0f;
                            int x6 = x4 & 0b00001111;
                            direction[i * 2 + 1, 0] = (float)x6 / 15.0f * 2.0f - 1.0f;

                            byte x7 = phyPacket.pop_byte()[0];
                            int x8 = x7 >> 4;
                            direction[i * 2 + 1, 1] = (float)x8 / 15.0f * 2.0f - 1.0f;
                            int x9 = x7 & 0b00001111;
                            direction[i * 2 + 1, 2] = (float)x9 / 15.0f * 2.0f - 1.0f;
                        }

                        for (int i = 0; i < gridSize3; i++)
                        {
                            ushort x1 = phyPacket.pop_UInt16();
                            scale[i] = (float)x1 / 30000.0f;
                        }

                        for (int i = 0; i < gridSize3; i++)
                        {
                            for (int j = 0; j < 3; j++)
                            {
                                tempPhysicsValue[i * 3 + j] = direction[i, j] * scale[i];
                            }
                        }

                        for (int i = 0; i < gridSize3; i++)
                        {
                            byte x1 = phyPacket.pop_byte()[0];
                            tempPhysicsValue[gridSize3 * 3 + i] = (float)x1 / 256;
                        }

                        int valSize = tempPhysicsValue.Length;
                        pauseFlag = false;
                        if (!pauseFlag)
                        {
                            Array.Copy(tempPhysicsValue, velocity, valSize - gridSize3);
                            Array.Copy(tempPhysicsValue, valSize - gridSize3, temperature, 0, gridSize3);
                            pauseFlag = true;
                        }

                        if (jumpDone == false && jumpSet == true)
                        {
                            jumpDone = true;
                            jumpSet = false;
                            Debug.Log("jump Done");
                        }
                    }
                    break;
                }
            default:
                {
                    Debug.Log("Unknown type");
                    break;
                }
        }
    }

    void OnApplicationQuit()
    {
        CloseSocket();
        thread.Abort();
    }

    void CloseSocket()
    {
        if (!connectDone) return;

        m_Client.Close();
        connectDone = false;
    }
}
