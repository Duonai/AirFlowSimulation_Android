using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugPrinter : MonoBehaviour
{
    //#if !UNITY_EDITOR
    static string myLog = "";
    private string output;
    private string stack;

    void OnEnable()
    {
        Application.logMessageReceived += Log;
    }

    void OnDisable()
    {
        Application.logMessageReceived -= Log;
    }

    public void Log(string logString, string stackTrace, LogType type)
    {
        output = logString;
        stack = stackTrace;

        myLog += $"\n [{System.DateTime.Now.ToString("HH:mm:ss")}] [{type}] {output}";
        
        if(type == LogType.Error || type == LogType.Exception)
        {
            myLog += "\n" + stack;
        }

        if (myLog.Length > 5000)
        {
            string newLog = "";
            for (int i = 0; i < 4000; i++)
            {
                newLog += myLog[1000+i];
            }
            myLog = newLog;
        }
    }

    void OnGUI()
    {
        //if (!Application.isEditor) //Do not display in editor ( or you can use the UNITY_EDITOR macro to also disable the rest)
        {
            GUIStyle style = new GUIStyle(GUI.skin.textArea);
            style.alignment = TextAnchor.LowerLeft;
            style.fontSize = 30;

            GUI.backgroundColor = new Color(0, 0, 0, 0);
            myLog = GUI.TextArea(new Rect(Screen.width / 2, 100, Screen.width / 2, Screen.height / 3), myLog, style);
        }
    }
    //#endif
}
