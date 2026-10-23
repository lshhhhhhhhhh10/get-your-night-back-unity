using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightBackPort.Editor
{
    // App/API and native object checks. This is deliberately separate from input-device testing.
    public static class NightNativeUXVerification
    {
        [Serializable] public class Check { public string name, detail; public bool passed; }
        [Serializable] public class Report
        {
            public string timestamp, scope; public int passed, failed, englishSamples;
            public bool controllerConnected, dualSense, nativeControllerAvailable, filesRestored;
            public List<Check> checks = new List<Check>(); public List<string> englishLeaks = new List<string>();
            public List<string> errors = new List<string>();
        }
        static readonly string[] SaveFiles = { "save.json", "save.json.tmp", "save.json.backup", "tutorial-save.json", "tutorial-save.json.tmp", "tutorial-save.json.backup", "settings.json", "progress.json" };
        static readonly Regex Cjk = new Regex("[\\u3400-\\u4dbf\\u4e00-\\u9fff]", RegexOptions.Compiled);
        static void CheckThat(Report r, string name, bool passed, string detail = "")
        { r.checks.Add(new Check { name = name, passed = passed, detail = detail }); if (passed) r.passed++; else r.failed++; }
        static void InvokeUpdate(object owner) { owner.GetType().GetMethod("Update", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(owner, null); }
        static int ActiveSources(NightAudio audio)
        { int result = 0; foreach (var s in audio.GetComponentsInChildren<AudioSource>(true)) if (s.isPlaying) result++; return result; }

        public static string StartInputProbe(float seconds = 8)
        {
            if (!Application.isPlaying || NightApp.Instance == null) return "{\"status\":\"requires-play-mode\"}";
            var app = NightApp.Instance; var probe = app.GetComponent<NightNativeUXProbe>(); if (!probe) probe = app.gameObject.AddComponent<NightNativeUXProbe>();
            probe.app = app; probe.Begin(seconds); return "{\"status\":\"observing-real-frames\"}";
        }
        public static string PollInputProbe()
        {
            var probe = UnityEngine.Object.FindObjectOfType<NightNativeUXProbe>();
            if (probe && probe.running) return "{\"status\":\"observing\",\"frames\":" + probe.report.frames.Count + ",\"uiEvents\":" + probe.report.events.Count + "}";
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence/native-input-probe.json"));
            return File.Exists(path) ? File.ReadAllText(path) : "{\"status\":\"idle\"}";
        }
        [MenuItem("Night Back Port/Verify Native App UX")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            if (!Application.isPlaying || NightApp.Instance == null) throw new InvalidOperationException("Run native UX verification in Play Mode with NightApp loaded.");
            var app = NightApp.Instance;
            var capture = app.GetComponent<NightPortCaptureDriver>(); if (capture && capture.recording) throw new InvalidOperationException("Do not run UX checks during video recording.");
            var report = new Report { timestamp = DateTime.Now.ToString("o"), scope = "Actual native NightApp APIs, UI Toolkit text, audio resources, and 11 instantiated character rigs. Gameplay samples use disclosed normal-input pilots. Pause checks invoke the actual app/audio Update methods. This does not assert physical controller/keyboard delivery, subjective audio quality, or blind human play. Existing scoped save/settings bytes are restored in finally." };
            var before = new Dictionary<string, byte[]>(); foreach (string file in SaveFiles) { string path = Path.Combine(app.DataDirectory, file); before[path] = File.Exists(path) ? File.ReadAllBytes(path) : null; }
            var originalGame = app.game; var originalSettings = app.settings; bool originalEnglish = NightText.english;
            string originalPage = app.page; bool originalPaused = app.paused, originalOverview = app.overview, originalThirdPerson = app.thirdPerson, originalVerification = app.verificationMode;
            int originalSelected = app.selectedNight, originalDeparture = app.departureChoice, originalUnlocked = app.unlocked; float originalYaw = app.yaw, originalPitch = app.pitch;
            var hiddenFields = new Dictionary<FieldInfo, object>(); foreach (var f in typeof(NightApp).GetFields(BindingFlags.Instance | BindingFlags.NonPublic)) if (!f.IsInitOnly) hiddenFields[f] = f.GetValue(app);
            var cursorLock = UnityEngine.Cursor.lockState; bool cursorVisible = UnityEngine.Cursor.visible;
            Application.LogCallback log = (message, trace, type) => { if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert) report.errors.Add(message); };
            Application.logMessageReceived += log;
            try
            {
                app.verificationMode = true; app.ReleaseInputs(); app.settings = JsonUtility.FromJson<NightSettings>(JsonUtility.ToJson(originalSettings)); app.settings.haptics = false; app.settings.adaptive = false;
                CheckThat(report, "native app components exist", app.world && app.ui && app.audioSystem && app.viewCamera && app.GetComponent<UIDocument>());
                CheckAudioAndController(app, report);
                CheckPause(app, report);
                CheckSaveContinue(app, report);
                CheckTutorialSaveIsolation(app, report);
                CheckSkins(app, report);
                CheckEnglish(app, report);
                CheckThat(report, "native API checks produced no logged errors", report.errors.Count == 0, string.Join(" | ", report.errors.ToArray()));
            }
            catch (Exception e) { CheckThat(report, "verification completed without exception", false, e.ToString()); }
            finally
            {
                app.verificationMode = true; app.audioSystem.StopAll(); app.ReleaseInputs(); app.settings = originalSettings; NightText.english = originalEnglish; app.game = originalGame;
                app.selectedNight = originalSelected; app.departureChoice = originalDeparture; app.unlocked = originalUnlocked; app.page = originalPage; app.paused = originalPaused;
                app.overview = originalOverview; app.thirdPerson = originalThirdPerson; app.yaw = originalYaw; app.pitch = originalPitch;
                try
                {
                    app.world.Build(originalGame); app.world.SetSkins(originalSettings.playerSkin, originalSettings.parentSkin); app.world.Sync(originalGame); app.world.SetOverview(originalOverview); app.ui.Show(originalPage); app.UpdateCamera(true);
                    foreach (var pair in hiddenFields) pair.Key.SetValue(app, pair.Value);
                }
                catch (Exception e) { CheckThat(report, "original live view restored", false, e.ToString()); }
                foreach (var pair in before)
                {
                    try { if (pair.Value == null) { if (File.Exists(pair.Key)) File.Delete(pair.Key); } else { Directory.CreateDirectory(Path.GetDirectoryName(pair.Key)); File.WriteAllBytes(pair.Key, pair.Value); } }
                    catch (Exception e) { CheckThat(report, "scoped file restored: " + Path.GetFileName(pair.Key), false, e.ToString()); }
                }
                report.filesRestored = true;
                foreach (var pair in before)
                {
                    bool same = pair.Value == null ? !File.Exists(pair.Key) : File.Exists(pair.Key) && BytesEqual(pair.Value, File.ReadAllBytes(pair.Key));
                    report.filesRestored &= same;
                }
                CheckThat(report, "existing save/settings restored byte for byte", report.filesRestored);
                app.verificationMode = originalVerification; UnityEngine.Cursor.lockState = cursorLock; UnityEngine.Cursor.visible = cursorVisible; Application.logMessageReceived -= log;
            }
            string result = JsonUtility.ToJson(report, true); string output = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence/native-ux-checks.json")); Directory.CreateDirectory(Path.GetDirectoryName(output)); File.WriteAllText(output, result); return result;
        }
        static bool BytesEqual(byte[] a, byte[] b) { if (a.Length != b.Length) return false; for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false; return true; }
        static void CheckAudioAndController(NightApp app, Report report)
        {
            var clips = Resources.LoadAll<AudioClip>("NightBack/Audio"); bool healthy = clips.Length >= 20; var names = new HashSet<string>(); var detail = new List<string>();
            foreach (var clip in clips) { names.Add(clip.name); if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData(); bool valid = clip.samples > 0 && clip.channels > 0 && clip.frequency > 0 && clip.loadState != AudioDataLoadState.Failed; healthy &= valid; if (!valid) detail.Add(clip.name + ":" + clip.loadState); }
            foreach (string name in new[] { "door-handle", "door-hinge", "hinge-real", "door-bump", "door-latch", "drawer", "metal-drop", "snore-real", "washer-spin", "floor-creak-1" }) healthy &= names.Contains(name);
            CheckThat(report, "recorded audio clips resolve and contain samples", healthy && app.audioSystem.loadedSamples == clips.Length && app.audioSystem.sampleErrors.Count == 0, clips.Length + " imported clips; " + string.Join(", ", detail.ToArray()));
            int listeners = 0; foreach (var listener in UnityEngine.Object.FindObjectsOfType<AudioListener>()) if (listener.enabled && listener.gameObject.activeInHierarchy) listeners++;
            CheckThat(report, "one active player-ear AudioListener", listeners == 1, listeners.ToString());
            app.controller.Poll(); report.controllerConnected = app.controller.connected; report.dualSense = app.controller.dualSense;
            var native = typeof(NightController).GetField("nativeAvailable", BindingFlags.Instance | BindingFlags.NonPublic); report.nativeControllerAvailable = native != null && (bool)native.GetValue(app.controller);
            bool finite = true; foreach (float value in app.controller.values) finite &= !float.IsNaN(value) && !float.IsInfinity(value);
            CheckThat(report, "native controller query returns finite data without exception", finite && report.nativeControllerAvailable, "connected=" + report.controllerConnected + "; DualSense=" + report.dualSense + "; this is discovery only, not button/trigger/haptics testing");
        }
        static void CheckPause(NightApp app, Report report)
        {
            app.departureChoice = 1; app.StartNight(0); app.game.Tick(1f / 60f, new NightInput()); app.audioSystem.Consume(app.game); InvokeUpdate(app.audioSystem);
            int before = ActiveSources(app.audioSystem); float time = app.game.time, clock = app.game.clock.elapsedSeconds; Vector3 parent = app.game.parent.position;
            app.Pause();
            for (int i = 0; i < 30; i++) { InvokeUpdate(app); InvokeUpdate(app.audioSystem); }
            CheckThat(report, "Pause blocks real app Update simulation", app.paused && app.page == "pause" && app.game.time == time && app.game.clock.elapsedSeconds == clock && app.game.parent.position == parent);
            CheckThat(report, "Pause stops native active audio sources", before > 0 && ActiveSources(app.audioSystem) == 0, "active before=" + before + ", during=" + ActiveSources(app.audioSystem));
            app.Resume(); InvokeUpdate(app.audioSystem); CheckThat(report, "Resume restores ambience and preserves clock until next tick", app.page == "playing" && !app.paused && ActiveSources(app.audioSystem) > 0 && app.game.clock.elapsedSeconds == clock);
            app.game.Tick(1f / 60f, new NightInput()); CheckThat(report, "clock advances again on a normal tick after Resume", app.game.clock.elapsedSeconds > clock);
        }
        static void CheckSaveContinue(NightApp app, Report report)
        {
            app.departureChoice = 1; app.StartNight(0); app.game.seed = 4; var pilot = new NightPlaythroughPilot();
            for (int i = 0; i < 7200 && app.game.status == "playing"; i++)
            {
                app.game.Tick(1f / 60f, pilot.Next(app.game, 1f / 60f));
                if (app.game.hasDevice && !app.game.PhonePending() && app.game.mode == null) break;
            }
            var original = app.game; CheckThat(report, "save fixture reaches phone by normal input", original.hasDevice && original.status == "playing" && original.phone.state == "silenced", original.status + ", phone=" + original.phone.state);
            app.verificationMode = false; app.SaveGame(); app.verificationMode = true;
            var written = JsonUtility.FromJson<NightSave>(File.ReadAllText(app.SavePath));
            CheckThat(report, "native SaveGame writes versioned progress", written.version == 1 && written.game.hasDevice && written.game.seed == 4 && written.game.locks.Count > 0);
            app.Menu(); app.Continue(); var restored = app.game;
            bool doors = restored.doors.Count == original.doors.Count; for (int i = 0; doors && i < restored.doors.Count; i++) doors &= Mathf.Abs(restored.doors[i].progress - original.doors[i].progress) < .0001f;
            bool locks = restored.locks.Count == original.locks.Count; for (int i = 0; locks && i < restored.locks.Count; i++) locks &= JsonUtility.ToJson(restored.locks[i]) == JsonUtility.ToJson(original.locks[i]);
            CheckThat(report, "Continue restores position clock device phone and door progress", restored != original && restored.player == original.player && Mathf.Abs(restored.clock.elapsedSeconds - original.clock.elapsedSeconds) < .0001f && restored.hasDevice && restored.phone.state == original.phone.state && doors);
            CheckThat(report, "Continue preserves locks incidents and score history", locks && JsonUtility.ToJson(restored.metrics) == JsonUtility.ToJson(original.metrics) && restored.history.Count == original.history.Count && restored.spots.Find(s => s.device).incidentUsed == original.spots.Find(s => s.device).incidentUsed);
            CheckThat(report, "Continue releases held input before movement", restored.velocity == Vector3.zero && restored.interactionNeedsNeutral && app.page == "playing" && !app.paused);
            CheckThat(report, "Continue preserves free movement instead of an empty interaction", original.mode == null && restored.mode == null, "restored mode=" + (restored.mode == null ? "none" : "'" + restored.mode.type + "'"));
            var returnPilot = new NightPlaythroughPilot(); for (int i = 0; i < 5400 && restored.status == "playing"; i++) restored.Tick(1f / 60f, returnPilot.Next(restored, 1f / 60f));
            CheckThat(report, "continued normal-input return completes the night", restored.status == "won", "status=" + restored.status + "; mode=" + (restored.mode == null ? "none" : "'" + restored.mode.type + "'") + "; player=" + restored.player + "; clock=" + restored.clock.displayTime + "; decision=" + returnPilot.decision);
            app.settings.english = true; app.settings.playerSkin = "nightcap"; app.settings.parentSkin = "box"; app.unlocked = 3;
            app.verificationMode = false; app.SaveSettings(); app.verificationMode = true;
            var settings = JsonUtility.FromJson<NightSettings>(File.ReadAllText(Path.Combine(app.DataDirectory, "settings.json"))); var progress = JsonUtility.FromJson<NightProgress>(File.ReadAllText(Path.Combine(app.DataDirectory, "progress.json")));
            CheckThat(report, "settings and unlock progress round-trip separately", settings.english && settings.playerSkin == "nightcap" && settings.parentSkin == "box" && progress.unlocked == 3);
        }
        static void CheckSkins(NightApp app, Report report)
        {
            app.departureChoice = 0; app.StartNight(0); app.world.SetShowcase(false); var ids = new HashSet<string>(NightWorld.SkinIds);
            CheckThat(report, "wardrobe exposes eleven unique skin IDs", ids.Count == 11 && NightWorld.SkinIds.Length == 11);
            foreach (string id in NightWorld.SkinIds)
            {
                bool asset = Resources.Load<TextAsset>("NightBack/skin-" + id) != null; app.world.SetSkins(id, id); app.world.Sync(app.game);
                string first, second; bool player = InspectRig(app.world.playerVisual, id, out first), parent = InspectRig(app.world.parentVisual, id, out second);
                CheckThat(report, "skin builds for both roles: " + id, asset && player && parent && app.world.playerSkin == id && app.world.parentSkin == id, "player " + first + "; parent " + second);
            }
        }
        static void CheckTutorialSaveIsolation(NightApp app, Report report)
        {
            byte[] mainBefore = File.ReadAllBytes(app.SavePath); var formal = JsonUtility.FromJson<NightSave>(System.Text.Encoding.UTF8.GetString(mainBefore));
            app.verificationMode = false; app.StartNight(3); app.verificationMode = true;
            var tutorial = app.game; tutorial.seed = 2;
            CheckThat(report, "CompletePractice cannot skip unfinished lessons", !app.CompletePractice() && app.game.status == "playing" && app.game.tutorialStep == 0);
            for (int i = 0; i < 360 && tutorial.DoorNear() == null; i++) tutorial.Tick(1f / 60f, new NightInput { move = new Vector2(0, -1) });
            tutorial.Tick(1f / 60f, new NightInput { actionPressed = true });
            for (int i = 0; i < 360 && tutorial.tutorialStep < 3; i++) tutorial.Tick(1f / 60f, new NightInput { force = .9f, rightForce = .9f });
            int lesson = tutorial.tutorialStep; float angle = tutorial.doors[0].progress; Vector3 position = tutorial.player;
            CheckThat(report, "tutorial save fixture advances by normal movement grasp and push", lesson >= 3 && tutorial.mode != null && tutorial.mode.type == "door" && angle >= .16f, "lesson=" + lesson + "; door=" + angle);
            app.verificationMode = false; app.SaveGame(); app.verificationMode = true; app.Menu(); app.ContinueTutorial();
            CheckThat(report, "practice saves and ContinueTutorial preserve the formal slot bytes", app.HasSave && app.HasTutorialSave && BytesEqual(mainBefore, File.ReadAllBytes(app.SavePath)));
            CheckThat(report, "ContinueTutorial restores lesson position and door angle", app.game.IsTutorial && app.game.tutorialStep == lesson && app.game.player == position && Mathf.Abs(app.game.doors[0].progress - angle) < .0001f && app.game.mode != null && app.game.mode.type == "door");
            for (int i = 0; i < 30; i++) app.game.Tick(1f / 60f, new NightInput());
            CheckThat(report, "continued practice releases door force until new input", Mathf.Abs(app.game.doors[0].progress - angle) < .0001f && app.game.doors[0].force == 0 && app.game.doors[0].speed == 0);
            var tutorialPilot = new NightTutorialPilot();
            for (int i = 0; i < 18000 && app.game.status == "playing" && !(app.game.tutorialStep == 10 && app.game.mode == null); i++) app.game.Tick(.02f, tutorialPilot.Next(app.game, .02f));
            CheckThat(report, "continued tutorial reaches final free practice by normal input", app.game.status == "playing" && app.game.tutorialStep == 10 && app.game.mode == null && app.game.metrics.rescued >= 2, "lesson=" + app.game.tutorialStep + "; status=" + app.game.status + "; decision=" + tutorialPilot.decision);
            string finalLesson = JsonUtility.ToJson(app.game);
            app.Pause(); app.verificationMode = false; bool completed = app.CompletePractice(); app.verificationMode = true;
            var completedSave = JsonUtility.FromJson<NightSave>(File.ReadAllText(app.TutorialSavePath));
            CheckThat(report, "CompletePractice from pause saves the real tutorial result only", completed && app.page == "result" && completedSave.game.status == "won" && completedSave.game.IsTutorial && BytesEqual(mainBefore, File.ReadAllBytes(app.SavePath)));
            app.ContinueTutorial(); CheckThat(report, "completed practice starts again without overwriting formal progress", app.game.IsTutorial && app.game.status == "playing" && app.game.tutorialStep == 0 && BytesEqual(mainBefore, File.ReadAllBytes(app.SavePath)));
            // Rehydrate the actually reached last lesson, then press normal confirm. No lesson/position edits.
            var confirm = JsonUtility.FromJson<NightGame>(finalLesson); bool finalValid = confirm.RepairAfterLoad(); confirm.Start(); app.game = confirm; confirm.Tick(.02f, new NightInput { confirm = true });
            app.verificationMode = false; app.SaveSettings(); app.verificationMode = true;
            var confirmSave = JsonUtility.FromJson<NightSave>(File.ReadAllText(app.TutorialSavePath));
            CheckThat(report, "normal Tick confirm persists tutorial completion via result hook", finalValid && confirm.status == "won" && confirmSave.game.status == "won" && !app.HasTutorialSave && BytesEqual(mainBefore, File.ReadAllBytes(app.SavePath)));
            byte[] tutorialBefore = File.ReadAllBytes(app.TutorialSavePath); app.Continue();
            CheckThat(report, "formal Continue remains independent of practice", !app.game.IsTutorial && app.game.levelIndex == formal.game.levelIndex && app.game.hasDevice == formal.game.hasDevice && app.game.player == formal.game.player);
            app.verificationMode = false; app.SaveGame(); app.verificationMode = true;
            CheckThat(report, "formal SaveGame leaves the tutorial slot byte for byte intact", BytesEqual(tutorialBefore, File.ReadAllBytes(app.TutorialSavePath)));
        }
        static bool InspectRig(Transform actor, string id, out string detail)
        {
            Transform root = null; if (actor) foreach (var t in actor.GetComponentsInChildren<Transform>(true)) if (t.name == "Skin:" + id) { root = t; break; }
            if (!root) { detail = "missing Skin:" + id; return false; }
            int count = 0, skins = 0, vertices = 0; bool valid = true;
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                Mesh mesh = null; var skinned = renderer as SkinnedMeshRenderer; if (skinned) { mesh = skinned.sharedMesh; skins++; valid &= skinned.bones.Length > 0; foreach (var bone in skinned.bones) valid &= bone != null; if (mesh) valid &= mesh.bindposes.Length == skinned.bones.Length; }
                else { var filter = renderer.GetComponent<MeshFilter>(); if (filter) mesh = filter.sharedMesh; }
                if (!mesh) { valid = false; continue; } count++; vertices += mesh.vertexCount; valid &= mesh.vertexCount > 0 && mesh.subMeshCount > 0;
                foreach (var mat in renderer.sharedMaterials) valid &= mat != null && mat.shader != null && mat.shader.isSupported;
                Vector3 bounds = mesh.bounds.size; valid &= IsFinite(bounds.x) && IsFinite(bounds.y) && IsFinite(bounds.z);
            }
            detail = count + " meshes, " + skins + " skinned, " + vertices + " vertices"; return valid && count > 0 && vertices > 10;
        }
        static bool IsFinite(float value) { return !float.IsNaN(value) && !float.IsInfinity(value); }
        static void CheckEnglish(NightApp app, Report report)
        {
            app.ChangeLanguage(true); var seen = new HashSet<string>();
            foreach (string page in new[] { "menu", "settings", "help", "wardrobe", "pause" }) { app.ShowPage(page); SampleEnglish(app, report, seen, page); }
            app.departureChoice = 0; app.StartNight(0); SampleEnglish(app, report, seen, "22:30 start"); app.StartNight(3); SampleEnglish(app, report, seen, "tutorial introduction");
            for (int route = 0; route < 4; route++)
            {
                int night = route < 3 ? route : 0; app.departureChoice = route == 3 ? 2 : 1; app.StartNight(night); app.game.seed = 4;
                var pilot = new NightPlaythroughPilot { waitForFirstInspection = route == 3 }; string previous = "";
                for (int i = 0; i < 18000 && app.game.status == "playing"; i++)
                {
                    app.game.Tick(1f / 60f, pilot.Next(app.game, 1f / 60f)); var g = app.game;
                    string changed = (g.mode == null ? "walk" : g.mode.type + ":" + g.mode.hint + ":" + g.mode.phase) + g.InteractionHint() + g.toast + g.clock.phaseIndex;
                    if (changed != previous || i % 180 == 0) { app.ui.Refresh(); SampleEnglish(app, report, seen, "night " + (night + 1) + ", " + g.clock.displayTime + ", " + (g.mode == null ? "walk" : g.mode.type)); previous = changed; }
                    g.events.Clear();
                }
                CheckThat(report, "English HUD normal-input route completes: " + route, app.game.status == "won", "night=" + (night + 1) + "; status=" + app.game.status);
                app.page = "result"; app.ui.Show("result"); SampleEnglish(app, report, seen, "win result");
            }
            app.departureChoice = 2; app.StartNight(0); var exposed = new NightCaptureCaughtPilot();
            for (int i = 0; i < 4800 && app.game.status == "playing"; i++) app.game.Tick(1f / 60f, exposed.Next(app.game, 1f / 60f));
            CheckThat(report, "English GAME OVER fixture comes from real recognition", app.game.status == "lost" && app.game.parent.recognition >= NightGame.RecognitionTime);
            app.page = "result"; app.ui.Show("result"); SampleEnglish(app, report, seen, "lose result");
            CheckThat(report, "sampled English pages and live HUD contain no unintended Chinese", report.englishLeaks.Count == 0, report.englishSamples + " distinct text samples; the deliberate 中文 language selector is allowed");
        }
        static void SampleEnglish(NightApp app, Report report, HashSet<string> seen, string context)
        {
            if (app.ui.root == null) { CheckThat(report, "UI root available: " + context, false); return; }
            foreach (var element in app.ui.root.Query<TextElement>().ToList())
            {
                bool hidden = false; for (VisualElement p = element; p != null; p = p.parent) if (p.ClassListContains("hidden") || p.style.display == DisplayStyle.None) { hidden = true; break; }
                string text = element.text; if (hidden || string.IsNullOrEmpty(text) || text == "中文" || !seen.Add(text)) continue;
                report.englishSamples++; if (Cjk.IsMatch(text)) report.englishLeaks.Add(context + ": " + text);
            }
        }
    }
}
