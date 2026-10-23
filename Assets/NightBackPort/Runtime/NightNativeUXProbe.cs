#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightBackPort
{
    [Serializable] public class NightInputProbeButton
    {
        public string text, name, picking, display; public bool enabled, visible; public Rect bounds;
    }
    [Serializable] public class NightInputProbeFrame
    {
        public int frame, width, height; public float realtime; public bool focused, mouseHeld, mouseDown, mouseUp, anyKeyDown;
        public Vector3 mouse; public Vector2 panelPoint; public string page, rootPicking, panelType, hitType, hitName, hitText;
        public bool documentEnabled, rootEnabled, hasPanel; public Rect rootBounds, panelBounds;
        public List<NightInputProbeButton> buttons = new List<NightInputProbeButton>();
    }
    [Serializable] public class NightInputProbeEvent
    {
        public int frame; public float realtime; public string type, target, text, key; public Vector3 position;
    }
    [Serializable] public class NightInputProbeReport
    {
        public string scope, timestamp, status, error; public bool completed; public float seconds;
        public List<NightInputProbeFrame> frames = new List<NightInputProbeFrame>();
        public List<NightInputProbeEvent> events = new List<NightInputProbeEvent>();
    }

    // Read-only observation from real Play Mode frames, not one-shot editor GUI values.
    public sealed class NightNativeUXProbe : MonoBehaviour
    {
        public NightApp app; public bool running; public NightInputProbeReport report;
        float start, duration; VisualElement observedRoot;
        EventCallback<PointerDownEvent> down; EventCallback<PointerUpEvent> up;
        EventCallback<ClickEvent> click; EventCallback<KeyDownEvent> key;
        public void Begin(float seconds)
        {
            if (running) return;
            if (!Application.isPlaying || app == null) throw new InvalidOperationException("Input probe requires the native app in Play Mode.");
            duration = Mathf.Clamp(seconds, 1, 30); start = Time.realtimeSinceStartup; running = true;
            report = new NightInputProbeReport { timestamp = DateTime.Now.ToString("o"), status = "observing", scope = "Read-only LateUpdate samples and real UI Toolkit events. No injected input, EventSystem changes, state overrides, or claim of physical hardware validation." };
            ObserveRoot();
        }
        void ObserveRoot()
        {
            var root = app != null && app.ui != null ? app.ui.root : null;
            if (root == observedRoot) return; Unsubscribe(); observedRoot = root; if (root == null) return;
            down = e => Event("PointerDown", e.target, e.position, ""); up = e => Event("PointerUp", e.target, e.position, "");
            click = e => Event("Click", e.target, e.position, ""); key = e => Event("KeyDown", e.target, Vector3.zero, e.keyCode.ToString());
            root.RegisterCallback(down, TrickleDown.TrickleDown); root.RegisterCallback(up, TrickleDown.TrickleDown);
            root.RegisterCallback(click, TrickleDown.TrickleDown); root.RegisterCallback(key, TrickleDown.TrickleDown);
        }
        void Event(string type, IEventHandler handler, Vector3 position, string keyName)
        {
            var target = handler as VisualElement; var text = target as TextElement;
            report.events.Add(new NightInputProbeEvent { frame = Time.frameCount, realtime = Time.realtimeSinceStartup - start, type = type, target = target == null ? "null" : target.GetType().Name + ":" + target.name, text = text == null ? "" : text.text, position = position, key = keyName });
        }
        void LateUpdate()
        {
            if (!running) return;
            try
            {
                ObserveRoot(); var root = observedRoot; var doc = app.GetComponent<UIDocument>();
                var f = new NightInputProbeFrame { frame = Time.frameCount, width = Screen.width, height = Screen.height, realtime = Time.realtimeSinceStartup - start, focused = Application.isFocused,
                    mouse = Input.mousePosition, mouseHeld = Input.GetMouseButton(0), mouseDown = Input.GetMouseButtonDown(0), mouseUp = Input.GetMouseButtonUp(0), anyKeyDown = Input.anyKeyDown, page = app.page,
                    documentEnabled = doc != null && doc.enabled, rootEnabled = root != null && root.enabledInHierarchy, hasPanel = root != null && root.panel != null };
                if (root != null)
                {
                    f.rootBounds = root.worldBound; f.rootPicking = root.pickingMode.ToString();
                    if (root.panel != null)
                    {
                        f.panelType = root.panel.GetType().FullName; f.panelBounds = root.panel.visualTree.worldBound;
                        f.panelPoint = RuntimePanelUtils.ScreenToPanel(root.panel, new Vector2(f.mouse.x, Screen.height - f.mouse.y));
                        var hit = root.panel.Pick(f.panelPoint); f.hitType = hit == null ? "none" : hit.GetType().Name; f.hitName = hit == null ? "" : hit.name;
                        var hitText = hit as TextElement; f.hitText = hitText == null ? "" : hitText.text;
                    }
                    foreach (var b in root.Query<Button>().ToList()) f.buttons.Add(new NightInputProbeButton { text = b.text, name = b.name, enabled = b.enabledInHierarchy, visible = b.visible, bounds = b.worldBound, picking = b.pickingMode.ToString(), display = b.resolvedStyle.display.ToString() });
                }
                report.frames.Add(f);
                if (f.realtime >= duration || report.frames.Count >= 1800) Finish(true, "");
            }
            catch (Exception e) { Finish(false, e.ToString()); }
        }
        void Unsubscribe()
        {
            if (observedRoot != null)
            {
                if (down != null) observedRoot.UnregisterCallback(down, TrickleDown.TrickleDown);
                if (up != null) observedRoot.UnregisterCallback(up, TrickleDown.TrickleDown);
                if (click != null) observedRoot.UnregisterCallback(click, TrickleDown.TrickleDown);
                if (key != null) observedRoot.UnregisterCallback(key, TrickleDown.TrickleDown);
            }
            observedRoot = null;
        }
        void Finish(bool success, string error)
        {
            running = false; Unsubscribe(); report.completed = success; report.status = success ? "complete" : "interrupted"; report.error = error; report.seconds = Time.realtimeSinceStartup - start;
            string path = Path.GetFullPath(Path.Combine(Application.dataPath, "../NightBackEvidence/native-input-probe.json")); Directory.CreateDirectory(Path.GetDirectoryName(path)); File.WriteAllText(path, JsonUtility.ToJson(report, true));
        }
        void OnDisable() { if (running) Finish(false, "Probe was disabled before the requested duration."); }
    }
}
#endif
