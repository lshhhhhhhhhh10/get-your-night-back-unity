using System;
using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

public static class WhiteboxBootstrap
{
    static AddRequest request;
    static double deadline;
    public static void InstallBridge()
    {
        request = Client.Add("cn.tuanjie.codely.bridge@1.0.81");
        deadline = EditorApplication.timeSinceStartup + 180;
        EditorApplication.update += Poll;
    }
    static void Poll()
    {
        if (!request.IsCompleted)
        {
            if (EditorApplication.timeSinceStartup > deadline)
            {
                Debug.LogError("WHITEBOX_BRIDGE_TIMEOUT");
                EditorApplication.Exit(2);
            }
            return;
        }
        EditorApplication.update -= Poll;
        if (request.Status == StatusCode.Success)
        {
            Debug.Log("WHITEBOX_BRIDGE_READY " + request.Result.name + " " + request.Result.version);
            EditorApplication.Exit(0);
        }
        else
        {
            Debug.LogError("WHITEBOX_BRIDGE_ERROR " + request.Error.message);
            EditorApplication.Exit(1);
        }
    }
}
