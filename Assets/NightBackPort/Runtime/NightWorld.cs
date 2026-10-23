// @role Builds and synchronizes the 3D household
// @calls NightBedroomTitle NightFirstNightHints NightHomeArt NightHomeLighting

using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Native Unity meshes generated from the current local Three.js scene.
    // The data and visuals use identical x/z coordinates. This component never reads input or creates a camera.
    public sealed class NightWorld : MonoBehaviour
    {
        public Transform playerVisual, parentVisual, catVisual, lockVisual;
        public readonly Dictionary<string, Transform> furnitureVisuals = new Dictionary<string, Transform>();
        public readonly List<Transform> doorVisuals = new List<Transform>();
        public static readonly string[] SkinIds = { "golden-frog", "golden-bull", "nailong", "naiwa", "naishu", "scarf", "crab", "chef", "box", "pillow", "nightcap" };
        public static readonly string[] SkinNames = { "金黄圆肚蛙", "金黄小牛", "奶龙", "奶蛙", "奶鼠", "围巾夜行", "螃蟹侦察员", "偷吃小厨师", "纸箱潜行员", "抱枕骑士", "晚安星星" };
        public string playerSkin = "scarf", parentSkin = "scarf";
        public bool overview = true;
        public int builtLevel = -1;

        Transform worldRoot, fullWalls, overviewWalls, roof, chargerLamp, toyVisual;
        Transform playerBody, parentBody, catBody, catTail, rescueHands, searchHands, tickleHands, lockBody, sleepSymbols, celebration, minuteHand, hourHand, phoneVisual, stepRing;
        public const int DetailLayer = 30;
        public const int DetailMask = 1 << DetailLayer;
        public bool showcaseEnabled { get; private set; }
        public Vector3 ShowcaseCenter { get { return new Vector3(60, .95f, 0); } }
        public Vector3 ShowcaseCameraPosition
        {
            get
            {
                // Elevation changes only the portrait camera. Horizontal dragging turns its independent actor group,
                // keeping the asymmetric UI composition in place while the real world and studio lights stay put.
                return ShowcaseCenter + Quaternion.Euler(-showcaseOrbitPitch, 0, 0) * new Vector3(.25f, .8f, 6.6f);
            }
        }
        Transform closeupFurniture, closeupFloor, showcaseRoot, showcaseOrbitGroup, showcasePlayer, showcaseParent, showcaseCat;
        Light closeupKey, closeupRim;
        Rig showcasePlayerRig, showcaseParentRig;
        bool showcaseSelected, savedFog;
        string showcasePlayerId, showcaseParentId;
        float showcaseOrbitYaw, showcaseOrbitPitch;
        readonly Dictionary<GameObject, bool> showcaseHidden = new Dictionary<GameObject, bool>();
        readonly Dictionary<string, Transform> lockParts = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> rescueParts = new Dictionary<string, Transform>();
        readonly Dictionary<string, Transform> catBones = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Pose> catRest = new Dictionary<Transform, Pose>();
        ClipData[] catAnimations; string catClip; float catClipAge;
        readonly List<Transform> catFeet = new List<Transform>();
        readonly List<LightState> localLights = new List<LightState>();
        readonly Dictionary<string, FurnitureState> furniture = new Dictionary<string, FurnitureState>();
        readonly Dictionary<string, Transform> named = new Dictionary<string, Transform>();
        readonly Dictionary<Transform, Pose> rest = new Dictionary<Transform, Pose>();
        readonly List<Transform> doorHands = new List<Transform>();
        readonly List<Mesh> ownedSkinMeshes = new List<Mesh>();
        Rig playerRig, parentRig;
        Light moon, parentLamp, tutorialLamp, timeLamp;
        Vector3 previousPlayer, previousParent, previousCat;
        float previousTime, walkPhase, parentPhase;
        int phase = -1;
        static readonly Dictionary<string, Mesh> meshes = new Dictionary<string, Mesh>();
        static Material[] materials;

        // Flat hierarchy avoids JsonUtility's depth limit truncating the 30/38-bone authored rigs.
        [Serializable] class FileData { public int rootId; public Node[] nodes; }
        [Serializable] class Node
        {
            public int id, parent; public string name, geometry; public Vector3 position, scale;
            public Quaternion rotation; public bool active = true, bone;
            public int[] materials, bones; public MatrixData[] bindposes; public LightData light;
        }
        [Serializable] class MatrixData { public float[] values; }
        [Serializable] class LightData { public Color color; public float power, range, angle; public string type; }
        [Serializable] class MaterialData
        {
            public string name, texture, normal; public Color color, emission;
            public float emissionPower, metallic, roughness, normalScale, alphaCutoff; public bool doubleSided, vertexColors;
        }
        [Serializable] class MaterialFile { public MaterialData[] materials; }
        [Serializable] class AnimationFile { public ClipData[] clips; }
        [Serializable] class ClipData { public string name; public float duration; public TrackData[] tracks; }
        [Serializable] class TrackData { public string name, type; public float[] times, values; }
        struct Pose { public Vector3 position, scale; public Quaternion rotation; }
        class LightState { public Light light; public float intensity; }
        class FurnitureState
        {
            public Transform root, padlock, frame, item, lid, flowers;
            public string kind; public readonly List<Transform> drawers = new List<Transform>(), books = new List<Transform>(), pencils = new List<Transform>();
        }
        class Rig
        {
            public string id; public readonly Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            public readonly Dictionary<string, Quaternion> rotations = new Dictionary<string, Quaternion>();
        }

        public NightBedroomTitle bedroomTitle;
        public NightFirstNightHints firstNightHints;
        public Transform FindFurniture(string id) { Transform t; return furnitureVisuals.TryGetValue(id, out t) ? t : null; }
        public Transform GetDoorTransform(int index) { return index >= 0 && index < doorVisuals.Count ? doorVisuals[index] : null; }
        public Transform FindVisual(string name) { Transform t; return named.TryGetValue(name, out t) ? t : null; }
        public Transform GetIncidentFrame(string kind) { foreach (var f in furniture.Values) if (f.kind == kind) return f.frame; return null; }
        public Transform GetIncidentObject(string kind) { foreach (var f in furniture.Values) if (f.kind == kind) return f.item; return null; }

        public void Build(NightGame game)
        {
            if (game == null || game.level == null) return;
            Clear(); builtLevel = game.levelIndex;
            worldRoot = new GameObject("Native Night Back World").transform;
            worldRoot.SetParent(transform, false);
            Transform house = Load("NightBack/world" + builtLevel, worldRoot, true);
            NightHomeArt.Apply(house, game);
            ScanHouse(house);
            if(!game.IsTutorial)TrimReplacedBedroomSurfaces(house);
            AddColliders(game.level);
            BuildActors();
            catVisual = Load("NightBack/cat", worldRoot, false);
            foreach (Transform t in catVisual.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Cat:body") catBody = t;
                if (t.name == "Cat:tail") catTail = t;
                if (t.name.StartsWith("CatLeg:")) catFeet.Add(t);
                if (t.name == "Body" || t.name == "Head" || t.name == "Head2" || t.name == "Head3") { catBones[t.name] = t; catRest[t] = new Pose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale }; }
            }
            catAnimations = JsonUtility.FromJson<AnimationFile>(Resources.Load<TextAsset>("NightBack/cat-animation").text).clips;
            toyVisual = Load("NightBack/cat-toy", worldRoot, false); toyVisual.gameObject.SetActive(false);
            BuildCloseups(); BuildComicDetails(game.level);
            ConfigureLights();
            foreach(var artLight in NightHomeLighting.Build(house, game)) localLights.Add(new LightState { light=artLight, intensity=artLight.intensity });
            if(!game.IsTutorial){var titleRoot=new GameObject("Bedroom Title - Shared Map");titleRoot.transform.SetParent(worldRoot,false);bedroomTitle=titleRoot.AddComponent<NightBedroomTitle>();bedroomTitle.Build(game,this);}else bedroomTitle=null;
            firstNightHints = null;
            if (!game.IsTutorial) { var hints = new GameObject("First Night Hints"); hints.transform.SetParent(worldRoot, false); firstNightHints = hints.AddComponent<NightFirstNightHints>(); firstNightHints.Build(this, game); }
            previousPlayer = game.player; previousParent = game.parent.position; previousCat = game.cat.position; previousTime = game.time;
            SetOverview(overview); Sync(game);
        }

        void Clear()
        {
            // Private caches are intentionally not serialized. A saved preview can survive a domain reload.
            var oldWorlds = new List<GameObject>();
            foreach (Transform child in transform) if (child.name == "Native Night Back World") oldWorlds.Add(child.gameObject);
            foreach (GameObject old in oldWorlds) { old.SetActive(false); DestroySafe(old); }
            foreach (Mesh m in ownedSkinMeshes) if (m) DestroySafe(m);
            ownedSkinMeshes.Clear(); furniture.Clear(); furnitureVisuals.Clear(); named.Clear(); rest.Clear(); doorVisuals.Clear(); doorHands.Clear(); localLights.Clear(); catFeet.Clear(); lockParts.Clear(); rescueParts.Clear(); catBones.Clear(); catRest.Clear(); catClip = null; catClipAge = 0; showcaseHidden.Clear(); showcaseEnabled = false; showcaseRoot = showcaseOrbitGroup = closeupFurniture = null;
            playerVisual = parentVisual = catVisual = null; catBody = catTail = null; fullWalls = overviewWalls = roof = chargerLamp = null;
            walkPhase = parentPhase = 0; phase = -1;
        }
        static void DestroySafe(UnityEngine.Object o)
        {
            if (!o) return;
#if UNITY_EDITOR
            // The authoring tool persists preview meshes/materials. Rebuilding must never delete those assets.
            if (UnityEditor.AssetDatabase.Contains(o)) return;
#endif
            if (Application.isPlaying) Destroy(o); else DestroyImmediate(o);
        }
        void OnDestroy() { foreach (Mesh m in ownedSkinMeshes) if (m) DestroySafe(m); ownedSkinMeshes.Clear(); }

        void ScanHouse(Transform house)
        {
            foreach (Transform t in house.GetComponentsInChildren<Transform>(true))
            {
                if (!named.ContainsKey(t.name)) named[t.name] = t;
                rest[t] = new Pose { position = t.localPosition, rotation = t.localRotation, scale = t.localScale };
                if (t.name == "FullWalls") fullWalls = t; else if (t.name == "OverviewWalls") overviewWalls = t; else if (t.name == "Roof") roof = t; else if (t.name == "ChargerLamp") chargerLamp = t;
                else if (t.name.StartsWith("Furniture:"))
                {
                    string id = t.name.Substring(10); furnitureVisuals[id] = t;
                    var f = new FurnitureState { root = t }; furniture[id] = f;
                    foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
                    {
                        if (c.name.StartsWith("Drawer:")) f.drawers.Add(c);
                        else if (c.name.StartsWith("Book:")) f.books.Add(c);
                        else if (c.name == "Padlock") f.padlock = c;
                        else if (c.name.StartsWith("IncidentFrame:")) { f.frame = c; f.kind = c.name.Substring(14); }
                        else if (c.name.StartsWith("IncidentObject:")) f.item = c;
                        else if (c.name.StartsWith("Pencil:")) f.pencils.Add(c);
                        else if (c.name == "TinLid") f.lid = c; else if (c.name == "Flowers") f.flowers = c;
                    }
                }
                else if (t.name.StartsWith("Door:"))
                {
                    int index; if (int.TryParse(t.name.Substring(5), out index)) { while (doorVisuals.Count <= index) doorVisuals.Add(null); doorVisuals[index] = t; }
                }
                else if (t.name == "DoorHands") { doorHands.Add(t); t.gameObject.SetActive(false); }
            }
        }

        void TrimReplacedBedroomSurfaces(Transform house)
        {
            // Source meshes are shared with every night. Clone only affected static batches;
            // do not change the cached originals or the remaining rooms.
            foreach(var filter in house.GetComponentsInChildren<MeshFilter>(true)){
                bool decoration=filter.transform.parent==house;
                bool frame=filter.transform.parent.name=="Roof";
                if(!decoration&&!frame)continue;
                var source=filter.sharedMesh;if(!source)continue;
                var vertices=source.vertices;var indices=source.triangles;var keep=new List<int>(indices.Length);
                for(int i=0;i<indices.Length;i+=3){
                    var c=filter.transform.TransformPoint((vertices[indices[i]]+vertices[indices[i+1]]+vertices[indices[i+2]])/3f);
                    bool replaced=decoration&&c.x>.50f&&c.x<6.50f&&c.z>10.45f&&c.z<13.55f&&c.y>.12f;
                    replaced|=frame&&c.x>2.35f&&c.x<3.65f&&c.z>9.40f&&c.z<10.65f&&c.y<2.39f;
                    if(!replaced){keep.Add(indices[i]);keep.Add(indices[i+1]);keep.Add(indices[i+2]);}
                }
                if(keep.Count<indices.Length){var copy=Instantiate(source);copy.name=source.name+" / bedroom replaced";copy.triangles=keep.ToArray();copy.RecalculateBounds();filter.sharedMesh=copy;ownedSkinMeshes.Add(copy);}
                var bounds=filter.GetComponent<Renderer>().bounds;
                if(decoration&&bounds.min.x>.5f&&bounds.max.x<6.5f&&bounds.min.z>10.5f&&bounds.max.z<13.5f&&bounds.size.y<.05f&&bounds.center.y>.024f)filter.GetComponent<Renderer>().enabled=false;
            }
        }

        void AddColliders(NightLevel level)
        {
            Transform physical = new GameObject("Shared Layout Colliders").transform; physical.SetParent(worldRoot, false);
            foreach (Vector2 w in level.walls) ColliderBox(physical, "Wall", new Vector3(w.x, 1.3f, w.y), new Vector3(1.005f, 2.6f, 1.005f));
            foreach (Vector2 p in level.floors) ColliderBox(physical, "Floor", new Vector3(p.x, -.025f, p.y), new Vector3(.975f, .075f, .975f));
            foreach (NightFurniture f in level.furniture)
            {
                Transform t = FindFurniture(f.id); if (!t) continue;
                BoxCollider c = t.gameObject.AddComponent<BoxCollider>(); c.center = new Vector3(0, f.h * .5f, 0); c.size = new Vector3(f.w, f.h, f.d);
            }
            foreach (Transform d in doorVisuals) if (d) { BoxCollider c = d.gameObject.AddComponent<BoxCollider>(); c.center = new Vector3(.47f, 1.08f, 0); c.size = new Vector3(.94f, 2.16f, .12f); }
        }
        static void ColliderBox(Transform parent, string name, Vector3 center, Vector3 size)
        { var g = new GameObject(name); g.transform.SetParent(parent, false); g.transform.localPosition = center; g.AddComponent<BoxCollider>().size = size; }

        public void SetOverview(bool enabled)
        {
            overview = enabled;
            if(firstNightHints)firstNightHints.RefreshVisibility();
            if(bedroomTitle)bedroomTitle.SetOverview(enabled);
            if (fullWalls) fullWalls.gameObject.SetActive(!enabled);
            if (overviewWalls) overviewWalls.gameObject.SetActive(enabled);
            if (roof) roof.gameObject.SetActive(!enabled);
        }
        public void SetSkins(string player, string parent)
        {
            player = ValidSkin(player); parent = ValidSkin(parent);
            bool changed = player != playerSkin || parent != parentSkin;
            playerSkin = player; parentSkin = parent;
            if (worldRoot && changed) BuildActors();
            if (showcaseEnabled && showcaseSelected && changed) RebuildShowcaseActors();
        }
        static string ValidSkin(string id) { return Array.IndexOf(SkinIds, id) >= 0 ? id : "scarf"; }
        void BuildActors()
        {
            Vector3 p = playerVisual ? playerVisual.position : Vector3.zero, q = parentVisual ? parentVisual.position : Vector3.zero;
            if (playerVisual) DestroySafe(playerVisual.gameObject); if (parentVisual) DestroySafe(parentVisual.gameObject);
            playerVisual = new GameObject("Player Visual").transform; playerVisual.SetParent(worldRoot, false); playerVisual.position = p;
            parentVisual = new GameObject("Parent Visual").transform; parentVisual.SetParent(worldRoot, false); parentVisual.position = q; parentVisual.localScale = Vector3.one * 1.12f;
            playerBody = new GameObject("Player Body").transform; playerBody.SetParent(playerVisual, false);
            parentBody = new GameObject("Parent Body").transform; parentBody.SetParent(parentVisual, false);
            playerRig = BuildRig(playerSkin, playerBody); parentRig = BuildRig(parentSkin, parentBody, true);
            phoneVisual = DecorBox(playerBody, "Recovered Phone", new Vector3(.30f, .45f, .20f), new Vector3(.15f, .26f, .035f), new Color(.16f, .20f, .29f));
            // The screen is a sibling so primitive scaling never scales its authored dimensions twice.
            Transform screen = DecorBox(playerBody, "Phone Screen", new Vector3(.30f, .45f, .226f), new Vector3(.11f, .19f, .015f), new Color(.61f, .80f, .78f)); screen.SetParent(phoneVisual, true);
            phoneVisual.gameObject.SetActive(false);
            CreateRing(playerVisual, "Player Ground Ring", .32f, .37f, new Color(.97f, .82f, .55f)).localPosition = new Vector3(0, .045f, 0);
        }
        Rig BuildRig(string id, Transform parent, bool parentRole = false)
        {
            var rig = new Rig { id = ValidSkin(id) }; Transform root = Load("NightBack/skin-" + rig.id, parent, false);
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (!rig.bones.ContainsKey(t.name)) { rig.bones[t.name] = t; rig.rotations[t.name] = t.localRotation; }
            if (parentRole && rig.id == "scarf") foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
                if (renderer.name.ToLowerInvariant().Contains("clothes") || renderer.name.ToLowerInvariant().Contains("scarf"))
                { Material[] tinted = renderer.sharedMaterials; for (int i = 0; i < tinted.Length; i++) { tinted[i] = new Material(tinted[i]); tinted[i].color *= new Color(.675f, .65f, .847f); } renderer.sharedMaterials = tinted; }
            return rig;
        }

        void ConfigureLights()
        {
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.42f, .48f, .62f);
            RenderSettings.fog = true; RenderSettings.fogMode = FogMode.ExponentialSquared; RenderSettings.fogColor = new Color(.065f, .10f, .18f); RenderSettings.fogDensity = .014f;
            moon = NewLight("Moon", LightType.Directional, new Color(.68f, .8f, 1), .75f, 60);
            moon.transform.rotation = Quaternion.Euler(50, -35, 0); moon.shadows = LightShadows.Soft; moon.shadowNormalBias = .04f;
            parentLamp = NewLight("Parent Flashlight", LightType.Spot, new Color(1, .82f, .55f), 2f, 7);
            parentLamp.spotAngle = 58; parentLamp.shadows = LightShadows.Soft;
            tutorialLamp = NewLight("Practice Warm Guidance", LightType.Spot, new Color(1, .77f, .43f), 3f, 6);
            tutorialLamp.spotAngle = 74; tutorialLamp.shadows = LightShadows.Soft;
            timeLamp = NewLight("Timed Hall Lamp", LightType.Point, new Color(1, .77f, .46f), 1.6f, 6);
            timeLamp.transform.position = new Vector3(10.7f, 2.25f, 8.8f); timeLamp.shadows = LightShadows.Soft;
        }
        Light NewLight(string name, LightType type, Color color, float power, float range)
        { var o = new GameObject(name); o.transform.SetParent(worldRoot, false); var l = o.AddComponent<Light>(); l.type = type; l.color = color; l.intensity = power; l.range = range; l.renderMode = LightRenderMode.Auto; l.shadowBias = .035f; return l; }

        public void Sync(NightGame game)
        {
            if (game == null) return; if (!worldRoot || builtLevel != game.levelIndex) { Build(game); return; }
            float dt = Mathf.Clamp(game.time - previousTime, 0, .08f); previousTime = game.time;
            float walked = Vector3.Distance(game.player, previousPlayer), parentWalked = Vector3.Distance(game.parent.position, previousParent);
            bool moving = walked > .0001f, parentMoving = parentWalked > .0001f;
            walkPhase += walked * 8; parentPhase += parentWalked * 6;
            previousPlayer = game.player; previousParent = game.parent.position;
            playerVisual.position = game.player;
            playerVisual.rotation = Quaternion.Euler(0, game.heading * Mathf.Rad2Deg, 0);
            playerBody.localScale = new Vector3(1, game.crouched ? .65f : 1, 1);
            playerBody.localPosition = new Vector3(0, moving ? Mathf.Abs(Mathf.Sin(walkPhase)) * .025f : game.status == "won" ? Mathf.Abs(Mathf.Sin(Time.unscaledTime * 6)) * .12f : 0, 0);
            playerBody.localRotation = Quaternion.Euler(game.crouched ? -7 : 0, 0, 0);
            AnimateRig(playerRig, game.time, walkPhase, moving, game.crouched, game.mode != null && game.mode.type == "door" ? .55f : game.mode != null && (game.mode.type == "catch" || game.mode.type == "rescue") ? 1 : 0, false);
            phoneVisual.gameObject.SetActive(game.hasDevice);
            bool sleeping = game.parent.state == "sleep" || game.parent.state == "alert";
            float wake = sleeping ? 0 : game.parent.state == "warning" ? Mathf.Clamp01((NightGame.ParentWarningSeconds - game.parent.timer) / 2) : 1;
            parentVisual.position = Vector3.Lerp(game.level.parentBed, game.parent.position, wake);
            parentVisual.rotation = Quaternion.Euler(0, sleeping ? 0 : game.parent.heading * Mathf.Rad2Deg, 0);
            parentBody.localPosition = new Vector3(0, .84f * (1 - wake), .78f * (1 - wake));
            parentBody.localRotation = Quaternion.Euler(-90 * (1 - wake), 0, sleeping ? Mathf.Sin(game.time * 17) * Mathf.Min(4, game.tickleHeat * 3) : 0);
            parentBody.localScale = new Vector3(1, sleeping ? 1 + Mathf.Sin(game.time * 1.5f) * .012f : 1, 1);
            AnimateRig(parentRig, game.time, parentPhase, parentMoving, false, 0, sleeping, game.tickleHeat);
            for (int i = 0; i < doorVisuals.Count && i < game.doors.Count; i++)
            {
                Transform t = doorVisuals[i]; NightDoor d = game.doors[i]; if (!t) continue;
                t.localPosition = new Vector3(d.x - .47f, 0, d.z); t.localRotation = Quaternion.Euler(0, d.progress * 88.2f, 0);
                Transform hands = FindDescendant(t, "DoorHands");
                if (hands)
                {
                    bool touching = game.mode != null && game.mode.type == "door" && game.mode.index == i;
                    hands.gameObject.SetActive(touching);
                    float side = game.player.z >= d.z ? 1 : -1;
                    hands.localScale = new Vector3(1, 1, side); hands.localPosition = new Vector3(0, 0, side * .14f);
                }
            }
            SyncFurniture(game); SyncCat(game, dt); SyncLights(game); SyncCloseups(game); SyncComicDetails(game);
            if (chargerLamp) chargerLamp.gameObject.SetActive(game.levelIndex != 3 && !game.hasDevice);
            if (stepRing) { bool stepping = game.mode != null && game.mode.type == "step"; stepRing.gameObject.SetActive(stepping); if (stepping) stepRing.position = game.mode.target + Vector3.up * .045f; }
        }

        void SyncFurniture(NightGame game)
        {
            foreach (var pair in furniture)
            {
                FurnitureState f = pair.Value; NightMode m = game.mode;
                bool searching = m != null && m.type == "search" && m.id == pair.Key;
                float open = searching ? Mathf.Min(1, m.elapsed / .85f) * .42f : 0;
                foreach (Transform t in f.drawers) t.localPosition = rest[t].position + Vector3.forward * open;
                for (int i = 0; i < f.books.Count; i++) { Transform t = f.books[i]; float pull = searching ? Mathf.Max(0, Mathf.Sin(m.elapsed * 3 - i * .65f)) : 0; t.localPosition = rest[t].position + Vector3.forward * pull * .28f; t.localRotation = rest[t].rotation * Quaternion.Euler(-pull * 13.75f, 0, 0); }
                if (f.padlock)
                {
                    bool unlocked = false; foreach (NightSpot s in game.spots) if (s.id == pair.Key) { unlocked = s.unlocked || s.searched; break; }
                    f.padlock.gameObject.SetActive(!unlocked && game.levelIndex != 3);
                }
                if (f.item)
                {
                    Pose p = rest[f.item]; f.item.localPosition = p.position; f.item.localRotation = p.rotation;
                    if (f.lid) { f.lid.localPosition = rest[f.lid].position; f.lid.localRotation = rest[f.lid].rotation; }
                    bool handling = m != null && (m.type == "catch" || m.type == "reaction") && m.kind == f.kind;
                    NightRescue r = handling ? game.rescue : null;
                    if (!handling && game.incidentResults != null)
                    {
                        foreach (var result in game.incidentResults) if (result.kind == f.kind) { r = result.rescue; m = new NightMode { type = "reaction", kind = result.kind, caught = result.success, elapsed = 1.25f }; break; }
                    }
                    if (r != null && m != null)
                    {
                        NightFurniture definition = null; foreach (var d in game.level.furniture) if (d.id == pair.Key) { definition = d; break; }
                        VisualPose pose = IncidentPose(r, m, definition, p.position);
                        f.item.localPosition = pose.position; f.item.localRotation = Quaternion.Euler(pose.euler);
                        if (f.flowers) f.flowers.localRotation = Quaternion.Euler(0, 0, -pose.euler.z * .22f + Mathf.Sin((m.type == "reaction" ? r.sourceElapsed + m.elapsed : m.elapsed) * 8) * 2 * (m.type == "reaction" ? Mathf.Exp(-m.elapsed * 5) : r.stage == "reach" ? 1 : .1f));
                        if (f.lid) { float damp = r.left > .12f ? 0 : m.type == "reaction" ? Mathf.Exp(-m.elapsed * 6) : 1; f.lid.localPosition = rest[f.lid].position + Vector3.up * Mathf.Abs(Mathf.Sin(m.elapsed * 23)) * .045f * damp; f.lid.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(m.elapsed * 20) * 3.4f * damp); }
                        for (int i = 0; i < f.pencils.Count; i++) if (r.pencils != null && i < r.pencils.Length) { VisualPose pencil = PencilPose(r.pencils[i], i, r, m, definition.h); f.pencils[i].localPosition = pencil.position; f.pencils[i].localRotation = Quaternion.Euler(pencil.euler); }
                    }
                }
            }
        }
        void SyncCat(NightGame game, float dt)
        {
            if (!catVisual || game.cat == null) return; var c = game.cat;
            bool walking = Vector3.Distance(c.position, previousCat) > .0001f; previousCat = c.position;
            AnimateCat(walking ? "Walk" : c.state == "play" ? "Bite_Front" : c.state == "jump" ? "Jump" : "Idle", dt);
            catVisual.position = c.position + Vector3.up * (c.state == "jump" ? Mathf.Sin(Mathf.Clamp01(c.timer / .55f) * Mathf.PI * .5f) * .7f : 0);
            catVisual.rotation = Quaternion.Slerp(catVisual.rotation, Quaternion.Euler(0, c.heading * Mathf.Rad2Deg, 0), dt > 0 ? Mathf.Min(1, dt * 14) : 1);
            if (catBody) { catBody.localRotation = Quaternion.Euler(0, 0, c.state == "rub" ? Mathf.Sin(game.time * 3) * 9 : 0); catBody.localScale = new Vector3(1, c.state == "prepare" ? .7f : 1, 1); }
            if (catTail) catTail.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(game.time * 2) * 10);
            for (int i = 0; i < catFeet.Count; i++) catFeet[i].localRotation = Quaternion.Euler(walking ? Mathf.Sin(game.time * 11 + (i == 0 || i == 3 ? 0 : Mathf.PI)) * 25 : 0, 0, 0);
            if (toyVisual && game.catMemory != null)
            {
                var memory = game.catMemory; toyVisual.gameObject.SetActive(memory.hasToy);
                if (memory.hasToy) { float u = Mathf.Clamp01(memory.toyAge / .5f); toyVisual.position = Vector3.Lerp(memory.toyFrom, memory.toy, u) + Vector3.up * (.07f + Mathf.Sin(u * Mathf.PI) * .35f); toyVisual.localRotation = Quaternion.Euler(game.time * 170, 0, 0); }
            }
            if (game.lures != null) foreach (var lure in game.lures)
            {
                Transform t; if (!named.TryGetValue("Lure:" + lure.id, out t)) continue;
                bool playing = lure.armed && !lure.done && lure.pulses > 0;
                t.localRotation = rest[t].rotation * Quaternion.Euler(0, 0, playing ? Mathf.Sin(game.time * 23) * 2 : 0);
                Transform lamp = FindDescendant(t, "LureLamp"); if (lamp) foreach (var renderer in lamp.GetComponentsInChildren<Renderer>()) { var block = new MaterialPropertyBlock(); block.SetColor("_EmissionColor", playing ? new Color(1, .35f, .05f) : Color.black); renderer.SetPropertyBlock(block); }
            }
        }
        void AnimateCat(string clipName, float dt)
        {
            if (catAnimations == null) return;
            if (catClip != clipName) { catClip = clipName; catClipAge = 0; } else catClipAge += dt;
            ClipData clip = null; foreach (var candidate in catAnimations) if (candidate.name == clipName) { clip = candidate; break; }
            if (clip == null) return; float clock = clip.duration > 0 ? catClipAge % clip.duration : 0;
            var targets = new Dictionary<Transform, Pose>(catRest);
            foreach (TrackData track in clip.tracks)
            {
                int dot = track.name.LastIndexOf('.'); if (dot < 0 || track.times.Length == 0) continue;
                Transform bone; if (!catBones.TryGetValue(track.name.Substring(0, dot), out bone)) continue;
                string property = track.name.Substring(dot + 1); int a = 0;
                while (a + 1 < track.times.Length && track.times[a + 1] <= clock) a++;
                int b = Mathf.Min(a + 1, track.times.Length - 1); float blend = b == a ? 0 : Mathf.InverseLerp(track.times[a], track.times[b], clock); Pose pose = targets[bone];
                if (track.type == "quaternion")
                {
                    var qa = new Quaternion(track.values[a * 4], track.values[a * 4 + 1], track.values[a * 4 + 2], track.values[a * 4 + 3]);
                    var qb = new Quaternion(track.values[b * 4], track.values[b * 4 + 1], track.values[b * 4 + 2], track.values[b * 4 + 3]); pose.rotation = Quaternion.Slerp(qa, qb, blend);
                }
                else if (track.type == "vector")
                {
                    Vector3 v = Vector3.Lerp(new Vector3(track.values[a * 3], track.values[a * 3 + 1], track.values[a * 3 + 2]), new Vector3(track.values[b * 3], track.values[b * 3 + 1], track.values[b * 3 + 2]), blend);
                    if (property == "position") pose.position = v; else if (property == "scale") pose.scale = v;
                }
                targets[bone] = pose;
            }
            float fade = dt > 0 ? Mathf.Min(1, dt * 14) : 1;
            foreach (var pair in targets) { pair.Key.localPosition = Vector3.Lerp(pair.Key.localPosition, pair.Value.position, fade); pair.Key.localRotation = Quaternion.Slerp(pair.Key.localRotation, pair.Value.rotation, fade); pair.Key.localScale = Vector3.Lerp(pair.Key.localScale, pair.Value.scale, fade); }
        }
        void SyncLights(NightGame game)
        {
            float factor = game.clock == null ? 1 : game.clock.lightFactor;
            bool practice = game.levelIndex == 3;
            if(bedroomTitle)bedroomTitle.SetLightFactor(factor);
            RenderSettings.ambientLight = new Color(.42f, .48f, .62f) * factor * (practice ? .85f : 1);
            moon.intensity = .75f * factor * (practice ? .7f : 1);
            foreach (LightState l in localLights) if (l.light) l.light.intensity = l.intensity * factor;
            bool checking = game.parent.state == "checking" || game.parent.state == "returning";
            parentLamp.enabled = checking; parentLamp.transform.position = game.parent.position + Vector3.up * 1.25f;
            parentLamp.transform.rotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(game.parent.heading), -.18f, Mathf.Cos(game.parent.heading)));
            timeLamp.enabled = !practice && game.clock != null && (game.clock.phaseIndex == 2 || game.clock.inspectionSoon);
            if (game.clock != null && phase != game.clock.phaseIndex) phase = game.clock.phaseIndex;
            tutorialLamp.enabled = practice && game.active && game.tutorialStep < 10;
            if (tutorialLamp.enabled)
            {
                Vector3 target = game.tutorialStep <= 5 ? new Vector3(3, 1, 12) : game.tutorialStep <= 7 ? new Vector3(1.3f, 1, 8) : game.tutorialStep == 8 ? new Vector3(3, .2f, 5.3f) : new Vector3(3, 1.4f, 1.1f);
                tutorialLamp.transform.position = target + new Vector3(.3f, 1.3f, 1.1f); tutorialLamp.transform.LookAt(target);
            }
        }
        static void AnimateRig(Rig rig, float time, float phase, bool walking, bool crouched, float reach, bool asleep, float tickle = 0)
        {
            if (rig == null) return;
            foreach (var b in rig.rotations) if (rig.bones[b.Key]) rig.bones[b.Key].localRotation = b.Value;
            bool reference = rig.id == "golden-frog" || rig.id == "golden-bull";
            foreach (string side in new[] { "L", "R" })
            {
                float sign = side == "L" ? 1 : -1, swing = walking ? Mathf.Sin(phase + (side == "L" ? 0 : Mathf.PI)) : 0;
                Transform arm; if (rig.bones.TryGetValue("Arm" + side, out arm))
                {
                    if (!reference) arm.localRotation = Quaternion.AngleAxis(-sign * .72f * Mathf.Rad2Deg, Vector3.forward) * arm.localRotation;
                    arm.localRotation *= Quaternion.Euler((reference ? -swing * .27f - reach * .9f : swing * .32f - reach) * Mathf.Rad2Deg, 0, 0);
                }
                RotateBone(rig, "Leg" + side, swing * .4f + (asleep ? Mathf.Min(.7f, tickle) * (.6f + Mathf.Sin(time * 12) * .2f) : 0), 0, 0);
                if (reference) { float lift = Mathf.Max(0, -swing); RotateBone(rig, "Shin" + side, -lift * .42f, 0, 0); RotateBone(rig, "Foot" + side, lift * .18f, 0, 0); RotateBone(rig, "Forearm" + side, -.035f - Mathf.Max(0, swing) * .12f - reach * .35f - (crouched ? .12f : 0), 0, 0); RotateBone(rig, "Ear" + side, 0, 0, Mathf.Sin(time * 1.3f + sign) * .035f); }
            }
            RotateBone(rig, "Spine", Mathf.Sin(time * 1.7f) * .012f + (crouched && reference ? .13f : 0), 0, walking ? Mathf.Sin(phase) * .018f : 0);
            RotateBone(rig, "Tail", 0, Mathf.Sin(time * (walking ? 5 : 1.7f)) * .15f, 0);
        }
        static void RotateBone(Rig rig, string name, float x, float y, float z)
        { Transform t; if (rig.bones.TryGetValue(name, out t)) t.localRotation *= Quaternion.Euler(x * Mathf.Rad2Deg, y * Mathf.Rad2Deg, z * Mathf.Rad2Deg); }
        static Transform FindDescendant(Transform parent, string name)
        { foreach (Transform t in parent.GetComponentsInChildren<Transform>(true)) if (t.name == name) return t; return null; }

        struct VisualPose { public Vector3 position, euler; public VisualPose(Vector3 p, Vector3 r) { position = p; euler = r; } }
        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3 - 2 * t); }
        static VisualPose PosePath(VisualPose a, VisualPose b, float t, float arc)
        { return new VisualPose(Vector3.Lerp(a.position, b.position, t) + Vector3.up * Mathf.Sin(t * Mathf.PI) * arc, Vector3.Lerp(a.euler, b.euler, t)); }
        static VisualPose CatchPose(NightRescue r, float elapsed, NightFurniture f, Vector3 home)
        {
            if (r.stage == "lower") return new VisualPose(r.returnPosition, new Vector3(r.kind == "fork" ? 20 : 0, 0, r.tilt * Mathf.Rad2Deg));
            float u = r.stage == "reach" ? Smooth(elapsed / .65f) : 1;
            Vector3 p = new Vector3(Mathf.Lerp(home.x, r.objectX * .55f, u), home.y - Mathf.Max(0, 1.3f - r.height) * .5f, Mathf.Lerp(home.z, f.d / 2 + .15f, u));
            Vector3 e = new Vector3(r.kind == "fork" ? Mathf.Lerp(90, 20, u) : 0, 0, r.tilt * u * Mathf.Rad2Deg);
            if (r.stage == "reach") e.z += Mathf.Sin(elapsed * 11) * 3.7f * (1 - u);
            else if (r.contactAt >= 0) { float a = Mathf.Max(0, elapsed - r.contactAt); p.y -= Mathf.Sin(Mathf.Clamp01(a / .45f) * Mathf.PI) * .045f; e.z += Mathf.Sin(a * 15) * Mathf.Exp(-a * 7) * 2.6f; }
            return new VisualPose(p, e);
        }
        static VisualPose IncidentPose(NightRescue r, NightMode m, NightFurniture f, Vector3 home)
        {
            bool reaction = m.type == "reaction"; float clock = reaction ? r.sourceElapsed : m.elapsed;
            VisualPose target = new VisualPose(home, new Vector3(r.kind == "fork" ? 90 : 0, 0, 0));
            if (r.kind == "pencils")
            {
                float u = Smooth(clock / .6f); var tipped = new VisualPose(new Vector3(home.x + .05f * u, Mathf.Lerp(home.y, .215f, u), home.z), new Vector3(0, 0, 75 * u));
                return reaction ? PosePath(tipped, target, Smooth(m.elapsed / .32f), .035f) : tipped;
            }
            VisualPose start = CatchPose(r, clock, f, home); if (!reaction) return start;
            if (m.caught) return PosePath(start, target, Smooth(m.elapsed / .78f), r.returnProgress >= 1 ? 0 : .14f);
            float impact = .48f;
            VisualPose floor = new VisualPose(new Vector3(start.position.x + .1f, .085f - f.h + (r.kind == "fork" ? .018f : r.kind == "vase" ? .263f : .255f), Mathf.Max(start.position.z, f.d / 2 + .29f)), new Vector3(r.kind == "fork" ? 90 : 8.6f, 0, r.kind == "fork" ? 0 : Mathf.Sign(start.euler.z == 0 ? 1 : start.euler.z) * 90));
            VisualPose pose = PosePath(start, floor, Smooth(m.elapsed / impact), 0);
            if (m.elapsed > impact) { float age = m.elapsed - impact; pose.position.y += Mathf.Abs(Mathf.Sin(age * 17)) * Mathf.Exp(-age * 9) * .055f; pose.euler.z += Mathf.Sin(age * 17) * Mathf.Exp(-age * 8) * 4; }
            return pose;
        }
        static VisualPose PencilRest(int i)
        { return new VisualPose(new Vector3(-.43f + (i - 1) * .044f, .403f + i * .018f, -.17f), new Vector3(0, i * .8f * Mathf.Rad2Deg, (i - 1) * -.12f * Mathf.Rad2Deg)); }
        static VisualPose PencilPose(NightPencil q, int i, NightRescue rescue, NightMode m, float surfaceHeight)
        {
            bool reaction = m.type == "reaction"; float t = reaction ? rescue.sourceElapsed : m.elapsed;
            float approach = Mathf.Clamp01((t - (q.at - 1.2f)) / 1.2f), resolved = q.resolvedAt > 0 ? q.resolvedAt : q.at;
            var p = new VisualPose(new Vector3(Mathf.Lerp(-.43f, q.x * .55f, Smooth(approach * 2)), .112f, Mathf.Lerp(-.17f, .46f, approach)), new Vector3(90, (t * 4.4f + i) * Mathf.Rad2Deg, 0));
            if (q.state == "rolling" && t < .6f) p = PosePath(PencilRest(i), p, Smooth(t / .6f), .02f);
            if (q.state == "caught")
            {
                float a = Mathf.Max(0, t - resolved); p.position = new Vector3(rescue.handX * .55f + (i - 1) * .045f, .122f + i * .013f + Mathf.Sin(Mathf.Clamp01(a / .2f) * Mathf.PI) * .02f, .43f); p.euler.y = 90 + (i - 1) * 6.9f;
                if (reaction) { VisualPose home = PencilRest(i), above = home; above.position.y += .2f; p = PosePath(p, above, Smooth((m.elapsed - .12f) / .50f), .08f); p.position.y = Mathf.Lerp(p.position.y, home.position.y, Smooth((m.elapsed - .68f) / .35f)); }
            }
            else if (q.state == "fallen" || reaction && !m.caught)
            {
                bool interruptedRoll = reaction && !m.caught && q.state == "rolling";
                if (interruptedRoll) resolved = t;
                Vector3 from = interruptedRoll ? p.position : new Vector3(q.x * .55f, .112f, .47f);
                float fromAngle = interruptedRoll ? p.euler.x : 90;
                float age = Mathf.Max(0, t + (reaction ? m.elapsed : 0) - resolved), drop = Mathf.Clamp01(age / .34f);
                p.position = new Vector3(Mathf.Lerp(from.x, q.x * .55f + (i - 1) * .04f, drop), Mathf.Lerp(from.y, .085f - surfaceHeight + .018f, drop * drop), Mathf.Lerp(from.z, .67f, Smooth(drop * 1.6f)));
                p.euler.x = Mathf.Lerp(fromAngle, 90, Smooth(drop)) + Mathf.Sin(drop * Mathf.PI) * 120;
                if (drop >= 1) { float b = age - .34f; p.position.y += Mathf.Abs(Mathf.Sin(b * 18)) * Mathf.Exp(-b * 6) * .05f; p.position.z += Mathf.Min(.12f, b * .12f); p.euler.y = ((resolved + .34f) * 4.4f + i + .6f * (1 - Mathf.Exp(-b * 4))) * Mathf.Rad2Deg; }
            }
            return p;
        }

        void BuildCloseups()
        {
            rescueHands = Load("NightBack/rescue-hands", worldRoot, false);
            searchHands = Load("NightBack/search-hands", worldRoot, false); searchHands.gameObject.SetActive(false);
            foreach (Transform t in rescueHands.GetComponentsInChildren<Transform>(true)) if (!rescueParts.ContainsKey(t.name)) rescueParts[t.name] = t;
            Shader sleeveShader = Resources.Load<Shader>("NightBack/NativeSleeve");
            if (sleeveShader) foreach (var pair in rescueParts) if (pair.Key.EndsWith(":upper")) foreach (Renderer renderer in pair.Value.GetComponentsInChildren<Renderer>())
            { var material = new Material(sleeveShader); material.color = renderer.sharedMaterial.color; renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; }
            rescueHands.gameObject.SetActive(false);
            tickleHands = Load("NightBack/tickle-hands" + builtLevel, worldRoot, false); tickleHands.gameObject.SetActive(false);
            lockVisual = new GameObject("Lock Cutaway Stage").transform; lockVisual.SetParent(worldRoot, false); lockVisual.localPosition = new Vector3(0, -30, 0);
            lockBody = Load("NightBack/lock", lockVisual, false);
            foreach (Transform t in lockBody.GetComponentsInChildren<Transform>(true)) if (!lockParts.ContainsKey(t.name)) lockParts[t.name] = t;
            var a = new GameObject("Lock Key Light").AddComponent<Light>(); a.transform.SetParent(lockVisual, false); a.transform.localPosition = new Vector3(-2, 3, 3); a.type = LightType.Point; a.color = new Color(1, .85f, .58f); a.range = 12; a.intensity = 2.8f;
            var b = new GameObject("Lock Rim Light").AddComponent<Light>(); b.transform.SetParent(lockVisual, false); b.transform.localPosition = new Vector3(3, 1, 1); b.type = LightType.Point; b.color = new Color(.45f, .8f, 1); b.range = 10; b.intensity = 1.5f;
            lockVisual.gameObject.SetActive(false);
            closeupKey = NewLight("Detail Key Light", LightType.Point, new Color(1, .82f, .60f), 2.5f, 12); closeupKey.cullingMask = DetailMask; closeupKey.shadows = LightShadows.Soft; closeupKey.enabled = false;
            closeupRim = NewLight("Detail Rim Light", LightType.Point, new Color(.49f, .75f, .88f), 1.6f, 10); closeupRim.cullingMask = DetailMask; closeupRim.enabled = false;
            closeupFloor = DecorBox(worldRoot, "Detail Floor", new Vector3(0, -.04f, 0), new Vector3(8, .06f, 8), new Color(.15f, .22f, .29f)); closeupFloor.gameObject.layer = DetailLayer; closeupFloor.gameObject.SetActive(false);
        }
        void SyncCloseups(NightGame game)
        {
            NightMode m = game.mode;
            bool searching = m != null && m.type == "search";
            searchHands.gameObject.SetActive(searching);
            if (searching)
            {
                Transform f = FindFurniture(m.id); NightFurniture definition = null; foreach (var d in game.level.furniture) if (d.id == m.id) definition = d;
                if (f && definition != null)
                {
                    searchHands.SetParent(f, false); float height = definition.type == "shelf" ? definition.h * .52f : definition.h * .7f;
                    searchHands.localPosition = new Vector3(0, height - .035f, definition.d / 2 + .18f); searchHands.localRotation = Quaternion.identity;
                    searchHands.gameObject.SetActive(m.elapsed > .35f);
                    for (int i = 0; i < searchHands.childCount; i++) { var hand = searchHands.GetChild(i); hand.localPosition = new Vector3(i == 0 ? -.28f : .28f, Mathf.Sin(m.elapsed * 6 + i * Mathf.PI) * .05f, Mathf.Sin(m.elapsed * 5 + i * Mathf.PI) * .11f); hand.localRotation = Quaternion.Euler((-.22f + Mathf.Sin(m.elapsed * 5 + i) * .12f) * Mathf.Rad2Deg, 0, Mathf.Sin(m.elapsed * 4 + i * 2) * .13f * Mathf.Rad2Deg); }
                }
            }
            bool lockActive = m != null && m.type == "lockpick" && game.lockState != null;
            lockVisual.gameObject.SetActive(lockActive);
            if (lockActive)
            {
                var mechanism = game.lockState; float turn = Smooth(mechanism.turn), shake = mechanism.stress > 0 ? Mathf.Sin(m.elapsed * 63) * Mathf.Min(.013f, mechanism.stress * .02f) : 0;
                lockBody.localPosition = new Vector3(0, .2f, 0); lockBody.localRotation = Quaternion.Euler(.055f * Mathf.Rad2Deg, -.12f * Mathf.Rad2Deg, shake * Mathf.Rad2Deg);
                for (int i = 0; i < 4; i++)
                {
                    Transform group = lockParts["LockPin:" + i + ":group"]; bool exists = i < mechanism.pins.Length; group.gameObject.SetActive(exists); if (!exists) continue;
                    var pin = mechanism.pins[i]; string prefix = "LockPin:" + i + ":"; float seam = .48f + (pin.lift - pin.target) * .98f;
                    Transform lower = lockParts[prefix + "lower"], upper = lockParts[prefix + "upper"], spring = lockParts[prefix + "spring"];
                    lower.localPosition = new Vector3(turn * .08f, seam, 0); lower.localRotation = Quaternion.Euler(0, 0, -turn * .18f * Mathf.Rad2Deg); upper.localPosition = new Vector3(0, seam + .016f, 0);
                    lockParts[prefix + "groove"].gameObject.SetActive(pin.spool); lockParts[prefix + "driver"].gameObject.SetActive(!pin.spool);
                    spring.localPosition = new Vector3(0, seam + .36f, 0); spring.localScale = new Vector3(1, Mathf.Max(.13f, 1.51f - seam - .36f), 1);
                    Color color = pin.seated ? new Color(.64f, .87f, .78f) : mechanism.selected == i ? new Color(.95f, .78f, .5f) : new Color(.28f, .39f, .44f);
                    foreach (Renderer renderer in lockParts[prefix + "led"].GetComponentsInChildren<Renderer>()) { var block = new MaterialPropertyBlock(); block.SetColor("_Color", color); block.SetColor("_EmissionColor", color * (pin.seated ? 1 : .3f)); renderer.SetPropertyBlock(block); }
                }
                if (mechanism.pins.Length > 0)
                {
                    int selected = Mathf.Clamp(mechanism.selected, 0, mechanism.pins.Length - 1); var pin = mechanism.pins[selected]; Transform pick = lockParts["LockPick"];
                    pick.localPosition = new Vector3(-1.13f + selected * .675f, .48f + (pin.lift - pin.target) * .98f - .55f, .13f); pick.localRotation = Quaternion.Euler(0, 0, mechanism.pressure * .022f * Mathf.Rad2Deg); pick.gameObject.SetActive(mechanism.turn == 0);
                }
                lockParts["LockRotor"].localRotation = Quaternion.Euler(0, 0, -turn * 1.4f * Mathf.Rad2Deg);
                lockParts["LockTension"].localRotation = Quaternion.Euler(0, 0, (-.35f - turn * .6f) * Mathf.Rad2Deg);
            }
            bool rescuing = m != null && (m.type == "catch" || m.type == "reaction") && game.rescue != null;
            rescueHands.gameObject.SetActive(rescuing);
            if (rescuing)
            {
                FurnitureState f = null; NightFurniture def = null; foreach (var pair in furniture) if (pair.Value.kind == m.kind) { f = pair.Value; foreach (var d in game.level.furniture) if (d.id == pair.Key) def = d; break; }
                if (f != null && f.frame && def != null)
                {
                    rescueHands.SetParent(f.frame, false); rescueHands.localPosition = Vector3.zero; rescueHands.localRotation = Quaternion.identity;
                    var r = game.rescue; bool reaction = m.type == "reaction", pencils = r.kind == "pencils";
                    VisualPose pose = IncidentPose(r, m, def, rest[f.item].position); float withdraw = reaction ? Smooth((m.elapsed - (pencils ? .98f : m.caught ? .70f : .05f)) / .22f) : 0;
                    for (int side = 0; side < 2; side++)
                    {
                        int sign = side == 0 ? -1 : 1; float grip = sign < 0 ? r.left : r.right;
                        string prefix = "RescueArm:" + side + ":"; Transform hand = rescueParts[prefix + "hand"];
                        bool visible = !pencils || sign > 0 || reaction; hand.gameObject.SetActive(visible);
                        float x = pose.position.x + sign * (r.kind == "fork" ? .082f : r.kind == "tin" ? .19f : .175f), y = pose.position.y - (r.kind == "fork" ? -.03f : .14f), z = pose.position.z + .085f;
                        if (!reaction && r.stage == "reach") x = r.handX * .55f + sign * (r.kind == "fork" ? .082f : .175f);
                        if (pencils)
                        {
                            x = r.handX * .55f; y = .104f; z = .49f;
                            if (reaction && sign > 0) for (int i = 0; i < r.pencils.Length; i++) if (r.pencils[i].state == "caught") { VisualPose p = PencilPose(r.pencils[i], i, r, m, def.h); x = p.position.x + .09f; y = p.position.y - .05f; z = p.position.z + .055f; break; }
                            if (reaction && sign < 0) { x = pose.position.x - .17f; y = pose.position.y - .05f; z = pose.position.z + .055f; }
                        }
                        hand.localPosition = new Vector3(x + sign * withdraw * .12f, y + withdraw * .045f, z + withdraw * .36f);
                        hand.localRotation = Quaternion.Euler((pencils ? -.12f : r.kind == "fork" ? -.10f : -grip * .32f) * Mathf.Rad2Deg, 0, -sign * grip * .3f * Mathf.Rad2Deg);
                        Vector3 wrist = rescueHands.InverseTransformPoint(hand.TransformPoint(new Vector3(0, -.006f, .07f)));
                        float over = Smooth((def.d / 2 + .22f - wrist.z) / .22f);
                        Vector3 shoulder = wrist + new Vector3(sign * .14f, Mathf.Lerp(-.19f, .025f, over), .41f), delta = wrist - shoulder, axis = delta.normalized;
                        float distance = delta.magnitude, along = (.30f * .30f - .27f * .27f + distance * distance) / (2 * distance), bend = Mathf.Sqrt(Mathf.Max(0, .30f * .30f - along * along));
                        Vector3 wanted = new Vector3(sign * .65f, Mathf.Lerp(-1, .15f, over), 0), perpendicular = (wanted - axis * Vector3.Dot(wanted, axis)).normalized;
                        Vector3 elbow = shoulder + axis * along + perpendicular * bend;
                        Transform sleeve = rescueParts[prefix + "sleeve"], upper = rescueParts[prefix + "upper"], cuff = rescueParts[prefix + "cuff"], cap = rescueParts[prefix + "elbowCap"];
                        sleeve.gameObject.SetActive(visible); upper.gameObject.SetActive(visible); cuff.gameObject.SetActive(visible); cap.gameObject.SetActive(visible);
                        Segment(sleeve, wrist, elbow); Segment(upper, elbow, shoulder); cuff.localPosition = Vector3.Lerp(wrist, elbow, .035f); cuff.localRotation = sleeve.localRotation; cap.localPosition = elbow;
                    }
                    Transform wrap = rescueParts["RescueWrap"]; wrap.gameObject.SetActive(r.kind == "fork" && r.left > .12f && (r.stage == "damp" || reaction && m.elapsed < .62f)); wrap.localPosition = pose.position + new Vector3(0, -.035f, .035f);
                }
            }
            bool tickling = m != null && m.type == "tickle"; tickleHands.gameObject.SetActive(tickling);
            if (tickling) foreach (Transform hand in tickleHands)
            {
                int side = hand.name.EndsWith("0") ? 0 : 1; hand.localPosition = new Vector3(side == 0 ? -.19f : .19f, m.force > .01f ? m.force * .025f : -.035f, m.force > .01f ? Mathf.Sin(game.time * 9 + side) * .03f : 0);
                foreach (Transform finger in hand) if (finger.name.StartsWith("TickleFinger:")) finger.localRotation = Quaternion.Euler(90 + (m.force > .01f ? Mathf.Sin(game.time * 24 + finger.GetSiblingIndex() + side) * 27 : 0), 0, 0);
            }
        }
        static void Segment(Transform segment, Vector3 a, Vector3 b)
        { Vector3 direction = a - b; segment.localPosition = (a + b) * .5f; segment.localScale = new Vector3(1, direction.magnitude, 1); segment.localRotation = Quaternion.FromToRotation(Vector3.up, direction.normalized); }

        // Native equivalent of the original isolated SearchCamera / IncidentCamera render scene.
        // Pass this return value to the camera's cullingMask; no project layer configuration is required.
        public int SetCloseup(string furnitureIdOrKind)
        {
            if (string.IsNullOrEmpty(furnitureIdOrKind)) { ClearCloseup(); return -1; }
            Transform selected = FindFurniture(furnitureIdOrKind);
            if (!selected) foreach (FurnitureState f in furniture.Values) if (f.kind == furnitureIdOrKind) { selected = f.root; break; }
            if (!selected) { ClearCloseup(); return -1; }
            if (showcaseEnabled) SetShowcase(false);
            if (closeupFurniture != selected)
            {
                ClearCloseup(); closeupFurniture = selected;
                foreach (Light light in worldRoot.GetComponentsInChildren<Light>(true)) if (light != closeupKey && light != closeupRim) light.cullingMask = ~DetailMask;
            }
            SetLayer(selected, DetailLayer);
            closeupFloor.gameObject.SetActive(true); closeupFloor.position = new Vector3(selected.position.x, -.04f, selected.position.z);
            closeupKey.enabled = closeupRim.enabled = true;
            closeupKey.transform.position = selected.TransformPoint(new Vector3(-2, 4, 3)); closeupRim.transform.position = selected.TransformPoint(new Vector3(2, 2, -2));
            return DetailMask;
        }
        public void ClearCloseup()
        {
            if (closeupFurniture) SetLayer(closeupFurniture, 0); closeupFurniture = null;
            if (closeupKey) closeupKey.enabled = false; if (closeupRim) closeupRim.enabled = false; if (closeupFloor) closeupFloor.gameObject.SetActive(false);
            if (worldRoot && !showcaseEnabled) foreach (Light light in worldRoot.GetComponentsInChildren<Light>(true)) if (light != closeupKey && light != closeupRim) light.cullingMask = -1;
        }
        static void SetLayer(Transform root, int layer) { if (root) foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer; }

        // UI passes pointer movement in pixels. Repeated SetShowcase and changing skins retain this orbit.
        public void ShowcaseOrbit(Vector2 delta)
        {
            if (float.IsNaN(delta.x) || float.IsNaN(delta.y) || float.IsInfinity(delta.x) || float.IsInfinity(delta.y)) return;
            showcaseOrbitYaw = Mathf.Repeat(showcaseOrbitYaw + delta.x * .42f + 180, 360) - 180;
            showcaseOrbitPitch = Mathf.Clamp(showcaseOrbitPitch - delta.y * .28f, -6, 28);
            if (showcaseOrbitGroup) showcaseOrbitGroup.localRotation = Quaternion.Euler(0, showcaseOrbitYaw, 0);
        }
        public void ResetShowcaseOrbit()
        {
            showcaseOrbitYaw = showcaseOrbitPitch = 0;
            if (showcaseOrbitGroup) showcaseOrbitGroup.localRotation = Quaternion.identity;
        }

        // Menu uses the two authored golden mascots; wardrobe uses the equipped player/parent pair.
        // These are fresh instances and never move, pose or hide a simulated actor through its Transform.
        public void SetShowcase(bool enabled, bool useSelectedSkins = false)
        {
            if (!worldRoot) return;
            bool changedPair = showcaseSelected != useSelectedSkins; showcaseSelected = useSelectedSkins;
            if (enabled)
            {
                ClearCloseup();
                if (!showcaseRoot) BuildShowcase();
                if (changedPair || showcasePlayerId != (showcaseSelected ? playerSkin : "golden-frog") || showcaseParentId != (showcaseSelected ? parentSkin : "golden-bull")) RebuildShowcaseActors();
                if (!showcaseEnabled)
                {
                    showcaseHidden.Clear(); foreach (Transform child in worldRoot) if (child != showcaseRoot) { showcaseHidden[child.gameObject] = child.gameObject.activeSelf; child.gameObject.SetActive(false); }
                    savedFog = RenderSettings.fog;
                }
                showcaseEnabled = true; showcaseRoot.gameObject.SetActive(true); RenderSettings.fog = false;
                RenderSettings.ambientLight = new Color(.47f, .53f, .66f);
            }
            else if (showcaseEnabled)
            {
                showcaseEnabled = false; if (showcaseRoot) showcaseRoot.gameObject.SetActive(false);
                foreach (var pair in showcaseHidden) if (pair.Key) pair.Key.SetActive(pair.Value); showcaseHidden.Clear(); RenderSettings.fog = savedFog;
            }
        }
        void BuildShowcase()
        {
            showcaseRoot = new GameObject("Character Portrait Stage").transform; showcaseRoot.SetParent(worldRoot, false); showcaseRoot.localPosition = new Vector3(60, 0, 0);
            showcaseOrbitGroup = new GameObject("Independent Portrait Orbit").transform; showcaseOrbitGroup.SetParent(showcaseRoot, false);
            showcaseOrbitGroup.localRotation = Quaternion.Euler(0, showcaseOrbitYaw, 0);
            DecorPrimitive(showcaseRoot, PrimitiveType.Cylinder, "Midnight Stage", new Vector3(0, -.08f, 0), new Vector3(6.6f, .075f, 4.8f), new Color(.13f, .19f, .27f));
            DecorPrimitive(showcaseRoot, PrimitiveType.Cylinder, "Soft Carpet", new Vector3(0, .005f, .1f), new Vector3(4.5f, .015f, 3.8f), new Color(.31f, .43f, .44f));
            if (doorVisuals.Count > 0 && doorVisuals[0])
            {
                var door = Instantiate(doorVisuals[0].gameObject, showcaseRoot).transform; door.name = "A Suspiciously Open Door"; door.localPosition = new Vector3(-2.2f, 0, -.8f); door.localRotation = Quaternion.Euler(0, 23, 0);
                foreach (Collider collider in door.GetComponentsInChildren<Collider>()) collider.enabled = false;
                DecorBox(showcaseRoot, "Warm Door Gap", new Vector3(-1.6f, .025f, -.82f), new Vector3(1.5f, .025f, .16f), new Color(1, .72f, .31f));
            }
            StudioLight("Portrait Key", new Vector3(-2.6f, 3.8f, 3.6f), new Color(1, .87f, .64f), 3.7f);
            StudioLight("Portrait Fill", new Vector3(3.5f, 2.4f, 2.3f), new Color(.50f, .75f, 1), 2.8f);
            StudioLight("Portrait Rim", new Vector3(1.3f, 3.1f, -2.2f), new Color(1, .64f, .31f), 4.2f);
            RebuildShowcaseActors(); SetLayer(showcaseRoot, DetailLayer);
        }
        void StudioLight(string name, Vector3 p, Color color, float power)
        { var o = new GameObject(name); o.transform.SetParent(showcaseRoot, false); o.transform.localPosition = p; var light = o.AddComponent<Light>(); light.type = LightType.Point; light.color = color; light.intensity = power; light.range = 12; light.cullingMask = DetailMask; light.shadows = LightShadows.Soft; }
        void RebuildShowcaseActors()
        {
            if (!showcaseRoot) return;
            if (showcasePlayer) { showcasePlayer.gameObject.SetActive(false); DestroySafe(showcasePlayer.gameObject); }
            if (showcaseParent) { showcaseParent.gameObject.SetActive(false); DestroySafe(showcaseParent.gameObject); }
            if (showcaseCat) { showcaseCat.gameObject.SetActive(false); DestroySafe(showcaseCat.gameObject); }
            showcasePlayerId = showcaseSelected ? playerSkin : "golden-frog"; showcaseParentId = showcaseSelected ? parentSkin : "golden-bull";
            showcasePlayer = new GameObject("Portrait Player").transform; showcasePlayer.SetParent(showcaseOrbitGroup, false); showcasePlayer.localPosition = new Vector3(-.62f, .04f, .12f); showcasePlayer.localScale = Vector3.one * 1.08f;
            showcaseParent = new GameObject("Portrait Sleepy Parent").transform; showcaseParent.SetParent(showcaseOrbitGroup, false); showcaseParent.localPosition = new Vector3(.90f, .045f, -.04f); showcaseParent.localScale = Vector3.one * 1.16f;
            showcasePlayerRig = BuildRig(showcasePlayerId, showcasePlayer); showcaseParentRig = BuildRig(showcaseParentId, showcaseParent, true);
            showcaseCat = Load("NightBack/cat", showcaseOrbitGroup, false); showcaseCat.localPosition = new Vector3(.09f, 0, .88f); showcaseCat.localScale = Vector3.one * 1.15f; showcaseCat.localRotation = Quaternion.Euler(0, -18, 0);
            Transform phone = DecorBox(showcasePlayer, "Definitely Not A Phone", new Vector3(.40f, .83f, .34f), new Vector3(.18f, .29f, .055f), new Color(.15f, .2f, .29f)); phone.localRotation = Quaternion.Euler(-20, 0, 12);
            DecorBox(showcasePlayer, "Phone Glow", new Vector3(.40f, .83f, .37f), new Vector3(.135f, .235f, .015f), new Color(.47f, .85f, .83f));
            Transform flashlight = DecorPrimitive(showcaseParent, PrimitiveType.Cylinder, "Sleepy Flashlight", new Vector3(-.42f, .76f, .24f), new Vector3(.13f, .17f, .13f), new Color(.27f, .33f, .37f)); flashlight.localRotation = Quaternion.Euler(100, 0, -15);
            Transform lens = DecorPrimitive(showcaseParent, PrimitiveType.Cylinder, "Flashlight Lens", new Vector3(-.42f, .74f, .42f), new Vector3(.15f, .025f, .15f), new Color(1, .86f, .54f)); lens.localRotation = Quaternion.Euler(90, 0, 0);
            SetLayer(showcaseRoot, DetailLayer);
        }
        void LateUpdate()
        {
            if (!showcaseEnabled || !showcaseRoot) return;
            float t = Time.unscaledTime;
            showcasePlayer.localRotation = Quaternion.Euler(0, -9 + Mathf.Sin(t * .5f) * 4, Mathf.Sin(t * 1.2f) * 2);
            showcaseParent.localRotation = Quaternion.Euler(0, -17, -5 + Mathf.Sin(t * .7f) * 2);
            showcasePlayer.localPosition = new Vector3(-.62f, .04f + Mathf.Abs(Mathf.Sin(t * 1.1f)) * .018f, .12f);
            AnimateRig(showcasePlayerRig, t, t * .8f, false, false, .36f, false);
            AnimateRig(showcaseParentRig, t, 0, false, false, .18f, false);
            RotateBone(showcaseParentRig, "Head", .10f + Mathf.Sin(t * .7f) * .04f, -.13f, -.08f);
            Transform tail = FindDescendant(showcaseCat, "Cat:tail"); if (tail) tail.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 2) * 18);
            Transform body = FindDescendant(showcaseCat, "Cat:body"); if (body) body.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(t * 1.5f) * 4);
        }

        void BuildComicDetails(NightLevel level)
        {
            sleepSymbols = new GameObject("Floating Sleep Letters").transform; sleepSymbols.SetParent(worldRoot, false);
            for (int i = 0; i < 3; i++)
            {
                Transform z = new GameObject("Sleep Z " + i).transform; z.SetParent(sleepSymbols, false);
                DecorBox(z, "Top", new Vector3(0, .10f, 0), new Vector3(.19f, .035f, .035f), new Color(.55f, .75f, .93f));
                Transform diagonal = DecorBox(z, "Diagonal", Vector3.zero, new Vector3(.23f, .035f, .035f), new Color(.55f, .75f, .93f)); diagonal.localRotation = Quaternion.Euler(0, 0, 45);
                DecorBox(z, "Bottom", new Vector3(0, -.10f, 0), new Vector3(.19f, .035f, .035f), new Color(.55f, .75f, .93f));
            }
            celebration = new GameObject("Quiet Victory Stars").transform; celebration.SetParent(worldRoot, false);
            for (int i = 0; i < 10; i++)
            {
                Transform star = new GameObject("Star " + i).transform; star.SetParent(celebration, false);
                Color c = i % 2 == 0 ? new Color(1, .78f, .25f) : new Color(.44f, .84f, .78f);
                for (int j = 0; j < 3; j++) { Transform ray = DecorBox(star, "Spark", Vector3.zero, new Vector3(.19f, .038f, .035f), c); ray.localRotation = Quaternion.Euler(0, 0, j * 60); }
            }
            celebration.gameObject.SetActive(false);
            stepRing = CreateRing(worldRoot, "Deliberate Foot Placement", .30f, .37f, new Color(.96f, .8f, .54f)); stepRing.gameObject.SetActive(false);
            Transform clock = new GameObject("House Routine Wall Clock").transform; clock.SetParent(worldRoot, false);
            clock.localPosition = builtLevel == 3 ? new Vector3(4.6f, 1.91f, 12.51f) : new Vector3(2.05f, 1.91f, 10.51f);
            Transform rim = DecorPrimitive(clock, PrimitiveType.Cylinder, "Clock Brass Rim", Vector3.zero, new Vector3(.45f, .018f, .45f), new Color(.7f, .52f, .28f)); rim.localRotation = Quaternion.Euler(90, 0, 0);
            Transform face = DecorPrimitive(clock, PrimitiveType.Cylinder, "Clock Face", new Vector3(0, 0, .022f), new Vector3(.405f, .012f, .405f), new Color(.93f, .87f, .7f)); face.localRotation = Quaternion.Euler(90, 0, 0);
            for (int i = 0; i < 12; i++) { float a = i * Mathf.PI / 6; Transform tick = DecorBox(clock, "Clock Tick", new Vector3(Mathf.Sin(a) * .165f, Mathf.Cos(a) * .165f, .038f), new Vector3(.014f, .032f, .012f), new Color(.28f, .29f, .32f)); tick.localRotation = Quaternion.Euler(0, 0, -i * 30); }
            hourHand = new GameObject("Hour Hand").transform; hourHand.SetParent(clock, false); hourHand.localPosition = Vector3.forward * .05f;
            minuteHand = new GameObject("Minute Hand").transform; minuteHand.SetParent(clock, false); minuteHand.localPosition = Vector3.forward * .06f;
            DecorBox(hourHand, "Hand", Vector3.up * .04f, new Vector3(.021f, .11f, .012f), new Color(.20f, .27f, .34f));
            DecorBox(minuteHand, "Hand", Vector3.up * .06f, new Vector3(.012f, .16f, .012f), new Color(.20f, .27f, .34f));
        }
        static Transform DecorBox(Transform parent, string name, Vector3 p, Vector3 size, Color color) { return DecorPrimitive(parent, PrimitiveType.Cube, name, p, size, color); }
        static Transform CreateRing(Transform parent, string name, float inner, float outer, Color color)
        {
            const int segments = 40; var vertices = new Vector3[(segments + 1) * 2]; var normals = new Vector3[vertices.Length]; var triangles = new int[segments * 6];
            for (int i = 0; i <= segments; i++) { float a = i * Mathf.PI * 2 / segments; Vector3 direction = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)); vertices[i * 2] = direction * inner; vertices[i * 2 + 1] = direction * outer; normals[i * 2] = normals[i * 2 + 1] = Vector3.up; if (i < segments) { int k = i * 6, v = i * 2; triangles[k] = v; triangles[k + 1] = v + 2; triangles[k + 2] = v + 1; triangles[k + 3] = v + 1; triangles[k + 4] = v + 2; triangles[k + 5] = v + 3; } }
            var o = new GameObject(name); o.transform.SetParent(parent, false); var mesh = new Mesh(); mesh.vertices = vertices; mesh.normals = normals; mesh.triangles = triangles; mesh.RecalculateBounds(); o.AddComponent<MeshFilter>().sharedMesh = mesh;
            var material = new Material(Shader.Find("Standard")); material.color = color; material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .25f); var renderer = o.AddComponent<MeshRenderer>(); renderer.sharedMaterial = material; renderer.shadowCastingMode = ShadowCastingMode.Off; return o.transform;
        }
        static Transform DecorPrimitive(Transform parent, PrimitiveType type, string name, Vector3 p, Vector3 scale, Color color)
        {
            var o = GameObject.CreatePrimitive(type); o.name = name; o.transform.SetParent(parent, false); o.transform.localPosition = p; o.transform.localScale = scale;
            var collider = o.GetComponent<Collider>(); if (collider) { collider.enabled = false; DestroySafe(collider); }
            var material = new Material(Shader.Find("Standard")); material.color = color; material.SetFloat("_Glossiness", .18f); o.GetComponent<Renderer>().sharedMaterial = material; return o.transform;
        }
        void SyncComicDetails(NightGame game)
        {
            bool asleep = game.parent.state == "sleep" && game.tickleHeat < .18f;
            sleepSymbols.gameObject.SetActive(asleep && game.active); sleepSymbols.position = game.level.parentBed + new Vector3(-.42f, 1.12f, -.28f);
            for (int i = 0; i < sleepSymbols.childCount; i++)
            {
                Transform z = sleepSymbols.GetChild(i); float age = (game.time * .35f + i / 3f) % 1;
                z.localPosition = new Vector3(age * .36f, age * .52f, 0); z.localScale = Vector3.one * (.35f + Mathf.Sin(age * Mathf.PI) * .35f); z.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(game.time + i) * 12);
            }
            bool won = game.status == "won"; celebration.gameObject.SetActive(won); celebration.position = game.player + Vector3.up * 1.1f;
            if (won) for (int i = 0; i < celebration.childCount; i++) { float time = Time.unscaledTime; float angle = i * Mathf.PI * .2f + time * .3f; Transform star = celebration.GetChild(i); star.localPosition = new Vector3(Mathf.Cos(angle) * .72f, .1f + Mathf.Sin(time * 3 + i) * .18f, Mathf.Sin(angle) * .72f); star.localRotation = Quaternion.Euler(0, 0, time * 35 + i * 17); star.localScale = Vector3.one * (.5f + Mathf.Sin(time * 4 + i) * .15f); }
            if (game.clock != null) { float minutes = 22 * 60 + 30 + game.clock.totalSeconds / 6f; minuteHand.localRotation = Quaternion.Euler(0, 0, -minutes * 6); hourHand.localRotation = Quaternion.Euler(0, 0, -minutes * .5f); }
        }

        Transform Load(string resource, Transform parent, bool collectLights)
        {
            EnsureMaterials(); TextAsset json = Resources.Load<TextAsset>(resource);
            if (!json) throw new InvalidOperationException("Missing native world resource: " + resource);
            FileData data = JsonUtility.FromJson<FileData>(json.text); var nodes = new Dictionary<int, Transform>(); var deferred = new List<KeyValuePair<Node, Transform>>();
            foreach (Node d in data.nodes)
            {
                Transform parentNode = parent;
                if (d.parent >= 0 && !nodes.TryGetValue(d.parent, out parentNode)) throw new InvalidDataException("Native hierarchy parent missing in " + resource + ": " + d.parent);
                LoadNode(d, parentNode, nodes, deferred, collectLights);
            }
            Transform root = nodes[data.rootId];
            foreach (var pair in deferred)
            {
                Node d = pair.Key; Transform t = pair.Value; var renderer = t.gameObject.AddComponent<SkinnedMeshRenderer>();
                Mesh mesh = Instantiate(LoadMesh(d.geometry)); ownedSkinMeshes.Add(mesh);
                var bind = new Matrix4x4[d.bindposes.Length]; for (int i = 0; i < bind.Length; i++) { var m = new Matrix4x4(); for (int k = 0; k < 16; k++) m[k] = d.bindposes[i].values[k]; bind[i] = m; } mesh.bindposes = bind;
                var bones = new Transform[d.bones.Length]; for (int i = 0; i < bones.Length; i++) bones[i] = nodes[d.bones[i]];
                renderer.sharedMesh = mesh; renderer.bones = bones; renderer.rootBone = bones.Length > 0 ? bones[0] : root; renderer.sharedMaterials = MaterialsFor(d.materials);
                renderer.updateWhenOffscreen = true; renderer.localBounds = new Bounds(Vector3.up * .9f, Vector3.one * 4); renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
            }
            return root;
        }
        Transform LoadNode(Node d, Transform parent, Dictionary<int, Transform> nodes, List<KeyValuePair<Node, Transform>> deferred, bool collectLights)
        {
            var obj = new GameObject(d.name); Transform t = obj.transform; t.SetParent(parent, false); t.localPosition = d.position; t.localRotation = d.rotation; t.localScale = d.scale; nodes[d.id] = t;
            if (!string.IsNullOrEmpty(d.geometry))
            {
                if (d.bones != null && d.bones.Length > 0) deferred.Add(new KeyValuePair<Node, Transform>(d, t));
                else { obj.AddComponent<MeshFilter>().sharedMesh = LoadMesh(d.geometry); var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterials = MaterialsFor(d.materials); renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true; }
            }
            if (d.light != null && d.light.range > 0)
            {
                Light l = obj.AddComponent<Light>(); l.type = d.light.type == "spot" ? LightType.Spot : LightType.Point; l.color = d.light.color; l.intensity = d.light.power * .16f; l.range = d.light.range; l.spotAngle = d.light.angle; l.shadows = LightShadows.Soft; l.shadowBias = .04f;
                if (collectLights) localLights.Add(new LightState { light = l, intensity = l.intensity });
            }
            obj.SetActive(d.active); return t;
        }
        static Material[] MaterialsFor(int[] ids)
        { if (ids == null) return new Material[0]; var result = new Material[ids.Length]; for (int i = 0; i < ids.Length; i++) result[i] = materials[ids[i]]; return result; }
        static void EnsureMaterials()
        {
            if (materials != null && materials.Length > 0 && materials[0]) return;
            var data = JsonUtility.FromJson<MaterialFile>(Resources.Load<TextAsset>("NightBack/materials").text); materials = new Material[data.materials.Length];
            Shader shader = Resources.Load<Shader>("NightBack/NativeSurface"); if (!shader) shader = Shader.Find("Standard");
            for (int i = 0; i < materials.Length; i++)
            {
                var d = data.materials[i]; var m = new Material(shader); m.name = string.IsNullOrEmpty(d.name) ? "Native " + i : d.name; m.color = d.color;
                m.SetFloat("_Metallic", d.metallic); m.SetFloat("_Glossiness", 1 - d.roughness);
                m.SetFloat("_UseVertexColor", d.vertexColors ? 1 : 0); m.SetFloat("_Cull", d.doubleSided ? 0 : 2);
                if (!string.IsNullOrEmpty(d.texture)) m.mainTexture = Resources.Load<Texture2D>(d.texture);
                if (!string.IsNullOrEmpty(d.normal))
                {
                    TextAsset bytes = Resources.Load<TextAsset>(d.normal);
                    if (bytes) { var texture = new Texture2D(2, 2, TextureFormat.RGBA32, true, true); texture.name = "Native linear normal"; texture.LoadImage(bytes.bytes); m.SetTexture("_BumpMap", texture); m.SetFloat("_UseNormal", 1); m.SetFloat("_BumpScale", d.normalScale); }
                }
                if (d.emissionPower > 0) { m.EnableKeyword("_EMISSION"); m.SetColor("_EmissionColor", d.emission * d.emissionPower); }
                if (d.alphaCutoff > 0) { m.SetFloat("_Mode", 1); m.SetFloat("_Cutoff", d.alphaCutoff); m.EnableKeyword("_ALPHATEST_ON"); m.renderQueue = 2450; }
                materials[i] = m;
            }
        }
        static Mesh LoadMesh(string resource)
        {
            Mesh found; if (meshes.TryGetValue(resource, out found) && found) return found;
            TextAsset data = Resources.Load<TextAsset>(resource); if (!data) throw new InvalidOperationException("Missing native geometry: " + resource);
            using (var reader = new BinaryReader(new MemoryStream(data.bytes)))
            {
                if (reader.ReadInt32() != 0x4e424d32) throw new InvalidDataException("Invalid native mesh header");
                int count = reader.ReadInt32(), indexCount = reader.ReadInt32(); bool hasUV = reader.ReadInt32() != 0, hasBones = reader.ReadInt32() != 0, hasColors = reader.ReadInt32() != 0;
                var vertices = new Vector3[count]; var normals = new Vector3[count]; var uv = hasUV ? new Vector2[count] : null; var weights = hasBones ? new BoneWeight[count] : null; var colors = hasColors ? new Color[count] : null;
                for (int i = 0; i < count; i++)
                {
                    vertices[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle()); normals[i] = new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                    if (hasUV) uv[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());
                    if (hasBones) weights[i] = new BoneWeight { weight0 = reader.ReadSingle(), weight1 = reader.ReadSingle(), weight2 = reader.ReadSingle(), weight3 = reader.ReadSingle(), boneIndex0 = reader.ReadInt32(), boneIndex1 = reader.ReadInt32(), boneIndex2 = reader.ReadInt32(), boneIndex3 = reader.ReadInt32() };
                    if (hasColors) colors[i] = new Color(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
                }
                var indices = new int[indexCount]; for (int i = 0; i < indexCount; i++) indices[i] = reader.ReadInt32();
                var mesh = new Mesh(); mesh.name = resource; if (count > 65535) mesh.indexFormat = IndexFormat.UInt32;
                mesh.vertices = vertices; mesh.normals = normals; if (hasUV) mesh.uv = uv; if (hasBones) mesh.boneWeights = weights; if (hasColors) mesh.colors = colors; mesh.triangles = indices; mesh.RecalculateBounds(); if (hasUV) mesh.RecalculateTangents();
                meshes[resource] = mesh; return mesh;
            }
        }
    }
}
