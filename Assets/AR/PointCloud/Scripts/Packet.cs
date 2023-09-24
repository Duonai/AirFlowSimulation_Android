using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Packet
{
    public int buffer_length = 1440;
    public int header_length = sizeof(int);

    private byte[] m_buffer;
    private int m_position = 0;
    private uint m_body_length = 0;

    public int get_buffer_length()
    {
        return buffer_length;
    }

    public void set_buffer_length(int length)
    {
        buffer_length = length;
    }

    public void init(int size = 0)
    {
        m_position = header_length;
        m_body_length = 0;
        if (size != 0)
            buffer_length = size;
        m_buffer = new byte[buffer_length];
        //Array.Clear(m_buffer, 0, buffer_length);
    }

    public void init(byte[] buffer, int size = 0)
    {
        m_position = header_length;
        m_body_length = 0;
        if (size != 0)
            buffer_length = size;

        m_buffer = buffer;
        decode_body_length();
    }

    void record_body_length()
    {
        byte[] header = BitConverter.GetBytes(m_body_length);
        Array.Reverse(header); //bigEndian

        m_buffer[0] = header[0];
        m_buffer[1] = header[1];
        m_buffer[2] = header[2];
        m_buffer[3] = header[3];
        //Debug.Log(header.Length);
    }

    void decode_body_length()
    {
        List<byte> headerList = new List<byte>();

        for (int i = 0; i <= 3; i++)
            headerList.Add(m_buffer[i]);

        uint value = 0;

        byte[] data = headerList.ToArray();
        Array.Reverse(data); //bigEndian
        value = BitConverter.ToUInt32(data, 0);

        m_body_length = value;
    }

    public byte[] get_buffer()
    {
        return m_buffer;
    }

    public byte[] get_body()
    {
        List<byte> bodyList = new List<byte>();

        for (int i = header_length; i <= (int)m_body_length + header_length - 1; i++)
            bodyList.Add(m_buffer[i]);

        byte[] body = bodyList.ToArray();
        return body;
    }

    public int get_position()
    {
        return m_position;
    }

    public int get_total_length()
    {
        return (int)m_body_length + header_length;
    }

    public int get_body_length()
    {
        return (int)m_body_length;
    }

    byte[] read_buffer(int size)
    {
        byte[] data = new byte[size];
        //Array.Clear(data, 0, data.Length);


        Buffer.BlockCopy(m_buffer, m_position, data, 0, size);
        //for (int i = 0; i <= size - 1; i++)
        //    data[i] = m_buffer[m_position + i];

        m_position += size;
        return data;
    }

    void write_buffer(byte[] data, int size)
    {
        Buffer.BlockCopy(data, 0, m_buffer, m_position, size);
        //for (int i = 0; i < size; i++)
        //    m_buffer[m_position + i] = data[i];

        m_position += size;
        m_body_length += (uint)size;
        //Debug.Log("m_body_length" + m_body_length);
        record_body_length();
    }

    public byte[] pop_byte()
    {
        byte[] data = read_buffer(1);

        return data;
    }

    public byte[] pop_byte_array(int size)
    {
        byte[] data = read_buffer(size);

        return data;
    }

    public bool[] pop_bool_array(int size)
    {
        byte[] data = read_buffer(size);
        bool[] result = new bool[size];

        for (int i = 0; i < data.Length; i++)
        {
            if (data[i] != 0x00)
                result[i] = true;
            else
                result[i] = false;
        }

        return result;
    }

    public bool pop_bool()
    {
        byte[] data = pop_byte();

        return data[0] != 0x00;
    }

    public ushort pop_UInt16()
    {
        byte[] temp = read_buffer(sizeof(short));

        ushort value = 0;

        value = BitConverter.ToUInt16(temp, 0);

        return value;
    }

    //public int pop_int32()
    //{
    //    byte[] temp = read_buffer(sizeof(int));

    //    int value = 0;

    //    value = BitConverter.ToInt32(temp, 0);

    //    return value;
    //}

    //public long pop_int64()
    //{
    //    byte[] temp = read_buffer(sizeof(long));

    //    long value = 0;

    //    value = BitConverter.ToInt64(temp, 0);

    //    return value;
    //}

    public float pop_single()
    {
        byte[] temp = read_buffer(sizeof(float));

        float value = 0;

        value = BitConverter.ToSingle(temp, 0);

        return value;
    }

    //public byte[] pop_byte_array()
    //{
    //    long size = pop_int64();
    //    byte[] data = read_buffer((int)size);

    //    return data;
    //}

    public void push_byte(byte data)
    {
        byte[] temp = new byte[] { data };

        write_buffer(temp, 1);
    }

    public void push_byte_array(byte[] data)
    {
        write_buffer(data, data.Length);
    }

    public void push_bool_array(bool[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            if (data[i])
            {
                byte[] temp = new byte[] { 0x01 };
                write_buffer(temp, 1);
            }
            else
            {
                byte[] temp = new byte[] { 0x00 };
                write_buffer(temp, 1);
            }
        }
    }

    public void push_bool(bool data)
    {
        if (data)
        {
            byte[] temp = new byte[] { 0x01 };
            write_buffer(temp, 1);
        }
        else
        {
            byte[] temp = new byte[] { 0x00 };
            write_buffer(temp, 1);
        }
    }

    public void push_UInt16(ushort data)
    {
        byte[] temp = BitConverter.GetBytes(data);

        write_buffer(temp, sizeof(ushort));
    }

    public void push_int32(int data)
    {
        byte[] temp = BitConverter.GetBytes(data);

        write_buffer(temp, sizeof(int));
    }

    public void push_UInt32(uint data)
    {
        byte[] temp = BitConverter.GetBytes(data);

        write_buffer(temp, sizeof(uint));
    }

    public void push_int64(long data)
    {
        byte[] temp = BitConverter.GetBytes(data);

        write_buffer(temp, sizeof(long));
    }

    public void push_single(float data)
    {
        byte[] temp = BitConverter.GetBytes(data);

        write_buffer(temp, sizeof(float));
    }

    public void push_byte_array(byte[] data, int size)
    {
        push_int64((long)size);
        write_buffer(data, size);
    }

    public void debugTemp()
    {
        byte[] temp = BitConverter.GetBytes(50.234f);

        float value = 0;

        value = BitConverter.ToSingle(temp, 0);
        Debug.Log(value);
    }
}