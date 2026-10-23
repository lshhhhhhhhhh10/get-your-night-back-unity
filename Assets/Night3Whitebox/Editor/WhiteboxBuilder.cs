using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;

[Serializable] public class WBPoint { public float x,z; }
[Serializable] public class WBOpening { public float x,z; public string axis,name; }
[Serializable] public class WBProp { public string id,type,name; public float x,z,w,d,h,yaw; public bool cover,search,vase; }
[Serializable] public class WBRug { public float x,z,w,d; }
[Serializable] public class WBLayout { public int width,depth; public float wallHeight,doorClearance; public WBPoint home,target; public WBPoint[] walls,floors; public WBOpening[] openings; public WBProp[] furniture; public WBRug[] rugs; }

public static class WhiteboxBuilder
{
    const string Root="Assets/Night3Whitebox";
    public const string ScenePath=Root+"/Scenes/Night03_FootstepsCloser.scene";
    static Dictionary<string,Material> mats;
    static Dictionary<string,GameObject> kit;
    static List<Renderer> uppers;
    static Transform architecture, furniture, details, annotation;
    static WBLayout data;
    static Material M(string id, Color color)
    {
        string path=Root+"/Materials/"+id+".mat";
        Material m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Standard"));AssetDatabase.CreateAsset(m,path);}
        m.color=color;m.SetFloat("_Glossiness",.16f);m.SetFloat("_Metallic",0);return m;
    }
    static Color Hex(string s){ColorUtility.TryParseHtmlString(s,out var c);return c;}
    static GameObject Group(string name,Transform parent=null)
    {var g=new GameObject(name);if(parent)g.transform.SetParent(parent,false);return g;}
    static GameObject Shape(string name,Transform p,Vector3 pos,Vector3 size,string mat,PrimitiveType type=PrimitiveType.Cube)
    {
        var g=GameObject.CreatePrimitive(type);g.name=name;g.transform.SetParent(p,false);g.transform.localPosition=pos;g.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(g.GetComponent<Collider>());g.GetComponent<Renderer>().sharedMaterial=mats[mat];return g;
    }
    static GameObject Box(string name,Transform p,float x,float y,float z,float w,float h,float d,string mat)
    {return Shape(name,p,new Vector3(x,y,z),new Vector3(w,h,d),mat);}
    static void Solid(GameObject g,Vector3 center,Vector3 size)
    {var c=g.AddComponent<BoxCollider>();c.center=center;c.size=size;}
    static GameObject SaveModule(GameObject g,string name)
    {g.name=name;var p=PrefabUtility.SaveAsPrefabAsset(g,Root+"/Prefabs/"+name+".prefab");UnityEngine.Object.DestroyImmediate(g);kit[name]=p;return p;}
    static GameObject Place(string name,Transform parent,Vector3 pos,Vector3 scale,float yaw=0)
    {var g=(GameObject)PrefabUtility.InstantiatePrefab(kit[name]);g.transform.SetParent(parent,false);g.transform.localPosition=pos;g.transform.localScale=scale;g.transform.localRotation=Quaternion.Euler(0,yaw,0);return g;}
    static void MakeArchitecture()
    {
        var g=Group("WB_Wall_1m");
        Box("Lower wall",g.transform,0,.39f,0,1,.78f,1,"chalk");
        Box("Upper wall",g.transform,0,1.69f,0,1,1.82f,1,"chalk");
        Box("Upper cap",g.transform,0,2.61f,0,1.015f,.025f,1.015f,"paper");
        Box("Base shadow line",g.transform,0,.045f,0,1.008f,.09f,1.008f,"trim");
        Solid(g,new Vector3(0,1.3f,0),new Vector3(1,2.6f,1));SaveModule(g,"WB_Wall_1m");
        g=Group("WB_Floor_1m");Box("Floor",g.transform,0,-.045f,0,.996f,.09f,.996f,"floor");Solid(g,new Vector3(0,-.045f,0),new Vector3(1,.09f,1));SaveModule(g,"WB_Floor_1m");
        g=Group("WB_Doorway_1m");
        var top=Box("Upper lintel",g.transform,0,2.4f,0,1,.4f,1,"chalk");Solid(top,Vector3.zero,Vector3.one);
        for(int side=-1;side<=1;side+=2){var jamb=Group("Jamb collider",g.transform);Solid(jamb,new Vector3(side*.48f,1.1f,0),new Vector3(.04f,2.2f,1.02f));Box("Lower jamb",g.transform,side*.48f,.39f,0,.04f,.78f,1.02f,"trim");Box("Upper jamb",g.transform,side*.48f,1.49f,0,.04f,1.42f,1.02f,"trim");}
        Box("Upper head trim",g.transform,0,2.18f,0,1,.05f,1.03f,"trim");SaveModule(g,"WB_Doorway_1m");
        g=Group("WB_HingedDoor");
        Box("Door leaf",g.transform,.47f,1.08f,0,.94f,2.16f,.12f,"oak");
        Box("Recess",g.transform,.47f,1.24f,-.064f,.73f,1.45f,.014f,"paper");
        Box("Lower panel",g.transform,.47f,.30f,-.067f,.73f,.20f,.02f,"trim");
        for(int side=-1;side<=1;side+=2)Box("Lever",g.transform,.79f,1.03f,side*.12f,.16f,.035f,.05f,"graphite");
        Solid(g,new Vector3(.47f,1.08f,0),new Vector3(.94f,2.16f,.12f));g.AddComponent<WhiteboxDoor>();SaveModule(g,"WB_HingedDoor");
    }
    static void Legs(Transform p,float y,float h,float spread=.38f)
    {foreach(float x in new[]{-spread,spread})foreach(float z in new[]{-spread,spread})Box("Leg",p,x,y,z,.065f,h,.065f,"trim");}
    static void MakeFurniture(string type)
    {
        var g=Group("WB_"+type);var t=g.transform;
        switch(type)
        {
            case "bed":
                Box("Frame",t,0,.23f,0,1,.24f,1,"trim");Box("Mattress",t,0,.48f,.02f,.94f,.27f,.92f,"paper");
                Box("Blanket",t,0,.625f,.19f,.92f,.035f,.54f,"fabric");Box("Headboard",t,0,.68f,-.46f,1,.64f,.08f,"chalk");
                foreach(float x in new[]{-.24f,.24f})Box("Pillow",t,x,.68f,-.26f,.39f,.12f,.21f,"paper");Legs(t,.075f,.15f);break;
            case "sofa":
                Box("Base",t,0,.32f,0,1,.34f,1,"fabric");Box("Back",t,0,.69f,-.38f,1,.62f,.24f,"chalk");
                foreach(float x in new[]{-.44f,.44f})Box("Arm",t,x,.59f,.07f,.12f,.43f,.86f,"chalk");
                foreach(float x in new[]{-.23f,.23f})Box("Cushion",t,x,.54f,.09f,.435f,.14f,.65f,"paper");Legs(t,.09f,.18f);break;
            case "table":case "desk":
                Box("Top",t,0,.945f,0,1,.11f,1,"paper");Legs(t,.45f,.9f);
                if(type=="desk"){Box("Drawer",t,.27f,.76f,0,.4f,.29f,.85f,"chalk");Box("Drawer pull",t,.27f,.75f,.44f,.18f,.035f,.025f,"graphite");}
                break;
            case "chair":
                Box("Seat",t,0,.49f,0,1,.12f,.9f,"paper");Box("Back",t,0,.78f,-.4f,1,.44f,.11f,"chalk");Legs(t,.24f,.48f);break;
            case "cabinet":case "kitchen":
                Box("Carcass",t,0,.50f,0,1,.88f,.96f,"chalk");Box("Top",t,0,.96f,0,1.025f,.08f,1.025f,"paper");
                for(int i=0;i<3;i++){float y=.22f+i*.25f;Box("Drawer front "+i,t,0,y,.49f,.94f,.23f,.025f,"paper");Box("Pull "+i,t,0,y+.045f,.516f,.26f,.018f,.026f,"graphite");}
                Box("Plinth",t,0,.04f,0,.88f,.08f,.86f,"trim");break;
            case "shelf":
                foreach(float x in new[]{-.46f,.46f})Box("Side",t,x,.5f,0,.08f,1,1,"chalk");
                Box("Back",t,0,.5f,-.46f,.85f,1,.08f,"trim");
                for(int i=0;i<5;i++)Box("Shelf "+i,t,0,.03f+i*.24f,0,.92f,.045f,1,"paper");
                for(int row=0;row<3;row++)for(int col=0;col<4;col++)Box("Book block",t,-.32f+col*.18f,.15f+row*.24f,-.08f,.12f,.18f,.68f,(row+col)%2==0?"fabric":"oak");break;
            case "tv":
                Box("Low console",t,0,.20f,0,1,.36f,1,"chalk");Box("Screen",t,0,.72f,0,.93f,.53f,.13f,"graphite");
                Box("Stand",t,0,.44f,0,.09f,.18f,.09f,"trim");Box("Screen inset",t,0,.72f,.071f,.86f,.45f,.015f,"slate");break;
            case "washer":
                Box("Body",t,0,.5f,0,1,1,1,"paper");Box("Control panel",t,0,.85f,.506f,.89f,.18f,.02f,"trim");
                var drum=Shape("Drum rim",t,new Vector3(0,.43f,.52f),new Vector3(.70f,.05f,.70f),"trim",PrimitiveType.Cylinder);drum.transform.localRotation=Quaternion.Euler(90,0,0);
                drum=Shape("Drum glass",t,new Vector3(0,.43f,.57f),new Vector3(.55f,.025f,.55f),"slate",PrimitiveType.Cylinder);drum.transform.localRotation=Quaternion.Euler(90,0,0);
                Shape("Dial",t,new Vector3(.25f,.86f,.55f),new Vector3(.13f,.035f,.13f),"graphite",PrimitiveType.Sphere);break;
            case "lamp":
                Shape("Base",t,new Vector3(0,.025f,0),new Vector3(.65f,.025f,.65f),"trim",PrimitiveType.Cylinder);
                Shape("Stem",t,new Vector3(0,.43f,0),new Vector3(.065f,.42f,.065f),"graphite",PrimitiveType.Cylinder);
                Shape("Shade",t,new Vector3(0,.85f,0),new Vector3(1,.15f,1),"paper",PrimitiveType.Cylinder);break;
            case "plant":
                Shape("Pot",t,new Vector3(0,.18f,0),new Vector3(.72f,.18f,.72f),"chalk",PrimitiveType.Cylinder);
                for(int i=0;i<4;i++)Shape("Leaf volume",t,new Vector3(Mathf.Sin(i*2)*.15f,.60f+i*.06f,Mathf.Cos(i*2)*.15f),new Vector3(.5f,.5f,.5f),"sage",PrimitiveType.Sphere);break;
            case "boxes":
                Box("Large carton",t,-.12f,.27f,.05f,.76f,.54f,.8f,"oak");Box("Upper carton",t,.10f,.73f,-.10f,.65f,.46f,.64f,"chalk");
                Box("Packing band",t,.1f,.965f,-.1f,.09f,.014f,.65f,"trim");break;
        }
        Solid(g,new Vector3(0,.5f,0),Vector3.one);SaveModule(g,"WB_"+type);
    }
    static void Text(string name,string content,Vector3 pos,float size,Transform p,bool horizontal=true,Color? color=null)
    {
        var g=Group(name,p);g.transform.position=pos;if(horizontal)g.transform.rotation=Quaternion.Euler(90,0,0);
        var text=g.AddComponent<TextMesh>();text.text=content;text.characterSize=size*.72f;text.fontSize=64;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.fontStyle=FontStyle.Bold;text.color=color??Hex("#46565D");
    }
    static string FloorMaterial(float x,float z)
    {if(z>=11&&x<7)return "floorWarm";if(x>18&&z<7)return "floorSage";if(x>=15&&z<7)return "floorBlue";if(x>5&&x<9&&z>4&&z<8)return "floorBlue";return "floor";}
    static void Disk(Transform parent,string name,Vector3 pos,float radius,string mat)
    {Shape(name,parent,pos,new Vector3(radius*2,.006f,radius*2),mat,PrimitiveType.Cylinder);}

    [MenuItem("Night Back/Build Third Night Whitebox")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before rebuilding.");
        if(EditorSceneManager.GetActiveScene().isDirty && EditorSceneManager.GetActiveScene().path!=ScenePath)
            throw new InvalidOperationException("Save the other active scene first.");
        foreach(string f in new[]{"Materials","Prefabs","Scenes","Reports"})Directory.CreateDirectory(Root+"/"+f);
        AssetDatabase.Refresh();
        data=JsonUtility.FromJson<WBLayout>(File.ReadAllText(Root+"/Data/Night3Layout.json"));
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        mats=new Dictionary<string,Material>();kit=new Dictionary<string,GameObject>();uppers=new List<Renderer>();
        foreach(var p in new Dictionary<string,string>{{"chalk","#E2E3DE"},{"paper","#F6F4EC"},{"trim","#A8B2B1"},{"graphite","#425460"},{"slate","#647D88"},{"oak","#C8B89C"},{"fabric","#B5C5C5"},{"sage","#94ACA0"},{"floor","#C6CECE"},{"floorWarm","#D8CCB5"},{"floorBlue","#B7C7D3"},{"floorSage","#BACFC3"},{"base","#354852"},{"accent","#C58C40"},{"target","#509887"}})mats[p.Key]=M(p.Key,Hex(p.Value));
        MakeArchitecture();foreach(var type in data.furniture.Select(p=>p.type).Distinct())MakeFurniture(type);
        var root=Group("NIGHT 03 | FOOTSTEPS CLOSER");architecture=Group("01 Architecture",root.transform).transform;furniture=Group("02 Furniture | 33 source props",root.transform).transform;details=Group("03 Spatial cues",root.transform).transform;annotation=Group("04 Overview labels",root.transform).transform;
        var floors=Group("Floor modules",architecture).transform;var walls=Group("Wall modules",architecture).transform;var openings=Group("Doorway modules",architecture).transform;
        Box("Foundation - main wing",architecture,11.5f,-.24f,7,24.12f,.28f,15.12f,"base");Box("Foundation - return wing",architecture,16.5f,-.24f,16.5f,14.12f,.28f,4.12f,"base");
        foreach(var p in data.floors){var g=Place("WB_Floor_1m",floors,new Vector3(p.x,0,p.z),Vector3.one);g.name=$"Floor [{p.x},{p.z}]";g.GetComponentInChildren<Renderer>().sharedMaterial=mats[FloorMaterial(p.x,p.z)];}
        foreach(var p in data.walls){var g=Place("WB_Wall_1m",walls,new Vector3(p.x,0,p.z),Vector3.one);g.name=$"Wall [{p.x},{p.z}]";uppers.AddRange(g.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Upper")));}
        foreach(var p in data.openings){var g=Place("WB_Doorway_1m",openings,new Vector3(p.x,0,p.z),Vector3.one,p.axis=="z"?90:0);g.name=$"Opening [{p.x},{p.z}]";uppers.AddRange(g.GetComponentsInChildren<Renderer>().Where(r=>r.name.StartsWith("Upper")));}
        var doors=new List<WhiteboxDoor>();foreach(var p in new[]{new Vector2(3,10),new Vector2(11,6)}){var g=Place("WB_HingedDoor",openings,new Vector3(p.x-.47f,0,p.y),Vector3.one);g.name=p.x==3?"Bedroom door":"Old timber door";doors.Add(g.GetComponent<WhiteboxDoor>());}
        foreach(var f in data.furniture){var g=Place("WB_"+f.type,furniture,new Vector3(f.x,0,f.z),new Vector3(f.w,f.h,f.d),f.yaw*Mathf.Rad2Deg);g.name=f.id+(f.cover?" [COVER]":"")+(f.search?" [SEARCH]":"");}
        foreach(var rug in data.rugs)Box("Rug | quiet footing",details,rug.x,.007f,rug.z,rug.w,.012f,rug.d,"fabric");
        // Spatial props are intentionally abstract. No original gameplay is ported.
        var vase=Shape("Vase silhouette",details,new Vector3(9.7f,1.07f,2.8f),new Vector3(.23f,.36f,.23f),"paper",PrimitiveType.Capsule);
        Shape("Vase neck",details,new Vector3(9.7f,1.29f,2.8f),new Vector3(.10f,.07f,.10f),"trim",PrimitiveType.Cylinder);
        Box("Phone | retrieval location",details,12,.887f,17.12f,.12f,.026f,.23f,"graphite");
        Disk(details,"Retrieval marker",new Vector3(12,.877f,17.12f),.22f,"target");
        Box("Desk book",details,15.05f,.89f,5.32f,.32f,.06f,.23f,"oak");
        Box("Table tray",details,20.8f,.84f,4.6f,.50f,.04f,.35f,"trim");
        var parent=Group("Sleeping parent | scale proxy",details);
        Shape("Torso",parent.transform,new Vector3(7.5f,.81f,5.80f),new Vector3(.45f,.24f,.85f),"slate",PrimitiveType.Capsule);
        Shape("Head",parent.transform,new Vector3(7.5f,.83f,5.12f),new Vector3(.25f,.27f,.25f),"oak",PrimitiveType.Sphere);
        Disk(annotation,"Start position",new Vector3(3,.021f,12),.35f,"accent");
        Text("Start label","START / RETURN",new Vector3(3.55f,.035f,13.10f),.09f,annotation);
        Text("Living label","LIVING",new Vector3(8,.035f,1.40f),.18f,annotation);
        Text("Own room label","YOUR ROOM",new Vector3(3.5f,.035f,11.35f),.13f,annotation);
        Text("Parent label","PARENT",new Vector3(7,.035f,7.35f),.105f,annotation);
        Text("Study label","STUDY",new Vector3(16,.035f,2.15f),.14f,annotation);
        Text("Dining label","DINING",new Vector3(20.8f,.035f,2.6f),.14f,annotation);
        Text("Storage label","STORAGE",new Vector3(16.3f,.035f,8.5f),.13f,annotation);
        Text("Utility label","UTILITY",new Vector3(20.8f,.035f,8.7f),.13f,annotation);
        Text("Laundry label","LAUNDRY / TARGET",new Vector3(11.95f,.035f,16.3f),.075f,annotation);
        Text("Return label","RETURN CORRIDOR",new Vector3(18.3f,.035f,16.5f),.12f,annotation);
        var ceiling=Group("Ceiling | full height in walk mode",architecture);
        Box("Main ceiling",ceiling.transform,11.5f,2.65f,7,24,.10f,15,"paper");Box("Return ceiling",ceiling.transform,16.5f,2.65f,16.5f,14,.10f,4,"paper");
        // Bright diffuse study lighting makes geometry readable before any night-time art pass.
        RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=Hex("#DCE4EC");RenderSettings.ambientEquatorColor=Hex("#B8C3C8");RenderSettings.ambientGroundColor=Hex("#7D898D");RenderSettings.ambientIntensity=1;
        RenderSettings.fog=false;QualitySettings.shadows=ShadowQuality.All;QualitySettings.shadowResolution=ShadowResolution.High;QualitySettings.shadowDistance=80;QualitySettings.antiAliasing=4;
        var light=Group("05 Light | diffuse key",root.transform).AddComponent<Light>();light.type=LightType.Directional;light.color=Hex("#FFF1D6");light.intensity=1.05f;light.shadows=LightShadows.Soft;light.shadowStrength=.4f;light.transform.rotation=Quaternion.Euler(48,-32,0);
        // Interior fill lights support a clear eye-level inspection under the ceiling.
        foreach(var p in new[]{new Vector3(3,2.2f,12),new Vector3(3,2.2f,3),new Vector3(11,2.2f,3),new Vector3(11,2.2f,8),new Vector3(7,2.2f,6),new Vector3(16,2.2f,3),new Vector3(21,2.2f,3),new Vector3(16,2.2f,11),new Vector3(21,2.2f,11),new Vector3(12,2.2f,16),new Vector3(20,2.2f,16)})
        {var l=Group("Interior fill",root.transform).AddComponent<Light>();l.transform.position=p;l.type=LightType.Point;l.range=7;l.intensity=.32f;l.color=Hex("#FFF4DD");l.shadows=LightShadows.None;}
        var player=Group("06 Walkthrough | source movement scale",root.transform);player.layer=8;player.transform.position=new Vector3(3,.025f,12);player.transform.rotation=Quaternion.Euler(0,180,0);
        var body=player.AddComponent<CharacterController>();body.radius=.2f;body.height=1.55f;body.center=new Vector3(0,.775f,0);body.skinWidth=.01f;body.stepOffset=.12f;body.minMoveDistance=0;
        var walk=Group("Eye level camera",player.transform).AddComponent<Camera>();walk.transform.localPosition=new Vector3(0,1.37f,0);walk.fieldOfView=78;walk.nearClipPlane=.04f;walk.farClipPlane=80;walk.clearFlags=CameraClearFlags.SolidColor;walk.backgroundColor=Hex("#BAC7CC");walk.gameObject.tag="MainCamera";walk.gameObject.AddComponent<AudioListener>();
        var overview=Group("07 Overview camera",root.transform).AddComponent<Camera>();overview.transform.position=new Vector3(32,32,-20);overview.transform.LookAt(new Vector3(11.5f,0,8.7f));overview.orthographic=true;overview.orthographicSize=14.3f;overview.nearClipPlane=.1f;overview.farClipPlane=120;overview.clearFlags=CameraClearFlags.SolidColor;overview.backgroundColor=Hex("#E8EDEB");
        var tour=player.AddComponent<WhiteboxTour>();tour.body=body;tour.walkCamera=walk;tour.overviewCamera=overview;tour.upperWalls=uppers.ToArray();tour.ceiling=ceiling;tour.annotations=annotation.gameObject;tour.doors=doors.ToArray();tour.SetOverview(true);
        player.AddComponent<WhiteboxWalkCheck>();
        PlayerSettings.companyName="Coursework";PlayerSettings.productName="Get Your Night Back - Night 03 Whitebox";
        PlayerSettings.colorSpace=ColorSpace.Linear;PlayerSettings.defaultScreenWidth=1600;PlayerSettings.defaultScreenHeight=1000;PlayerSettings.runInBackground=true;
        AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(),ScenePath);
        EditorBuildSettings.scenes=new[]{new EditorBuildSettingsScene(ScenePath,true)};
        if(SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.LookAt(new Vector3(11.5f,0,8.5f),Quaternion.Euler(56,-145,0),24);SceneView.lastActiveSceneView.sceneLighting=true;}
        Selection.activeGameObject=root;AssetDatabase.SaveAssets();
        Debug.Log($"WHITEBOX_BUILT modules={kit.Count} furniture={data.furniture.Length} walls={data.walls.Length} floor={data.floors.Length} path={ScenePath}");
    }
}
