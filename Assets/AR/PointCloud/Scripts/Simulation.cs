using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using System.IO;
using System;

public class Simulation
{
    float gridRotation = 0; //degrees
    [HideInInspector]
    public Quaternion W2G, G2W;
    //int gridSizeX = 60, gridSizeY = 27, gridSizeZ = 60;
    public gridUniform gd;

    public float[] velocity;
    public float[] temperature;
    public bool[] occupancy;
    public Vector3 ACPosition;

    public struct gridUniform
    {
        public int gridSizeX;
        public int gridSizeY;
        public int gridSizeZ;
        public Vector3 eye;
        public Vector3 maxVec;
        public Vector3 minVec;
        public float gridLengthX;
        public float gridLengthY;
        public float gridLengthZ;
        public Vector3 offset;
    }


    public void Init(Vector3Int gridSize, Vector3 maxVec, Vector3 minVec, Vector3 length, float rotation)
    {
        gridRotation = rotation;
        W2G = Quaternion.Euler(0, gridRotation, 0);
        G2W = Quaternion.Euler(0, -gridRotation, 0);

        gd.gridSizeX = gridSize.x;
        gd.gridSizeY = gridSize.y;
        gd.gridSizeZ = gridSize.z;
        gd.gridLengthX = length.x;
        gd.gridLengthY = length.y;
        gd.gridLengthZ = length.z;
        gd.maxVec = maxVec;
        gd.minVec = minVec;

        gd.offset = G2W * minVec;
    }
    public Vector3 WorldToGridPos(Vector3 pw)
    {
        Vector3 pg = W2G * (pw - gd.offset);
        pg.x *= gd.gridSizeX / gd.gridLengthX;
        pg.y *= gd.gridSizeY / gd.gridLengthY;
        pg.z *= gd.gridSizeZ / gd.gridLengthZ;

        return pg;
    }
    public Vector3 GridToWorldPos(Vector3 pg)
    {
        Vector3 pw = pg;
        pw.x *= gd.gridLengthX / gd.gridSizeX;
        pw.y *= gd.gridLengthY / gd.gridSizeY;
        pw.z *= gd.gridLengthZ / gd.gridSizeZ;
        pw = G2W * pw;
        pw += gd.offset;

        return pw;
    }
    public Vector3 WorldToGridDir(Vector3 pw)
    {
        Vector3 pg = W2G * pw;
        pg.x *= gd.gridSizeX / gd.gridLengthX;
        pg.y *= gd.gridSizeY / gd.gridLengthY;
        pg.z *= gd.gridSizeZ / gd.gridLengthZ;

        return pg;
    }
    public Vector3 GridToWorldDir(Vector3 pg)
    {
        Vector3 pw = pg;
        pw.x *= gd.gridLengthX / gd.gridSizeX;
        pw.y *= gd.gridLengthY / gd.gridSizeY;
        pw.z *= gd.gridLengthZ / gd.gridSizeZ;
        pw = G2W * pw;

        return pw;
    }
    //void ReadText()
    //{

    //    velocity = new Vector3[gd.gridSizeX, gd.gridSizeY, gd.gridSizeZ];
    //    temperature = new float[gd.gridSizeX, gd.gridSizeY, gd.gridSizeZ];
    //    occupancy = new bool[gd.gridSizeX, gd.gridSizeY, gd.gridSizeZ];

    //    TextAsset textAsset = Resources.Load("14300") as TextAsset;
    //    string str = textAsset.text;

    //    Debug.Log(str.Substring(0, 40));

    //    string path = "Assets/Resources/14300.txt";
    //    StreamReader reader = new StreamReader(path);
    //    string str = reader.ReadToEnd();

    //    reader.Close();

    //    char[] sepChar = { ' ', '\n' };

    //    string[] parsed = str.Split(sepChar);
    //    Array.Resize(ref parsed, parsed.Length - 1);

    //    float[] floats = Array.ConvertAll(parsed, float.Parse);

    //    for (int i = 0; i < gd.gridSizeX; i++)
    //    {
    //        for (int j = 0; j < gd.gridSizeY; j++)
    //        {
    //            for (int k = 0; k < gd.gridSizeZ; k++)
    //            {
    //                int idx = IDX(i, j, k);
    //                idx *= 4;

    //                velocity[i, j, k] = new Vector3(floats[idx], floats[idx + 1], floats[idx + 2]);
    //                temperature[i, j, k] = floats[idx + 3];
    //                occupancy[i, j, k] = false;

    //                if (i == 30 && j == 10 && k == 0)
    //                    Debug.Log(string.Format("({0},{1},{2}): {3};{4}", i, j, k, velocity[i, j, k], temperature[i, j, k]));
    //            }
    //        }
    //    }
    //}

    public int IDX(int x, int y, int z)
    {
        return (x * gd.gridSizeY * gd.gridSizeZ + y * gd.gridSizeZ + z);
    }

    public Quaternion velToRot(Vector3 v)
    {
        float a1, a2;
        a1 = Mathf.Atan2(Mathf.Sqrt(v.x * v.x + v.z * v.z), v.y);
        a2 = Mathf.Atan2(-v.x, -v.z);
        return Quaternion.Euler(-a1 * Mathf.Rad2Deg, a2 * Mathf.Rad2Deg, 0);
    }

    public Vector3 getVelocity(Vector3 v)
    {
        return getVelocity(v.x, v.y, v.z);
    }
    public Vector3 getVelocity(float px, float py, float pz)
    {
        //no_stick(ref px, ref py, ref pz);
        //Vector3 gv = W2G * new Vector3(px, py, pz);
        //px = gv.x;
        //py = gv.y;
        //pz = gv.z;

        int x, y, z;
        float xr, yr, zr;
        x = Mathf.FloorToInt(px - 0.5f);
        y = Mathf.FloorToInt(py - 0.5f);
        z = Mathf.FloorToInt(pz - 0.5f);
        xr = px - 0.5f - x;
        yr = py - 0.5f - y;
        zr = pz - 0.5f - z;

        if (px < 0.5f)
        {
            x = 0;
            xr = 0;
        }
        if (py < 0.5f)
        {
            y = 0;
            yr = 0;
        }
        if (pz < 0.5f)
        {
            z = 0;
            zr = 0;
        }
        if (px >= gd.gridSizeX - 1.5f)
        {
            x = gd.gridSizeX - 2;
            xr = 1;
        }
        if (py >= gd.gridSizeY - 1.5f)
        {
            y = gd.gridSizeY - 2;
            yr = 1;
        }
        if (pz >= gd.gridSizeZ - 1.5f)
        {
            z = gd.gridSizeZ - 2;
            zr = 1;
        }

        float[] shape = new float[8];
        for (int j = 0; j < 8; j++)
        {
            shape[j] = 1;
            shape[j] *= (j % 2) == 1 ? xr : (1 - xr);
            shape[j] *= (j / 2 % 2) == 1 ? yr : (1 - yr);
            shape[j] *= (j / 4 % 2) == 1 ? zr : (1 - zr);
        }

        Vector3 v = Vector3.zero;
        for (int j = 0; j < 8; j++)
        {
            Vector2Int idx = convert3DIndex(x + (j % 2), y + (j / 2 % 2), z + (j / 4 % 2));
            //v += shape[j] * velocity[x + (j % 2), y + (j / 2 % 2), z + (j / 4 % 2)];
            v.x += shape[j] * velocity[idx.y];
            v.y += shape[j] * velocity[idx.y + 1];
            v.z += shape[j] * velocity[idx.y + 2];
        }

        //gv = G2W * new Vector3(px, py, pz);
        //px = gv.x;
        //py = gv.y;
        //pz = gv.z;

        return v;
    }

    public Vector3 getVelocityAndTemp(Vector3 v, ref float temp)
    {
        return getVelocityAndTemp(v.x, v.y, v.z, ref temp);
    }
    public Vector3 getVelocityAndTemp(float px, float py, float pz, ref float temp)
    {
        //no_stick(ref px, ref py, ref pz);
        //Vector3 gv = W2G * new Vector3(px, py, pz);
        //px = gv.x;
        //py = gv.y;
        //pz = gv.z;

        int x, y, z;
        float xr, yr, zr;
        x = Mathf.FloorToInt(px - 0.5f);
        y = Mathf.FloorToInt(py - 0.5f);
        z = Mathf.FloorToInt(pz - 0.5f);
        xr = px - 0.5f - x;
        yr = py - 0.5f - y;
        zr = pz - 0.5f - z;

        if (px < 0.5f)
        {
            x = 0;
            xr = 0;
        }
        if (py < 0.5f)
        {
            y = 0;
            yr = 0;
        }
        if (pz < 0.5f)
        {
            z = 0;
            zr = 0;
        }
        if (px >= gd.gridSizeX - 1.5f)
        {
            x = gd.gridSizeX - 2;
            xr = 1;
        }
        if (py >= gd.gridSizeY - 1.5f)
        {
            y = gd.gridSizeY - 2;
            yr = 1;
        }
        if (pz >= gd.gridSizeZ - 1.5f)
        {
            z = gd.gridSizeZ - 2;
            zr = 1;
        }

        float[] shape = new float[8];
        for (int j = 0; j < 8; j++)
        {
            shape[j] = 1;
            shape[j] *= (j % 2) == 1 ? xr : (1 - xr);
            shape[j] *= (j / 2 % 2) == 1 ? yr : (1 - yr);
            shape[j] *= (j / 4 % 2) == 1 ? zr : (1 - zr);
        }

        Vector3 v = Vector3.zero;
        temp = 0;

        for (int j = 0; j < 8; j++)
        {
            int idx = IDX(x + (j % 2), y + (j / 2 % 2), z + (j / 4 % 2));
            //v += shape[j] * velocity[x + (j % 2), y + (j / 2 % 2), z + (j / 4 % 2)];
            v.x += shape[j] * velocity[3 * idx];
            v.y += shape[j] * velocity[3 * idx +1];
            v.z += shape[j] * velocity[3 * idx +2];
            //temp += shape[j] * temperature[x + (j % 2), y + (j / 2 % 2), z + (j / 4 % 2)];
            temp += shape[j] * temperature[idx];
        }

        //gv = G2W * new Vector3(px, py, pz);
        //px = gv.x;
        //py = gv.y;
        //pz = gv.z;

        return v;
    }

    public void no_stick(ref Vector3 v, float margin = 0.05f)
    {

        if (v.x < 0.5f) v.x = 0.5f + margin;
        if (v.y < 0.5f) v.y = 0.5f + margin;
        if (v.z < 0.5f) v.z = 0.5f + margin;

        if (v.x > gd.gridSizeX - 0.5f) v.x = gd.gridSizeX - 0.5f - margin;
        if (v.y > gd.gridSizeY - 0.5f) v.y = gd.gridSizeY - 0.5f - margin;
        if (v.z > gd.gridSizeZ - 0.5f) v.z = gd.gridSizeZ - 0.5f - margin;
    }

    public void no_stick(ref float x, ref float y, ref float z)
    {
        const float no_stick = 0.1f;

        if (x < 0) x = 0 + no_stick;
        if (y < 0) y = 0 + no_stick;
        if (z < 0) z = 0 + no_stick;

        if (x > 60) x = 60 - no_stick;
        if (y > 27) y = 27 - no_stick;
        if (z > 60) z = 60 - no_stick;
    }


    public Color tempToColor(float temp)
    {
        float h = 1.0f - Mathf.Min(temp, 1.0f);

        if (h < 0.4f)
        {
            return new Color(0, 0, 1.0f, 0.2f);
        }
        else if (h < 0.6f)
        {
            return new Color(0, 1.0f / 0.2f * (h - 0.4f), 1.0f, 0.2f);
        }
        else if (h < 0.75f)
        {
            return new Color(0, 1.0f, 1 - 1.0f / 0.15f * (h - 0.6f), 0.2f);
        }
        else if (h < 0.8f)
        {
            return new Color(1.0f / 0.05f * (h - 0.75f), 1.0f, 0, 0.2f);
        }
        else if (h <= 0.85f)
        {
            return new Color(1.0f, 1 - 1 / 0.05f * (h - 0.8f), 0, 0.2f);
        }
        else if (h <= 1)
        {
            return new Color(1.0f, 0, 0, 0.2f);
        }

        return new Color(0, 0, 0, 0);
    }

    Vector2Int convert3DIndex(int x, int y, int z)
    {
        int temperature_idx = x * gd.gridSizeY * gd.gridSizeZ + y * gd.gridSizeZ + z;
        int velocity_idx = temperature_idx * 3;
        return new Vector2Int(temperature_idx, velocity_idx);
    }
}
