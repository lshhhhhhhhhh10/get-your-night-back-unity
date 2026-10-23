// @role Animates the keyboard and controller practice models
// @calls NightText

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Native counterpart of source/tutorial-view.js. All teaching geometry is original procedural
    // project artwork, including the controller silhouette; it is not an official device model.
    // The scene, camera and animation are independent of the simulated player and their controls.
    public sealed class NightControlDemonstration : MonoBehaviour
    {
        public const int PreviewLayer = 29;
        public const int PreviewMask = 1 << PreviewLayer;
        public RenderTexture Texture { get; private set; }
        public string Scheme { get; private set; } = "keyboard";
        public string DemoAction { get; private set; } = "move";
        public bool Visible { get; private set; }
        public Camera PreviewCamera { get { return previewCamera; } }

        sealed class Part
        {
            public Transform transform;
            public Vector3 position;
            public Quaternion rotation;
            public Renderer[] renderers;
            public bool trigger, stick;
        }
        readonly Dictionary<string, Part> keys = new Dictionary<string, Part>();
        readonly Dictionary<string, Part> controls = new Dictionary<string, Part>();
        readonly List<Material> ownedMaterials = new List<Material>();
        Transform stage, keyboard, controller;
        Camera previewCamera;
        Material white, dark, gold, plate, ink, blue;
        Font font;
        NightGame game;
        float orbitYaw, orbitPitch = 36;

        public static NightControlDemonstration Ensure(GameObject owner)
        {
            var demo = owner.GetComponent<NightControlDemonstration>();
            if (!demo) demo = owner.AddComponent<NightControlDemonstration>();
            demo.Build(); return demo;
        }
        public static NightControlDemonstration Ensure(Transform owner) { return Ensure(owner.gameObject); }

        public void Build()
        {
            if (stage && Texture) return;
            stage = new GameObject("Native Control Demonstration Studio").transform;
            stage.SetParent(transform, false); stage.position = new Vector3(100, 0, 100);
            stage.gameObject.layer = PreviewLayer;
            white = MakeMaterial("Pearl controller shell", new Color(.79f, .86f, .87f), .28f);
            dark = MakeMaterial("Dark keys and controls", new Color(.055f, .095f, .14f), .23f);
            gold = MakeMaterial("Action highlight", new Color(1, .68f, .22f), .32f, .22f);
            plate = MakeMaterial("Studio blue", new Color(.045f, .09f, .13f), .08f);
            ink = MakeMaterial("Control ink", new Color(.88f, .96f, .96f), .1f, .18f);
            blue = MakeMaterial("Controller light strip", new Color(.24f, .70f, .91f), .1f, .35f);
            font = Resources.Load<Font>("NightBack/Fonts/LilitaOne-Regular");
            if (!font) font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            keyboard = Group("Keyboard and mouse", stage);
            controller = Group("Original controller teaching model", stage);
            BuildKeyboard(); BuildController();

            Primitive(stage, PrimitiveType.Cube, "Studio floor", new Vector3(0, -.25f, 0), new Vector3(5, .05f, 3.8f), plate);
            MakeLight("Warm teaching key", new Vector3(-2.5f, 4, -3.2f), new Color(1, .87f, .66f), 3.7f);
            MakeLight("Cool teaching fill", new Vector3(3.2f, 2.4f, -.8f), new Color(.46f, .73f, 1), 2.9f);
            MakeLight("Soft top outline", new Vector3(0, 3.6f, 2.6f), new Color(.69f, .9f, 1), 3.1f);

            Texture = new RenderTexture(320, 200, 16, RenderTextureFormat.ARGB32);
            Texture.name = "Night Back control demonstration"; Texture.filterMode = FilterMode.Bilinear;
            Texture.antiAliasing = 2; Texture.useMipMap = false; Texture.Create();
            previewCamera = Group("Control preview camera", stage).gameObject.AddComponent<Camera>();
            previewCamera.enabled = false; previewCamera.targetTexture = Texture;
            previewCamera.cullingMask = PreviewMask; previewCamera.clearFlags = CameraClearFlags.SolidColor;
            previewCamera.backgroundColor = new Color(.025f, .047f, .075f, 1);
            previewCamera.nearClipPlane = .05f; previewCamera.farClipPlane = 15;
            previewCamera.fieldOfView = 35; previewCamera.aspect = 1.6f;
            previewCamera.allowHDR = false; previewCamera.allowMSAA = true;
            UpdateOrbit(); SetScheme(Scheme); stage.gameObject.SetActive(Visible);
        }

        public void SetVisible(bool value)
        {
            Build(); Visible = value; stage.gameObject.SetActive(value);
        }
        public void SetScheme(string scheme)
        {
            Scheme = scheme == "controller" || scheme == "gamepad" ? "controller" : "keyboard";
            if (controller) controller.gameObject.SetActive(Scheme == "controller");
            if (keyboard) keyboard.gameObject.SetActive(Scheme == "keyboard");
        }
        public void Sync(NightGame current, string scheme = "keyboard")
        {
            Build(); game = current; SetScheme(scheme); DemoAction = ActionFor(game);
        }
        public void DragOrbit(Vector2 delta)
        {
            orbitYaw = Mathf.Repeat(orbitYaw + delta.x * .45f + 180, 360) - 180;
            orbitPitch = Mathf.Clamp(orbitPitch + delta.y * .28f, 16, 72); UpdateOrbit();
        }
        public void ResetOrbit() { orbitYaw = 0; orbitPitch = 36; UpdateOrbit(); }
        void UpdateOrbit()
        {
            if (!previewCamera || !stage) return;
            float a = orbitYaw * Mathf.Deg2Rad, p = orbitPitch * Mathf.Deg2Rad;
            Vector3 center = stage.position + Vector3.up * .03f;
            previewCamera.transform.position = center + new Vector3(Mathf.Sin(a) * Mathf.Cos(p), Mathf.Sin(p), -Mathf.Cos(a) * Mathf.Cos(p)) * 5.35f;
            previewCamera.transform.rotation = Quaternion.LookRotation(center - previewCamera.transform.position, Vector3.up);
        }

        // Keep the source tutorial's contextual switches, rather than guessing only from a step number.
        public static string ActionFor(NightGame g)
        {
            if (g == null) return "move";
            int s = Mathf.Clamp(g.tutorialStep, 0, NightGame.TutorialStepCount - 1);
            string action = new[] { "move", "interact", "push", "release", "reverse", "push", "support", "place", "interact", "support", "move" }[s];
            var mode = g.mode;
            if (s >= 1 && s <= 4 && (mode == null || mode.type != "door")) action = "interact";
            if (s == 4 && mode != null && mode.type == "door" && mode.index >= 0 && mode.index < g.doors.Count && g.doors[mode.index].direction == -1) action = "push";
            if (s == 5 && g.doors.Count > 0)
            {
                bool open = g.doors[0].progress >= .95f;
                if (mode != null && mode.type == "door" && open) action = "cancel";
                else if (mode == null) action = open ? "move" : "interact";
            }
            if ((s == 6 || s == 7 || s == 9) && mode == null) action = "interact";
            if (mode != null)
            {
                if ((mode.type == "catch" || mode.type == "reaction") && g.rescue != null) action = g.rescue.stage == "lower" ? "place" : "support";
                else if (mode.type == "tickle") action = "tickle";
                else if (mode.type == "step") action = "step";
                else if (mode.type == "lockpick") action = "lock";
                else if (mode.type == "search") action = "hold";
                else if (s == 10 && mode.type == "door") action = "push";
            }
            return action;
        }

        public string Caption
        {
            get
            {
                bool pad = Scheme == "controller";
                switch (DemoAction)
                {
                    case "interact": return NightText.Pick(pad ? "按一次 □：建立接触" : "按一次 E：建立接触", pad ? "Tap square to make contact" : "Tap E to make contact");
                    case "push": return NightText.Pick(pad ? "轻压 R2；松开就停" : "短按住 E；松开就停", pad ? "Squeeze R2 gently; release to stop" : "Hold E briefly; release to stop");
                    case "release": return NightText.Pick(pad ? "完全松开 R2，仍然握着" : "松开 E，仍然握着", pad ? "Release R2 while keeping contact" : "Release E while keeping contact");
                    case "reverse": return NightText.Pick(pad ? "L1 换方向，再轻压 R2" : "R 换方向，再短按 E", pad ? "L1 reverses; R2 applies force" : "R reverses; E applies force");
                    case "cancel": return NightText.Pick(pad ? "○ 收手，再用左摇杆移动" : "Esc 收手，再用 WASD 移动", pad ? "Circle lets go; left stick moves" : "Esc lets go; WASD moves");
                    case "support": return NightText.Pick(pad ? "双扳机托稳；左摇杆对准" : "Q + E 托稳；A / D 对准", pad ? "Both triggers support; left stick aligns" : "Q + E support; A / D align");
                    case "place": return NightText.Pick(pad ? "保持双扳机；左摇杆轻放" : "保持 Q + E；W 抬起，S 轻放", pad ? "Keep both triggers; lower with left stick" : "Keep Q + E; W lifts, S lowers");
                    case "step": return NightText.Pick(pad ? "× 在亮区落脚" : "Space 在亮区落脚", pad ? "Cross steps in the bright zone" : "Space steps in the bright zone");
                    case "tickle": return NightText.Pick(pad ? "轻压 R2，右摇杆轻挠" : "按住 E 轻挠，松手停下", pad ? "Gentle R2 and right stick" : "Hold E to tickle; release to stop");
                    case "lock": return NightText.Pick(pad ? "左摇杆选弹子，轻压 R2" : "A / D 选弹子，E 轻抬", pad ? "Left stick selects; R2 gently lifts" : "A / D select; E gently lifts");
                    case "hold": return NightText.Pick(pad ? "保持 R2 轻轻操作" : "保持 E 轻轻操作", pad ? "Hold R2 gently" : "Hold E gently");
                    default: return NightText.Pick(pad ? "左摇杆移动，右摇杆转头" : "WASD 移动，鼠标转头", pad ? "Left stick moves; right stick looks" : "WASD moves; mouse looks");
                }
            }
        }

        void LateUpdate()
        {
            if (!Visible || !stage || !previewCamera || !Texture) return;
            DemoAction = ActionFor(game); Animate(Time.unscaledTime); previewCamera.Render();
        }
        void Animate(float now)
        {
            foreach (var part in keys.Values) Reset(part);
            foreach (var part in controls.Values) Reset(part);
            float phase = now % 3.6f;
            float press = phase < .7f ? phase / .7f : phase < 1.65f ? 1 : phase < 2.25f ? 1 - (phase - 1.65f) / .6f : 0;
            float release = phase < .4f ? 1 : phase < 1.2f ? 1 - (phase - .4f) / .8f : 0;
            float x = Mathf.Sin(phase * 2) * .075f;
            switch (DemoAction)
            {
                case "push": Key("E", press); Pad("R2", press * .4f); break;
                case "release": Key("E", release); Pad("R2", release * .4f); break;
                case "reverse":
                    if (phase < 1.2f) { Key("R", press); Pad("L1", press); }
                    else { Key("E", press); Pad("R2", press * .4f); } break;
                case "interact": Key("E", phase < .7f ? press : 0); Pad("interact", phase < .7f ? press : 0); break;
                case "hold": Key("E", press); Pad("R2", press * .4f); break;
                case "cancel": Key("Esc", press); Pad("back", press); break;
                case "step": Key("Space", press); Pad("confirm", press); break;
                case "support": case "place":
                    Key("Q", .55f); Key("E", .55f); Pad("L2", .5f); Pad("R2", .5f);
                    bool place = DemoAction == "place";
                    Key(place ? (phase < 1.8f ? "W" : "S") : (phase < 1.8f ? "A" : "D"), press);
                    Stick("left", place ? new Vector2(0, phase < 1.8f ? .09f : -.09f) : new Vector2(x, 0)); break;
                case "tickle": Key("E", press); Pad("R2", .3f); Stick("right", new Vector2(x, .04f * Mathf.Sin(phase * 3))); break;
                case "lock":
                    if (phase < 1.4f) { Key("D", press); Stick("left", new Vector2(.08f, 0)); }
                    else { Key("E", press); Pad("R2", press * .55f); } break;
                default:
                    Key(phase < 1.8f ? "W" : "D", press);
                    Key("Mouse", press); Stick("left", new Vector2(x, press * .09f)); Stick("right", new Vector2(x, .025f)); break;
            }
        }
        void Reset(Part part)
        {
            part.transform.localPosition = part.position; part.transform.localRotation = part.rotation;
            foreach (Renderer renderer in part.renderers) renderer.sharedMaterial = dark;
        }
        void Key(string id, float value) { Part p; if (keys.TryGetValue(id, out p)) Press(p, value); }
        void Pad(string id, float value) { Part p; if (controls.TryGetValue(id, out p)) Press(p, value); }
        void Press(Part part, float value)
        {
            foreach (Renderer renderer in part.renderers) renderer.sharedMaterial = gold;
            if (part.trigger) part.transform.localRotation = part.rotation * Quaternion.Euler(value * 31, 0, 0);
            else part.transform.localPosition = part.position - Vector3.up * (value * .075f);
        }
        void Stick(string id, Vector2 direction)
        {
            Part p; if (!controls.TryGetValue(id, out p)) return; Press(p, 0);
            p.transform.localPosition = p.position + new Vector3(direction.x, 0, direction.y);
            p.transform.localRotation = p.rotation * Quaternion.Euler(direction.y * 120, 0, -direction.x * 120);
        }

        void BuildKeyboard()
        {
            Primitive(keyboard, PrimitiveType.Cube, "Keyboard chassis", new Vector3(-.22f, -.08f, -.13f), new Vector3(3.14f, .16f, 1.89f), plate);
            string[] names = { "W", "A", "S", "D", "Q", "E", "R", "C", "Esc", "Space" };
            float[] xx = { -.65f, -1.13f, -.65f, -.17f, -1.13f, .44f, 1.02f, .44f, 1.1f, -.3f };
            float[] zz = { .38f, -.12f, -.12f, -.12f, .38f, .38f, .38f, -.18f, -.18f, -.8f };
            float[] widths = { .42f, .42f, .42f, .42f, .42f, .5f, .5f, .5f, .56f, 1.7f };
            for (int i = 0; i < names.Length; i++)
            {
                Transform key = Primitive(keyboard, PrimitiveType.Cube, "Key " + names[i], new Vector3(xx[i], .08f, zz[i]), new Vector3(widths[i], .19f, .40f), dark);
                // The label follows the pressing key, but is excluded from highlight material replacement.
                Label(key, names[i], new Vector3(0, .515f, 0), names[i] == "Space" ? .15f : .18f, true);
                Register(keys, names[i], key, false, false, false);
            }
            var mouse = Group("Mouse", keyboard); mouse.localPosition = new Vector3(1.75f, .03f, .02f);
            Primitive(mouse, PrimitiveType.Sphere, "Mouse shell", Vector3.zero, new Vector3(.58f, .22f, .94f), dark);
            Primitive(mouse, PrimitiveType.Cube, "Mouse divide", new Vector3(0, .108f, .14f), new Vector3(.017f, .014f, .33f), blue);
            Primitive(mouse, PrimitiveType.Sphere, "Mouse wheel", new Vector3(0, .118f, .17f), new Vector3(.065f, .055f, .15f), ink);
            Label(keyboard, "LOOK", new Vector3(1.75f, .23f, -.52f), .15f);
            Register(keys, "Mouse", mouse, false, false, true);
        }
        void BuildController()
        {
            Primitive(controller, PrimitiveType.Sphere, "Pearl body", new Vector3(0, -.015f, .02f), new Vector3(2.65f, .39f, 1.32f), white);
            for (int side = -1; side <= 1; side += 2)
            {
                Transform grip = Primitive(controller, PrimitiveType.Sphere, "Swept grip", new Vector3(side * 1.08f, -.025f, -.45f), new Vector3(.68f, .48f, 1.22f), white);
                grip.localRotation = Quaternion.Euler(0, -side * 22, side * 4);
            }
            Primitive(controller, PrimitiveType.Cube, "Center face", new Vector3(0, .18f, -.18f), new Vector3(1.4f, .10f, .82f), dark);
            Primitive(controller, PrimitiveType.Cube, "Touchpad", new Vector3(0, .255f, .23f), new Vector3(1.10f, .09f, .54f), plate);
            for (int side = -1; side <= 1; side += 2) Primitive(controller, PrimitiveType.Cube, "Blue trim", new Vector3(side * .6f, .235f, .21f), new Vector3(.024f, .025f, .55f), blue);
            foreach (string id in new[] { "left", "right" })
            {
                float x = id == "left" ? -.48f : .48f;
                Disk(controller, "Analog collar", new Vector3(x, .22f, -.46f), .25f, .08f, dark);
                Transform top = Disk(controller, id + " analog cap", new Vector3(x, .32f, -.46f), .18f, .10f, dark);
                Register(controls, id, top, false, true, false);
                Label(controller, id == "left" ? "L" : "R", new Vector3(x, .32f, -.81f), .15f);
            }
            string[] names = { "interact", "confirm", "back", "map" };
            Vector3[] positions = { new Vector3(.68f, .25f, -.02f), new Vector3(.95f, .25f, -.25f), new Vector3(1.21f, .25f, -.02f), new Vector3(.95f, .25f, .22f) };
            for (int i = 0; i < names.Length; i++)
            {
                Transform button = Disk(controller, names[i], positions[i], .108f, .105f, dark);
                Symbol(button, i); Register(controls, names[i], button, false, false, false);
            }
            Primitive(controller, PrimitiveType.Cube, "D-pad horizontal", new Vector3(-1, .255f, .02f), new Vector3(.47f, .10f, .12f), dark);
            Primitive(controller, PrimitiveType.Cube, "D-pad vertical", new Vector3(-1, .255f, .02f), new Vector3(.12f, .10f, .47f), dark);
            foreach (string id in new[] { "L2", "R2" })
            {
                float x = id == "L2" ? -.92f : .92f;
                Transform pivot = Group(id + " trigger hinge", controller); pivot.localPosition = new Vector3(x, .10f, .68f);
                Primitive(pivot, PrimitiveType.Cube, id + " trigger", new Vector3(0, .07f, .035f), new Vector3(.47f, .20f, .34f), dark);
                Label(pivot, id, new Vector3(0, .181f, .035f), .19f);
                Register(controls, id, pivot, true, false, true);
            }
            Transform shoulder = Primitive(controller, PrimitiveType.Cube, "L1 shoulder", new Vector3(-.92f, .31f, .47f), new Vector3(.47f, .115f, .13f), dark);
            Label(controller, "L1", new Vector3(-.92f, .39f, .46f), .13f); Register(controls, "L1", shoulder, false, false, false);
        }

        void Register(Dictionary<string, Part> map, string id, Transform t, bool trigger, bool stick, bool children)
        {
            var renderers = new List<Renderer>();
            if (children) foreach (var r in t.GetComponentsInChildren<Renderer>()) { if (!r.GetComponent<TextMesh>()) renderers.Add(r); }
            else { Renderer renderer = t.GetComponent<Renderer>(); if (renderer) renderers.Add(renderer); }
            map[id] = new Part { transform = t, position = t.localPosition, rotation = t.localRotation, renderers = renderers.ToArray(), trigger = trigger, stick = stick };
        }
        Transform Group(string name, Transform parent)
        { var t = new GameObject(name).transform; t.SetParent(parent, false); t.gameObject.layer = PreviewLayer; return t; }
        Transform Primitive(Transform parent, PrimitiveType type, string name, Vector3 position, Vector3 scale, Material material)
        {
            var o = GameObject.CreatePrimitive(type); o.name = name; o.layer = PreviewLayer;
            o.transform.SetParent(parent, false); o.transform.localPosition = position; o.transform.localScale = scale;
            var collider = o.GetComponent<Collider>(); if (collider) { collider.enabled = false; DestroyOwned(collider); }
            var renderer = o.GetComponent<Renderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; return o.transform;
        }
        Transform Disk(Transform parent, string name, Vector3 position, float radius, float height, Material material)
        { return Primitive(parent, PrimitiveType.Cylinder, name, position, new Vector3(radius * 2, height * .5f, radius * 2), material); }
        void Label(Transform parent, string value, Vector3 position, float size, bool compensateScale = false)
        {
            Transform t = Group("Label " + value, parent); t.localPosition = position; t.localRotation = Quaternion.Euler(90, 0, 0);
            if (compensateScale) t.localScale = new Vector3(1 / parent.localScale.x, 1 / parent.localScale.z, 1 / parent.localScale.y);
            TextMesh text = t.gameObject.AddComponent<TextMesh>(); text.font = font; text.text = value; text.fontSize = 64;
            text.characterSize = size * 10f / text.fontSize; text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.color = new Color(.914f, .827f, .729f);
            var renderer = t.GetComponent<MeshRenderer>(); renderer.sharedMaterial = font.material; renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }
        void Symbol(Transform button, int kind)
        {
            // Geometry icons remain legible without relying on a system font containing controller glyphs.
            Transform icon = Group("Face symbol", button); icon.localScale = new Vector3(1 / button.localScale.x, 1 / button.localScale.y, 1 / button.localScale.z); icon.localPosition = new Vector3(0, 1.04f, 0);
            if (kind == 0) { Stroke(icon, -.045f, -.045f, .045f, -.045f); Stroke(icon, .045f, -.045f, .045f, .045f); Stroke(icon, .045f, .045f, -.045f, .045f); Stroke(icon, -.045f, .045f, -.045f, -.045f); }
            else if (kind == 1) { Stroke(icon, -.045f, -.045f, .045f, .045f); Stroke(icon, -.045f, .045f, .045f, -.045f); }
            else if (kind == 2) { for (int i = 0; i < 16; i++) { float a = i * Mathf.PI / 8, b = (i + 1) * Mathf.PI / 8; Stroke(icon, Mathf.Cos(a) * .05f, Mathf.Sin(a) * .05f, Mathf.Cos(b) * .05f, Mathf.Sin(b) * .05f); } }
            else { Stroke(icon, 0, .052f, -.05f, -.04f); Stroke(icon, -.05f, -.04f, .05f, -.04f); Stroke(icon, .05f, -.04f, 0, .052f); }
        }
        void Stroke(Transform parent, float x1, float z1, float x2, float z2)
        {
            Vector3 delta = new Vector3(x2 - x1, 0, z2 - z1);
            Transform line = Primitive(parent, PrimitiveType.Cube, "Symbol stroke", new Vector3((x1 + x2) * .5f, .002f, (z1 + z2) * .5f), new Vector3(.012f, .009f, delta.magnitude), ink);
            line.localRotation = Quaternion.LookRotation(delta, Vector3.up);
        }
        Material MakeMaterial(string name, Color color, float gloss, float emission = 0)
        {
            var m = new Material(Shader.Find("Standard")); m.name = name; m.color = color; m.SetFloat("_Glossiness", gloss);
            if (emission > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", color * emission); }
            ownedMaterials.Add(m); return m;
        }
        void MakeLight(string name, Vector3 position, Color color, float intensity)
        {
            Transform t = Group(name, stage); t.localPosition = position;
            var light = t.gameObject.AddComponent<Light>(); light.type = LightType.Point; light.range = 11; light.intensity = intensity;
            light.color = color; light.cullingMask = PreviewMask; light.shadows = LightShadows.Soft; light.shadowBias = .035f;
        }
        static void DestroyOwned(Object obj)
        { if (!obj) return; if (Application.isPlaying) Destroy(obj); else DestroyImmediate(obj); }
        void OnDestroy()
        {
            if (previewCamera) previewCamera.targetTexture = null;
            if (Texture) { Texture.Release(); DestroyOwned(Texture); Texture = null; }
            if (stage) DestroyOwned(stage.gameObject);
            foreach (var m in ownedMaterials) DestroyOwned(m); ownedMaterials.Clear();
        }
    }
}
