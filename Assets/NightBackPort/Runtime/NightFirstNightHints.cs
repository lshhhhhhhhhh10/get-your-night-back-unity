// @role Reveals nearby first-night action instructions
// @calls NightText
// @tune ReadDistance:float:3.15-3.15:Baseline nearby reading distance in world units; not changed.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Optional, surface-bound writing. Reading never captures input or changes the clock.
    public sealed class NightFirstNightHints : MonoBehaviour
    {
        public sealed class Line
        {
            public TextMesh text;
            public Renderer renderer, icon;
            public Mesh iconMesh;
            public string copy;
            public float start, duration;
            public MaterialPropertyBlock properties = new MaterialPropertyBlock();
        }
        public sealed class Note
        {
            public string id, nearby;
            public Transform mount, surface;
            public Vector3 surfacePosition;
            public Vector2 size;
            public bool direction;
            public float duration, opacity;
            public bool immediate;
            public float surfaceStretch = 1;
            public int index;
            public readonly List<Line> lines = new List<Line>();
        }
        public readonly List<Note> Notes = new List<Note>();
        public Transform Visual { get; private set; }
        public const float ReadDistance = 3.15f;
        NightWorld world;
        NightGame game;
        Font font;
        Material ink, iconInk, phoneShellMaterial, phoneScreenMaterial;
        public Note PencilPrompt { get; private set; }
        public Note PhonePrompt { get; private set; }
        public Transform HeldPhone { get; private set; }
        Transform phoneProgress;
        readonly List<Note> contextNotes = new List<Note>();
        string contextCopy;
        bool phoneWasPending;
        float phoneSuccessRemaining;
        bool english, pad, dualSense, initialized, fontDirty;
        float departure;

        public void Build(NightWorld owner, NightGame state)
        {
            world = owner; game = state;
            Visual = new GameObject("First night · living ink").transform;
            Visual.SetParent(transform, false);
            ink = new Material(Resources.Load<Shader>("NightBack/FirstNightHints/WallText"));
            iconInk = new Material(ink); iconInk.mainTexture = Texture2D.whiteTexture;
            if (game.levelIndex == 0)
            {
                Add("door", "bedroom-door", Vector3.zero, Vector3.forward, .73f, 1.02f);
                Notes[0].surface = world.GetDoorTransform(0);
                Notes[0].surfacePosition = new Vector3(.47f, 1.44f, .077f);
                Add("hide", "living-search", new Vector3(.548f, 1.77f, 1.14f), Vector3.right, 1.08f, 1.10f);
                Add("time", "living-search", new Vector3(5.29f, 1.76f, .548f), Vector3.forward, 1.90f, .94f);
                Add("search", "study-search", new Vector3(14.548f, 1.72f, 1.62f), Vector3.right, 1.60f, .96f);
                Add("return", "study-shelf", new Vector3(17.452f, 1.40f, 2.85f), Vector3.left, 1.22f, .85f);
                Add("route", "bedroom-door", new Vector3(.548f, 1.98f, 11.20f), Vector3.right, 1.30f, .82f, true);
                AddFloor("living", "living-search");
                AddFloor("drawer", "study-search");
                AddFloor("shelf", "study-shelf");
                // Optional save data: old saves start with blank hints; new saves retain partial writing.
                if (game.firstNightHintProgress == null || game.firstNightHintProgress.Length != Notes.Count)
                    Array.Resize(ref game.firstNightHintProgress, Notes.Count);
                for (int i = 0; i < Notes.Count; i++)
                    game.firstNightHintProgress[i] = float.IsNaN(game.firstNightHintProgress[i]) ? 0 : Mathf.Clamp01(game.firstNightHintProgress[i]);
            }
            BuildContextPrompts();
            RefreshCopy(false, false);
            Font.textureRebuilt += RefreshFontTexture;
            RefreshVisibility();
        }

        void Add(string id, string nearby, Vector3 position, Vector3 inward, float width, float height, bool direction = false)
        {
            var mount = new GameObject("Surface writing · " + id).transform;
            mount.SetParent(Visual, false); mount.localPosition = position; mount.localRotation = Quaternion.LookRotation(inward);
            Notes.Add(new Note { id = id, nearby = nearby, mount = mount, size = new Vector2(width, height), direction = direction, index = Notes.Count });
        }

        void AddFloor(string id, string nearby, float surfaceHeight = .035f)
        {
            var furniture = Array.Find(game.level.furniture, f => f.id == nearby);
            if (furniture == null) throw new InvalidOperationException("Missing first-night cabinet: " + nearby);
            var front = Quaternion.Euler(0, furniture.yaw * Mathf.Rad2Deg, 0) * Vector3.forward;
            // Clear the low rug and raised plank joints; stay outside the cabinet footprint.
            var position = furniture.Position + front * (furniture.d * .5f + .60f) + Vector3.up * (surfaceHeight + .004f);
            Add(id, nearby, position, Vector3.forward, Mathf.Min(furniture.w, 1.35f), .50f, true);
            // The top of the text and its arrow point towards the cabinet, read from the aisle.
            Notes[Notes.Count - 1].mount.localRotation = Quaternion.LookRotation(Vector3.up, -front);
        }

        void Write(Note note, string title, string body, string footer)
        {
            foreach (var line in note.lines)
            {
                line.text.gameObject.SetActive(false); Destroy(line.text.gameObject);
                if (line.iconMesh) Destroy(line.iconMesh);
            }
            note.lines.Clear(); note.duration = 0;
            var values = new List<string>();
            if (!string.IsNullOrEmpty(title)) values.Add(title);
            if (!string.IsNullOrEmpty(body)) values.AddRange(body.Split('\n'));
            int footerStart = values.Count;
            if (!string.IsNullOrEmpty(footer)) values.AddRange(footer.Split('\n'));
            font.RequestCharactersInTexture(string.Join("", values.ToArray()), 96, FontStyle.Normal);
            float row = note.size.y / values.Count;
            for (int i = 0; i < values.Count; i++)
            {
                var obj = new GameObject("Ink line " + i); obj.transform.SetParent(note.mount, false);
                obj.transform.localPosition = new Vector3(0, (values.Count * .5f - i - .5f) * row, .002f);
                obj.transform.localRotation = Quaternion.Euler(0, 180, 0);
                var text = obj.AddComponent<TextMesh>(); text.font = font; text.fontSize = 96; text.characterSize = .01f;
                text.anchor = TextAnchor.MiddleCenter; text.alignment = TextAlignment.Center; text.richText = false;
                bool square = values[i].StartsWith("□ ", StringComparison.Ordinal);
                text.text = square ? values[i].Substring(2) : values[i]; text.color = note.immediate ? new Color(.96f, .88f, .69f) : new Color(.90f, .82f, .64f);
                var r = obj.GetComponent<Renderer>(); r.sharedMaterial = ink;
                r.shadowCastingMode = ShadowCastingMode.Off; r.receiveShadows = false;
                var b = r.localBounds;
                Renderer icon = null; Mesh iconMesh = null;
                if (square)
                {
                    icon = Square(obj.transform, b, text.color, out iconMesh);
                    b.Encapsulate(icon.localBounds);
                    // Keep the square independent of font glyph coverage.
                }
                float height = Mathf.Min(row * .70f, i >= footerStart ? .063f : i == 0 ? .115f : .105f);
                float scale = Mathf.Min((note.size.x - .035f) / Mathf.Max(.001f, b.size.x), height / Mathf.Max(.001f, b.size.y));
                obj.transform.localScale = new Vector3(scale, scale * note.surfaceStretch, scale);
                if (square) obj.transform.localPosition -= obj.transform.localRotation * new Vector3(b.center.x * scale, 0, 0);
                var line = new Line { text = text, renderer = r, icon = icon, iconMesh = iconMesh, copy = values[i], start = note.duration, duration = Mathf.Max(.65f, values[i].Length * (english ? .050f : .12f)) };
                note.duration += line.duration + .28f;
                line.properties.SetFloat("_MinX", b.min.x); line.properties.SetFloat("_MaxX", b.max.x);
                note.lines.Add(line);
            }
            ApplyInk(note);
        }

        public static float FirstWarningDelay(float departureSeconds)
        {
            float phase = NightClock.RealSecondsPerPhase;
            float position = Mathf.Repeat(departureSeconds, phase * 4);
            return position >= phase * 2 && position < phase * 3 ? 0 : Mathf.Repeat(phase * 2 - position, phase * 4);
        }

        public void RefreshCopy(bool controllerConnected, bool playStation)
        {
            bool en = NightText.english;
            if (initialized && en == english && pad == controllerConnected && dualSense == playStation && departure == game.clock.departureSeconds) return;
            english = en; pad = controllerConnected; dualSense = playStation; departure = game.clock.departureSeconds; initialized = true;
            font = Resources.Load<Font>("NightBack/Fonts/" + (en ? "LilitaOne-Regular" : "ResourceHanRoundedCN-Bold"));
            string grab = pad ? (dualSense ? "□" : "X") : "E", reverse = pad ? (dualSense ? "L1" : "LB") : "R";
            string crouch = pad ? (dualSense ? "R1" : "RB") : "C", pressure = pad ? (dualSense ? "R2" : "RT") : "E";
            int seconds = Mathf.RoundToInt(FirstWarningDelay(departure)), warning = Mathf.RoundToInt(NightGame.ParentWarningSeconds);
            foreach (var n in Notes)
            {
                string title = "", body = "", footer = "";
                switch (n.id)
                {
                    case "door":
                        title = en ? "Leave quietly" : "出门要轻";
                        body = en ? "Push firmly first.\nFast creaks less.\nNear the end, ease off.\nFinish with a gentle push." : "前段快推，\n门轴声更轻。\n临近尽头，\n收力慢推。";
                        footer = en ? grab + " grab · hold " + pressure + " push\nRelease to stop · " + reverse + " reverse" : grab + " 握住 · 按住 " + pressure + " 施力\n松手停 · " + reverse + " 反向";
                        break;
                    case "hide":
                        title = en ? "Footsteps? Find cover" : "脚步近了，先躲";
                        body = en ? "Use a wall, or\ncrouch behind the sofa.\nCrouching alone\nis not cover." : "绕到墙后，或\n蹲到沙发后。\n让家具挡住你。\n只蹲下还不够。";
                        footer = en ? crouch + " crouch / stand" : crouch + " 蹲下 / 起身";
                        break;
                    case "time":
                        title = en ? "The first routine check" : "家长何时出动";
                        body = en ? (seconds == 0 ? "A quiet start: waking begins now.\n" : "A quiet start: wake-up in " + seconds + " sec.\n") + "Then " + warning + " sec before they move.\n" + (seconds == 0 ? "Noise may draw them toward you." : "Noise can wake them earlier.") : (seconds == 0 ? "安静时：开局就会翻身，\n" : "安静时：开局约 " + seconds + " 秒翻身，\n") + "再过 " + warning + " 秒开始行动。\n" + (seconds == 0 ? "响声可能引来调查。" : "响声可能让他们提前起床。");
                        footer = en ? new NightClock(departure).displayTime + " start · pauses do not count" : new NightClock(departure).displayTime + " 出发 · 暂停不计时";
                        break;
                    case "search":
                        title = en ? "Listen before searching" : "翻找前，先听";
                        body = en ? "Listen, then try the lock.\nUnlock, search, catch any fall.\nNothing here? Try another spot." : "先听脚步，再碰柜锁。\n锁开后翻找，掉东西要托住。\n没找到，就换个藏点。";
                        footer = en ? grab + " interact" : grab + " 互动";
                        break;
                    case "return":
                        title = en ? "Bring it home" : "找到，还要回去";
                        body = en ? "Got the device?\nUse cover to return.\nReach your bedroom\nto finish safely." : "拿到设备以后，\n沿遮挡返回卧室。\n安全回房才算成功。";
                        footer = en ? "Listen. Then move." : "听清，再走";
                        break;
                    case "route":
                        title = en ? "Where to look" : "去哪里翻找";
                        body = en ? "← Out to the living room\nSearch the low cabinet.\nThen: study drawers\nand the study bookcase." : "← 出门，找客厅矮柜\n再去书房找抽屉、书柜";
                        break;
                    case "living": title = en ? "Living-room cabinet" : "客厅矮柜"; body = grab + (en ? " Interact ↑" : " 互动 ↑"); break;
                    case "drawer": title = en ? "Study drawers" : "书房抽屉"; body = grab + (en ? " Interact ↑" : " 互动 ↑"); break;
                    case "shelf": title = en ? "Study bookcase" : "书房书柜"; body = grab + (en ? " Interact ↑" : " 互动 ↑"); break;
                }
                Write(n, title, body, footer);
            }
            contextCopy = null;
            RefreshContextCopy();
            RefreshFontTexture(font);
            RefreshGlyphs();
        }

        IEnumerable<Note> AllNotes()
        {
            foreach (var note in Notes) yield return note;
            foreach (var note in contextNotes) yield return note;
        }

        Renderer Square(Transform parent, Bounds textBounds, Color color, out Mesh mesh)
        {
            float size = textBounds.size.y * .88f, stroke = size * .12f;
            float x = textBounds.min.x - size * 1.32f, y = textBounds.center.y - size * .5f;
            var vertices = new List<Vector3>(); var triangles = new List<int>();
            Action<float,float,float,float> bar = (left,bottom,width,height) =>
            {
                int at = vertices.Count;
                vertices.Add(new Vector3(left,bottom,0)); vertices.Add(new Vector3(left,bottom+height,0));
                vertices.Add(new Vector3(left+width,bottom+height,0)); vertices.Add(new Vector3(left+width,bottom,0));
                triangles.AddRange(new[]{at,at+1,at+2,at,at+2,at+3});
            };
            bar(x,y,size,stroke); bar(x,y+size-stroke,size,stroke);
            bar(x,y+stroke,stroke,size-2*stroke); bar(x+size-stroke,y+stroke,stroke,size-2*stroke);
            mesh = new Mesh { name = "Outlined controller square" }; mesh.SetVertices(vertices); mesh.SetTriangles(triangles,0);
            var colors = new Color[vertices.Count]; for(int i=0;i<colors.Length;i++) colors[i]=color;
            mesh.colors=colors; mesh.uv=new Vector2[vertices.Count]; mesh.RecalculateBounds();
            var go=new GameObject("Square button · vector outline"); go.transform.SetParent(parent,false);
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=go.AddComponent<MeshRenderer>(); renderer.sharedMaterial=iconInk;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return renderer;
        }

        Note ContextNote(string id, float width, float height)
        {
            var mount=new GameObject("Surface control · "+id).transform; mount.SetParent(Visual,false);
            var note=new Note{id=id,mount=mount,size=new Vector2(width,height),immediate=true,index=-1};
            contextNotes.Add(note); return note;
        }

        Transform PhoneBox(string name, Vector3 position, Vector3 size, Material material)
        {
            var part=NightHomeArt.Box(HeldPhone,name,position,Vector3.one,"charcoal",.08f);
            part.localScale=size;
            var renderer=part.GetComponent<MeshRenderer>(); renderer.sharedMaterial=material;
            renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
            return part;
        }

        void BuildContextPrompts()
        {
            PencilPrompt=ContextNote("pencils",.70f,.36f); PencilPrompt.surfaceStretch=1.55f;
            PhonePrompt=ContextNote("phone",.285f,.21f);
            HeldPhone=new GameObject("Held phone · surface instructions").transform; HeldPhone.SetParent(Visual,false);
            phoneShellMaterial=new Material(Shader.Find("Standard")){color=new Color(.10f,.14f,.20f)};
            phoneShellMaterial.SetFloat("_Metallic",.18f); phoneShellMaterial.SetFloat("_Glossiness",.38f);
            phoneScreenMaterial=new Material(Shader.Find("Standard")){color=new Color(.055f,.11f,.13f)};
            phoneScreenMaterial.EnableKeyword("_EMISSION");phoneScreenMaterial.SetColor("_EmissionColor",new Color(.055f,.11f,.13f));
            PhoneBox("Phone body",Vector3.zero,new Vector3(.34f,.49f,.028f),phoneShellMaterial);
            PhoneBox("Phone glass",new Vector3(0,0,.016f),new Vector3(.306f,.434f,.006f),phoneScreenMaterial);
            phoneProgress=PhoneBox("Quiet hold progress",new Vector3(0,-.158f,.022f),new Vector3(.001f,.006f,.002f),phoneScreenMaterial);
            phoneProgress.GetComponent<Renderer>().sharedMaterial=NightHomeArt.Material("shade");
            PhoneBox("Phone speaker",new Vector3(0,.19f,.022f),new Vector3(.064f,.005f,.002f),phoneShellMaterial);
            PhonePrompt.mount.SetParent(HeldPhone,false);PhonePrompt.mount.localPosition=new Vector3(0,.018f,.023f);
            HeldPhone.gameObject.SetActive(false);PencilPrompt.mount.gameObject.SetActive(false);
        }

        void RefreshContextCopy()
        {
            string phase=game.mode!=null&&game.mode.type=="reaction"&&game.mode.kind=="pencils" ? (game.mode.caught?"returned":"missed") : "catch";
            string phoneState=game.phone!=null&&game.phone.state=="silenced" ? "quiet" : "pending";
            string key=english+":"+pad+":"+dualSense+":"+phase+":"+phoneState;
            if(contextCopy==key)return;contextCopy=key;
            string grab=pad?(dualSense?"□":"X"):"E", hold=pad?(dualSense?"R2":"RT"):"E";
            string move=pad?(english?"Left stick ← →":"左摇杆 ← →"):(english?"A / D":"A / D");
            Write(PencilPrompt,"",phase=="returned"?(english?"Back in the cup":"收回笔筒"):
                phase=="missed"?(english?"Some rolled away":"有笔滚落了"):
                (english?move+"\n"+hold+" hold to catch":move+"\n"+hold+" 按住接笔"),"");
            Write(PhonePrompt,"",phoneState=="quiet"?(english?"Silenced":"已静音"):
                (english?"Stop moving\n"+grab+" Hold to mute":"停下脚步\n"+grab+" 按住静音"),"");
        }

        void TickContext(float dt, Camera camera)
        {
            if(!camera)return;
            var app=NightApp.Instance;
            bool playing=app&&app.page=="playing"&&!app.paused&&game.active&&!world.overview&&!world.showcaseEnabled;
            bool pending=game.hasDevice&&game.PhonePending();
            if(playing&&dt>0)
            {
                if(phoneWasPending&&!pending&&game.phone!=null&&game.phone.state=="silenced")phoneSuccessRemaining=1.3f;
                phoneWasPending=pending;phoneSuccessRemaining=Mathf.Max(0,phoneSuccessRemaining-dt);
            }
            RefreshContextCopy();
            bool showPhone=playing&&game.mode==null&&(pending||phoneSuccessRemaining>0);
            HeldPhone.gameObject.SetActive(showPhone);
            if(showPhone)
            {
                Vector3 offset=camera.transform.TransformVector(new Vector3(.29f,-.19f,.69f));
                RaycastHit obstruction; float reach=1;
                if(Physics.SphereCast(camera.transform.position,.10f,offset.normalized,out obstruction,offset.magnitude,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore))
                    reach=Mathf.Clamp((obstruction.distance-.035f)/offset.magnitude,.25f,1);
                HeldPhone.SetPositionAndRotation(camera.transform.position+offset*reach,camera.transform.rotation*Quaternion.Euler(8,172,-5));
                HeldPhone.localScale=Vector3.one*reach;
                PhonePrompt.opacity=1;ApplyInk(PhonePrompt);
                float progress=game.phone!=null&&game.phone.state=="silenced"?1:Mathf.Clamp01(game.phone.hold/1.2f);
                phoneProgress.localScale=new Vector3(Mathf.Max(.001f,.235f*progress),.006f,.002f);
                phoneProgress.localPosition=new Vector3(-.1175f+.1175f*progress,-.158f,.022f);
            }
            var frame=world.GetIncidentFrame("pencils");
            bool resolved=game.incidentResults.Exists(result=>result.kind=="pencils");
            bool relevant=(!resolved&&(game.mode==null||game.mode.type=="search"&&game.mode.id=="study-search"))||
                game.mode!=null&&(game.mode.type=="catch"||game.mode.type=="reaction")&&game.mode.kind=="pencils";
            bool showPencil=playing&&frame&&relevant&&!showPhone;
            if(frame)
            {
                PencilPrompt.mount.SetPositionAndRotation(frame.TransformPoint(new Vector3(-.36f,.094f,.16f)),frame.rotation*Quaternion.LookRotation(Vector3.up,Vector3.back));
                int layer=game.mode!=null?NightWorld.DetailLayer:0;
                foreach(var child in PencilPrompt.mount.GetComponentsInChildren<Transform>(true))child.gameObject.layer=layer;
                showPencil &= Vector3.Distance(camera.transform.position,PencilPrompt.mount.position)<3.4f;
            }
            PencilPrompt.mount.gameObject.SetActive(showPencil);
            if(showPencil){PencilPrompt.opacity=1;ApplyInk(PencilPrompt);}
        }

        void RefreshFontTexture(Font rebuilt)
        { if (font && rebuilt == font && ink) { ink.mainTexture = font.material.mainTexture; fontDirty = true; } }

        void RefreshGlyphs()
        {
            if (!fontDirty || !font) return;
            fontDirty = false;
            // Request the complete set together, so a dynamic atlas rebuild cannot drop earlier lines.
            var all = new System.Text.StringBuilder();
            foreach (var n in AllNotes()) foreach (var l in n.lines) all.Append(l.text.text);
            font.RequestCharactersInTexture(all.ToString(), 96, FontStyle.Normal);
            ink.mainTexture = font.material.mainTexture;
            foreach (var n in AllNotes()) foreach (var l in n.lines)
            {
                string value = l.text.text; l.text.text = ""; l.text.text = value;
                var b = l.renderer.localBounds;
                if (l.icon) b.Encapsulate(l.icon.localBounds);
                l.properties.SetFloat("_MinX", b.min.x); l.properties.SetFloat("_MaxX", b.max.x);
            }
        }

        void FollowSurfaces()
        {
            foreach (var n in Notes) if (n.surface)
                n.mount.SetPositionAndRotation(n.surface.TransformPoint(n.surfacePosition), n.surface.rotation);
        }
        public float Progress(Note n) { return n.immediate ? 1 : game.firstNightHintProgress[n.index]; }
        public bool CanRead(Note n, Camera camera)
        {
            if (!camera || !Visual.gameObject.activeInHierarchy) return false;
            Vector3 toEye = camera.transform.position - n.mount.position;
            if (toEye.magnitude > ReadDistance || Vector3.Dot(toEye.normalized, n.mount.forward) < .12f) return false;
            var bounds = new Bounds(n.mount.position, Vector3.zero);
            foreach (var line in n.lines) bounds.Encapsulate(line.renderer.bounds);
            if (!GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera), bounds)) return false;
            Vector3 target = n.mount.position + n.mount.forward * .035f;
            return !Physics.Linecast(camera.transform.position, target, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        }
        void ApplyInk(Note n)
        {
            float elapsed = Progress(n) * n.duration;
            foreach (var line in n.lines)
            {
                line.properties.SetFloat("_Reveal", Mathf.Clamp01((elapsed - line.start) / line.duration));
                line.properties.SetFloat("_Opacity", n.opacity * (n.direction ? .69f : .86f));
                line.renderer.SetPropertyBlock(line.properties);
                if (line.icon) line.icon.SetPropertyBlock(line.properties);
            }
        }
        public void RefreshVisibility()
        {
            FollowSurfaces();
            var app = NightApp.Instance;
            bool exploring = app && (app.page == "playing" || app.page == "pause" || app.page == "settings");
            if (Visual) Visual.gameObject.SetActive(exploring && !world.overview && !world.showcaseEnabled);
        }
        public void Tick(float dt, Camera camera)
        {
            RefreshVisibility();
            TickContext(dt, camera);
            RefreshGlyphs();
            var app = NightApp.Instance;
            if (dt <= 0 || !camera || !app || app.page != "playing" || app.paused || !game.active || game.IsCinematic || !Visual.gameObject.activeInHierarchy) return;
            Note writing = null; float best = float.MaxValue;
            foreach (var n in Notes)
            {
                if (Progress(n) >= 1 || !CanRead(n, camera)) continue;
                float score = Vector3.Angle(camera.transform.forward, n.mount.position - camera.transform.position);
                if (score < best) { best = score; writing = n; }
            }
            foreach (var n in Notes)
            {
                if (n == writing) game.firstNightHintProgress[n.index] = Mathf.Min(1, Progress(n) + dt / n.duration);
                float distance = Vector3.Distance(camera.transform.position, n.mount.position);
                float visibility = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(3.75f, 1.65f, distance));
                n.opacity = Mathf.MoveTowards(n.opacity, visibility, dt * 1.6f);
                ApplyInk(n);
            }
        }
        void LateUpdate()
        {
            var app = NightApp.Instance;
            RefreshCopy(app && app.controller != null && app.controller.connected, app && app.controller != null && app.controller.dualSense);
            Tick(Time.deltaTime, app ? app.viewCamera : null);
        }
        void OnDestroy()
        {
            Font.textureRebuilt -= RefreshFontTexture;
            foreach (var n in AllNotes()) foreach (var l in n.lines) if (l.iconMesh) Destroy(l.iconMesh);
            foreach (var material in new[] { ink, iconInk, phoneShellMaterial, phoneScreenMaterial })
                if (material) { if (Application.isPlaying) Destroy(material); else DestroyImmediate(material); }
        }
    }
}
