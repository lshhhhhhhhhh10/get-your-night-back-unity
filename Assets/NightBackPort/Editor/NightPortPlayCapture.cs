using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightBackPort.Editor
{
    public static class NightPortPlayCapture
    {
        [Serializable] public class InputSmoke
        {
            public bool won, lost; public string winStatus, loseStatus, scope; public float winSeconds, loseSeconds, recognition; public int rescued, failedRescues;
        }
        static bool pending, probe;
        static double startTime;
        const string PendingKey = "NightBackPort.Capture.Pending";
        const string ProbeKey = "NightBackPort.Capture.Probe";
        // Entering Play Mode can reload this assembly. SessionState preserves only this
        // editor task's pending request; it does not change project or user settings.
        [InitializeOnLoadMethod]
        static void RestorePendingCapture()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;
            pending = true; probe = SessionState.GetBool(ProbeKey, false); startTime = EditorApplication.timeSinceStartup;
            EditorApplication.update -= WaitForPlay; EditorApplication.update += WaitForPlay;
        }
        public static string ValidateInputs()
        {
            var result = new InputSmoke { scope = "60Hz normal-input simulation of the exact capture pilots; no position/AI/device overrides." };
            var win = new NightGame(0, 90); win.seed = 4; win.Start(); var pilot = new NightPlaythroughPilot();
            for (int i = 0; i < 10800 && win.status == "playing"; i++) win.Tick(1f / 60f, pilot.Next(win, 1f / 60f));
            result.won = win.status == "won"; result.winStatus = win.status; result.winSeconds = win.realTime; result.rescued = win.metrics.rescued; result.failedRescues = win.metrics.failedRescues;
            var lose = new NightGame(0, 180); lose.seed = 4; lose.Start(); var exposed = new NightCaptureCaughtPilot();
            for (int i = 0; i < 4800 && lose.status == "playing"; i++) lose.Tick(1f / 60f, exposed.Next(lose, 1f / 60f));
            result.lost = lose.status == "lost"; result.loseStatus = lose.status; result.loseSeconds = lose.realTime; result.recognition = lose.parent.recognition;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence/capture-input-smoke.json")); Directory.CreateDirectory(Path.GetDirectoryName(path));
            string json = JsonUtility.ToJson(result, true); File.WriteAllText(path, json); return json;
        }
        public static string Start(bool isProbe = false)
        {
            var existing = UnityEngine.Object.FindObjectOfType<NightPortCaptureDriver>();
            if (existing != null && existing.recording) return Poll();
            probe = isProbe; pending = true; startTime = EditorApplication.timeSinceStartup;
            SessionState.SetBool(PendingKey, true); SessionState.SetBool(ProbeKey, probe);
            EditorApplication.update -= WaitForPlay; EditorApplication.update += WaitForPlay;
            if (!EditorApplication.isPlaying) EditorApplication.isPlaying = true;
            return "{\"status\":\"queued\",\"probe\":" + (probe ? "true" : "false") + "}";
        }
        static void WaitForPlay()
        {
            if (!pending) return;
            if (EditorApplication.timeSinceStartup - startTime > 30) { pending = false; SessionState.SetBool(PendingKey, false); EditorApplication.update -= WaitForPlay; Debug.LogError("Night capture could not find a running NightApp within 30 seconds."); return; }
            if (!EditorApplication.isPlaying || NightApp.Instance == null) return;
            pending = false; SessionState.SetBool(PendingKey, false); EditorApplication.update -= WaitForPlay;
            var driver = NightApp.Instance.GetComponent<NightPortCaptureDriver>(); if (driver == null) driver = NightApp.Instance.gameObject.AddComponent<NightPortCaptureDriver>();
            driver.app = NightApp.Instance; driver.Begin(probe);
        }
        public static string Poll()
        {
            var driver = UnityEngine.Object.FindObjectOfType<NightPortCaptureDriver>();
            if (driver != null)
            {
                if (driver.recording) return "{\"status\":\"recording\",\"stage\":\"" + driver.stage + "\",\"frames\":" + driver.frame + "}";
                if (driver.report != null) return JsonUtility.ToJson(driver.report, true);
            }
            if (pending) return "{\"status\":\"waiting-for-play\"}";
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence", probe ? "native-capture-probe.json" : "native-capture-report.json"));
            return File.Exists(path) ? File.ReadAllText(path) : "{\"status\":\"idle\"}";
        }
    }
}
