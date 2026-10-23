#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Unity.Collections;
using UnityEditor;
using UnityEditor.Media;
using UnityEngine;
using UnityEngine.UIElements;
using NightBackPort.Editor;
using Cursor = UnityEngine.Cursor;

namespace NightBackPort
{
    [Serializable] public class NightCaptureCheck { public string name, detail; public bool passed; }
    [Serializable] public class NightCaptureResult
    {
        public string file, wavFile, method, timestamp, status, error;
        public bool completed, probe, won, lost, replayed, returnedToMenu;
        public int width = 1920, height = 1080, fps = 30, frames, sampleRate, channels;
        public long audioSamples; public float seconds, audioSeconds, audioPeak, audioRms, winSeconds, loseSeconds;
        public List<string> screenshots = new List<string>(); public List<NightCaptureCheck> checks = new List<NightCaptureCheck>();
    }

    // 仅在编辑器编译。采集真实 Game View、UI Toolkit 与 Unity 主混音输出。
    public sealed class NightPortCaptureDriver : MonoBehaviour
    {
        public NightApp app;
        public bool recording;
        public int frame, stageFrame;
        public string stage = "idle", status = "Ready", error;
        public NightCaptureResult report;
        const int Fps = 30;
        const float Step = 1f / 60f;
        MediaEncoder encoder;
        BinaryWriter wav;
        bool audioStarted, probe;
        int previousCaptureRate, previousTargetRate;
        long sampleCount;
        double squaredAudio;
        float peak;
        string outputDirectory, videoPath, wavPath;
        NightPlaythroughPilot pilot;
        NightCaptureCaughtPilot caughtPilot;
        readonly HashSet<string> captured = new HashSet<string>();
        GUIStyle caption;

        public void Begin(bool isProbe)
        {
            if (recording) throw new InvalidOperationException("Capture already running");
            if (!Application.isPlaying || app == null) throw new InvalidOperationException("The native NightApp must be running in Play Mode");
            probe = isProbe; recording = true; error = null; frame = 0; captured.Clear();
            previousCaptureRate = Time.captureFramerate; previousTargetRate = Application.targetFrameRate;
            report = new NightCaptureResult { probe = probe, timestamp = DateTime.Now.ToString("o"), status = "starting", method = "Unity Play Mode actual 1920x1080 Game View, normal NightInput pilot, fixed seed=4 disclosed; no teleport/door/device/AI state overrides. Unity MediaEncoder H264 + AudioRenderer native main-mix audio. This is automated input, not a manual playthrough." };
            StartCoroutine(Guard(Record()));
        }
        IEnumerator Guard(IEnumerator routine)
        {
            while (true)
            {
                bool next;
                try { next = routine.MoveNext(); }
                catch (Exception exception) { Fail(exception); yield break; }
                if (!next) yield break;
                yield return routine.Current;
            }
        }
        IEnumerator Record()
        {
            app.verificationMode = true; app.controller.Stop();
            yield return new WaitForEndOfFrame();
            if (Screen.width != 1920 || Screen.height != 1080) throw new Exception("Game View must be 1920x1080; actual=" + Screen.width + "x" + Screen.height);
            outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence")); Directory.CreateDirectory(outputDirectory);
            videoPath = Path.Combine(outputDirectory, probe ? "native-capture-probe.mp4" : "NightBack-Native-Playthrough.mp4");
            wavPath = Path.Combine(outputDirectory, probe ? "native-capture-probe.wav" : "NightBack-Native-Playthrough.wav");
            report.file = videoPath; report.wavFile = wavPath; report.sampleRate = AudioSettings.outputSampleRate; report.channels = AudioChannels(AudioSettings.speakerMode);
            Time.captureFramerate = Fps; Application.targetFrameRate = Fps;
            var video = new VideoTrackEncoderAttributes(new H264EncoderAttributes { gopSize = 30, numConsecutiveBFrames = 2, profile = VideoEncodingProfile.H264High })
            { frameRate = new MediaRational(Fps), width = 1920, height = 1080, targetBitRate = 14000000 };
            var audio = new AudioTrackAttributes { sampleRate = new MediaRational(report.sampleRate), channelCount = (ushort)report.channels, language = "" };
            encoder = new MediaEncoder(videoPath, video, audio);
            wav = new BinaryWriter(File.Open(wavPath, FileMode.Create, FileAccess.ReadWrite)); WriteWaveHeader(0);
            sampleCount = 0; squaredAudio = 0; peak = 0;
            audioStarted = AudioRenderer.Start(); if (!audioStarted) throw new Exception("Unity AudioRenderer could not enter recording mode; no silent-video fallback will be reported as audio capture.");
            ShowMenu(1); EnterStage("title");
            while (stage != "complete")
            {
                Advance();
                yield return new WaitForEndOfFrame();
                CaptureFrame(); frame++; stageFrame++;
                status = "Recording " + stage + " / " + frame + " frames"; report.status = status;
                if (probe && frame >= 120) EnterStage("complete");
            }
            Complete();
        }
        void EnterStage(string value) { stage = value; stageFrame = 0; }
        void ShowMenu(int departure)
        {
            app.Menu(); app.departureChoice = departure; app.Preview(0); app.page = "menu"; app.paused = true; app.ui.Show("menu"); app.UpdateCamera(true);
        }
        void StartNormalNight(int departure)
        {
            app.departureChoice = departure; app.StartNight(0); app.game.seed = 4;
            app.overview = false; app.thirdPerson = false; app.world.SetOverview(false); app.paused = false;
            app.game.ReleaseInteractionInputs(); app.game.Tick(Step, new NightInput()); app.audioSystem.Consume(app.game);
            app.UpdateCamera(true); Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        }
        void Advance()
        {
            if (app == null) throw new Exception("NightApp was destroyed during capture");
            if (stage == "title")
            {
                if (stageFrame == 1) AddCheck("title page renders", app.page == "menu" && HasLabel("Get Your"));
                if (stageFrame >= (probe ? 30 : 90))
                {
                    StartNormalNight(1); app.overview = true; app.world.SetOverview(true); app.UpdateCamera(true); pilot = new NightPlaythroughPilot(); EnterStage("overview");
                }
            }
            else if (stage == "overview")
            {
                Drive(false, false);
                if (stageFrame >= 60) { app.overview = false; app.world.SetOverview(false); app.UpdateCamera(true); EnterStage("win_play"); }
            }
            else if (stage == "win_play")
            {
                Drive(true, false);
                if (app.game.status == "won")
                {
                    report.won = true; report.winSeconds = app.game.realTime;
                    AddCheck("real device return wins", app.game.hasDevice && app.game.player.x <= 6 && app.game.player.z >= 11 && app.game.metrics.score >= 40);
                    ShowResult(); EnterStage("win_result");
                }
                else if (app.game.status == "lost") throw new Exception("Normal-input winning route was caught; not recording a fake win.");
                else if (stageFrame > 6000) throw new Exception("Winning input route exceeded capture guard: " + app.game.InteractionHint());
            }
            else if (stage == "win_result")
            {
                if (stageFrame == 5) AddCheck("win page exposes replay", HasLabel("CLEARED") && HasButton("再来一次", "Try again"));
                if (stageFrame >= 120) { ShowMenu(2); EnterStage("retry_menu"); }
            }
            else if (stage == "retry_menu")
            {
                if (stageFrame >= 60) { StartNormalNight(2); caughtPilot = new NightCaptureCaughtPilot(); report.replayed = true; EnterStage("fail_play"); }
            }
            else if (stage == "fail_play")
            {
                Drive(true, true);
                if (app.game.status == "lost")
                {
                    report.lost = true; report.loseSeconds = app.game.realTime;
                    AddCheck("normal exposure produces real loss", app.game.parent.recognition >= NightGame.RecognitionTime);
                    ShowResult(); EnterStage("fail_result");
                }
                else if (stageFrame > 2400) throw new Exception("Exposure route did not trigger a real loss within guard; no forced state used.");
            }
            else if (stage == "fail_result")
            {
                if (stageFrame == 5) AddCheck("game over page exposes replay", HasLabel("FAILED") && HasButton("再来一次", "Try again"));
                if (stageFrame >= 120) { StartNormalNight(0); EnterStage("retry_start"); }
            }
            else if (stage == "retry_start")
            {
                Drive(false, false);
                if (stageFrame == 5) AddCheck("retry starts an intact new night", app.game.status == "playing" && !app.game.hasDevice && app.game.parent.state == "sleep");
                if (stageFrame >= 45) { app.Menu(); app.UpdateCamera(true); EnterStage("final_menu"); }
            }
            else if (stage == "final_menu")
            {
                if (stageFrame == 5) { report.returnedToMenu = app.page == "menu" && HasLabel("Get Your"); AddCheck("returned to title", report.returnedToMenu); }
                if (stageFrame >= 90) EnterStage("complete");
            }
            app.ui.Refresh();
        }
        void Drive(bool usePilot, bool failure)
        {
            if (app.game.status != "playing") return;
            for (int i = 0; i < 2 && app.game.status == "playing"; i++)
            {
                var input = usePilot ? failure ? caughtPilot.Next(app.game, Step) : pilot.Next(app.game, Step) : new NightInput();
                app.game.Tick(Step, input);
            }
            app.world.Sync(app.game); app.audioSystem.Consume(app.game);
            if (app.game.velocity.sqrMagnitude > .005f) app.yaw = app.game.heading * Mathf.Rad2Deg;
            if (failure && app.game.parent.state == "checking" && !app.game.Occluded(app.game.player, app.game.parent.position, app.game.crouched))
                app.yaw = Mathf.Atan2(app.game.parent.position.x - app.game.player.x, app.game.parent.position.z - app.game.player.z) * Mathf.Rad2Deg;
            app.pitch = 4; app.UpdateCamera(false);
        }
        void ShowResult()
        {
            app.page = "result"; app.paused = false; app.ReleaseInputs(); app.ui.Show("result"); app.UpdateCamera(false); Cursor.lockState = CursorLockMode.None; Cursor.visible = false;
        }
        bool HasLabel(string text)
        {
            if (app.ui == null || app.ui.root == null) return false;
            foreach (var label in app.ui.root.Query<Label>().ToList()) if (label.text != null && label.text.Contains(text)) return true; return false;
        }
        bool HasButton(string first, string second)
        {
            if (app.ui == null || app.ui.root == null) return false;
            foreach (var button in app.ui.root.Query<Button>().ToList()) if (button.text == first || button.text == second) return true; return false;
        }
        void AddCheck(string name, bool passed, string detail = "") { report.checks.Add(new NightCaptureCheck { name = name, passed = passed, detail = detail }); }
        void CaptureFrame()
        {
            var texture = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                if (texture.width != 1920 || texture.height != 1080) throw new Exception("Game View capture resolution changed");
                if (!encoder.AddFrame(texture)) throw new Exception("Video encoder rejected frame " + frame);
                string shot = null;
                if (stageFrame == 15 && (stage == "title" || stage == "overview" || stage == "win_result" || stage == "fail_result" || stage == "final_menu")) shot = stage;
                if (stage == "win_play" && app.game.mode != null && app.game.mode.elapsed > (app.game.mode.type == "catch" ? .75f : .35f) && (app.game.mode.type == "door" || app.game.mode.type == "lockpick" || app.game.mode.type == "catch" || app.game.mode.type == "search")) shot = app.game.mode.type;
                if (stage == "fail_play" && app.game.parent.recognition > 0) shot = "recognition";
                if (shot != null && captured.Add(shot))
                {
                    string path = Path.Combine(outputDirectory, (probe ? "probe-" : "native-") + shot + ".png"); File.WriteAllBytes(path, texture.EncodeToPNG()); report.screenshots.Add(path);
                    var colors = texture.GetPixels32(); int min = 255, max = 0; for (int i = 0; i < colors.Length; i += 997) { var c = colors[i]; int luminance = (c.r + c.g + c.b) / 3; min = Mathf.Min(min, luminance); max = Mathf.Max(max, luminance); }
                    AddCheck("rendered frame " + shot, max - min > 12 && max > 20, "range=" + min + ".." + max);
                }
            }
            finally { Destroy(texture); }
            int samples = AudioRenderer.GetSampleCountForCaptureFrame();
            if (samples <= 0) return;
            using (var buffer = new NativeArray<float>(samples * report.channels, Allocator.Temp))
            {
                if (!AudioRenderer.Render(buffer)) throw new Exception("Unity native audio render failed at frame " + frame);
                if (!encoder.AddSamples(buffer)) throw new Exception("MediaEncoder rejected native audio at frame " + frame);
                for (int i = 0; i < buffer.Length; i++)
                {
                    float value = buffer[i]; if (float.IsNaN(value) || float.IsInfinity(value)) throw new Exception("Non-finite native audio sample");
                    peak = Mathf.Max(peak, Mathf.Abs(value)); squaredAudio += value * value; wav.Write((short)Mathf.RoundToInt(Mathf.Clamp(value, -1, 1) * 32767));
                }
                sampleCount += buffer.Length;
            }
        }
        static int AudioChannels(AudioSpeakerMode mode)
        {
            switch (mode) { case AudioSpeakerMode.Mono: return 1; case AudioSpeakerMode.Quad: return 4; case AudioSpeakerMode.Surround: return 5; case AudioSpeakerMode.Mode5point1: return 6; case AudioSpeakerMode.Mode7point1: return 8; default: return 2; }
        }
        void WriteWaveHeader(long dataBytes)
        {
            wav.Seek(0, SeekOrigin.Begin); wav.Write(System.Text.Encoding.ASCII.GetBytes("RIFF")); wav.Write((uint)(36 + dataBytes)); wav.Write(System.Text.Encoding.ASCII.GetBytes("WAVEfmt "));
            wav.Write(16u); wav.Write((ushort)1); wav.Write((ushort)report.channels); wav.Write(report.sampleRate); wav.Write(report.sampleRate * report.channels * 2); wav.Write((ushort)(report.channels * 2)); wav.Write((ushort)16);
            wav.Write(System.Text.Encoding.ASCII.GetBytes("data")); wav.Write((uint)dataBytes);
        }
        void Complete()
        {
            report.frames = frame; report.seconds = frame / (float)Fps; report.audioSamples = sampleCount; report.audioSeconds = sampleCount / (float)(report.channels * report.sampleRate); report.audioPeak = peak; report.audioRms = sampleCount > 0 ? (float)Math.Sqrt(squaredAudio / sampleCount) : 0;
            AddCheck("native audio captured", sampleCount > 0 && peak > .00001f, "peak=" + peak + ", samples=" + sampleCount);
            AddCheck("audio and video duration agree", Mathf.Abs(report.audioSeconds - report.seconds) < .1f, report.audioSeconds + " vs " + report.seconds);
            if (!probe) AddCheck("full native win lose replay flow", report.won && report.lost && report.replayed && report.returnedToMenu);
            report.completed = report.checks.TrueForAll(check => check.passed); report.status = report.completed ? "complete" : "validation-failed";
            Cleanup(); status = report.status; Persist(); Debug.Log("NIGHT_NATIVE_CAPTURE " + status + " " + videoPath);
        }
        void Fail(Exception exception)
        {
            error = exception.ToString(); status = "failed";
            if (report != null) { report.error = error; report.status = status; report.completed = false; report.frames = frame; report.seconds = frame / (float)Fps; }
            Cleanup(); Persist(); Debug.LogError("NIGHT_NATIVE_CAPTURE_FAILED " + error);
        }
        void Persist()
        {
            if (string.IsNullOrEmpty(outputDirectory)) outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence"));
            Directory.CreateDirectory(outputDirectory); File.WriteAllText(Path.Combine(outputDirectory, probe ? "native-capture-probe.json" : "native-capture-report.json"), JsonUtility.ToJson(report, true));
        }
        void Cleanup()
        {
            if (audioStarted) { AudioRenderer.Stop(); audioStarted = false; }
            if (encoder != null) { encoder.Dispose(); encoder = null; }
            if (wav != null) { WriteWaveHeader(sampleCount * 2); wav.Dispose(); wav = null; }
            Time.captureFramerate = previousCaptureRate; Application.targetFrameRate = previousTargetRate; recording = false;
            if (app != null) { app.verificationMode = false; app.controller.Stop(); app.audioSystem.StopAll(); app.SetCursor(); }
        }
        void OnDisable() { if (recording) { error = "Capture component disabled before completion"; if (report != null) { report.error = error; report.status = "interrupted"; } Cleanup(); Persist(); } }
        void OnGUI()
        {
            if (!recording) return;
            if (caption == null) { caption = new GUIStyle(GUI.skin.label) { fontSize = 15, alignment = TextAnchor.MiddleCenter }; caption.normal.textColor = new Color(.85f, .93f, 1); }
            GUI.color = new Color(.025f, .04f, .065f, .9f); GUI.DrawTexture(new Rect(0, Screen.height - 27, Screen.width, 27), Texture2D.whiteTexture); GUI.color = Color.white;
            GUI.Label(new Rect(10, Screen.height - 26, Screen.width - 20, 24), "NATIVE UNITY PLAY  |  PROGRAMMED INPUT  |  SEED 4  |  " + stage.ToUpperInvariant().Replace('_', ' ') + "  |  ACTUAL GAME + LIVE MIX", caption);
        }
    }

    // 故意暴露的回归路线：23:00从卧室正常开门走到走廊，面对正常起床的家长。
    public sealed class NightCaptureCaughtPilot
    {
        readonly NightPlaythroughPilot interactions = new NightPlaythroughPilot();
        readonly List<Vector3> path = new List<Vector3>(); float repath;
        public NightInput Next(NightGame game, float dt)
        {
            if (game.mode != null) { path.Clear(); return interactions.Next(game, dt); }
            var input = new NightInput { crouchPressed = game.crouched };
            Vector3 goal = new Vector3(3, 0, 9); repath -= dt;
            if (path.Count == 0 && NightGeometry.Distance(game.player, goal) < .16f) return input;
            if (path.Count == 0 || repath <= 0) { path.Clear(); path.AddRange(game.FindPath(game.player, goal, false, .2f)); repath = 2; }
            while (path.Count > 0 && NightGeometry.Distance(game.player, path[0]) < .15f) path.RemoveAt(0);
            if (path.Count == 0) return input;
            foreach (var door in game.doors) if (door.progress < .999f && NightGeometry.Distance(game.player, door.Position) < 1.16f && ((game.player.z - door.z) * (path[0].z - door.z) <= 0 || NightGeometry.Distance(path[0], door.Position) < .56f))
            { input.actionPressed = true; return input; }
            Vector3 delta = path[0] - game.player; input.move = Vector2.ClampMagnitude(new Vector2(delta.x, delta.z) * 5, 1); return input;
        }
    }
}
#endif
