#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;

public static class CourseCaptureAuthoring
{
    public const string ScenePath="Assets/CourseCapture/Night03_CourseFlythrough.scene";
    public const string Output="CourseCapture/Submission-20260928";
    const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance;
    [MenuItem("Night Back/Course Capture/Prepare Recording Scene")]
    public static void Prepare()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first");
        var original=EditorSceneManager.GetActiveScene();
        if(original.path!=WhiteboxBuilder.ScenePath && original.path!=ScenePath)throw new Exception("Unexpected scene");
        Directory.CreateDirectory("Assets/CourseCapture");
        if(original.path!=ScenePath){EditorSceneManager.SaveScene(original,ScenePath,true);EditorSceneManager.OpenScene(ScenePath);}
        var tour=UnityEngine.Object.FindObjectOfType<WhiteboxTour>();
        tour.SetOverview(false);tour.body.enabled=false;tour.enabled=false;
        tour.walkCamera.enabled=false;tour.overviewCamera.enabled=false;
        foreach(var a in tour.GetComponentsInChildren<AudioListener>())a.enabled=false;
        foreach(var d in tour.doors){d.SetImmediate(true);PrefabUtility.RecordPrefabInstancePropertyModifications(d);PrefabUtility.RecordPrefabInstancePropertyModifications(d.transform);}
        GameObject fly=GameObject.Find("Course FlyCamera");
        if(!fly)fly=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/StudentTools/FlyCamera/FlyCamera.prefab"));
        fly.name="Course FlyCamera";
        int removed=0;foreach(var t in fly.GetComponentsInChildren<Transform>(true))removed+=GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
        var controller=fly.GetComponent<FlyCameraController>();var so=new SerializedObject(controller);
        so.FindProperty("moveSpeed").floatValue=2.05f;so.FindProperty("showControlsHint").boolValue=false;so.ApplyModifiedPropertiesWithoutUndo();
        var cam=fly.GetComponent<Camera>();cam.fieldOfView=78;cam.nearClipPlane=.04f;cam.farClipPlane=120;
        cam.clearFlags=CameraClearFlags.SolidColor;cam.backgroundColor=new Color(.91f,.93f,.92f);cam.enabled=true;cam.allowHDR=false;cam.allowMSAA=true;
        fly.transform.position=new Vector3(3,1.37f,12);fly.transform.rotation=Quaternion.Euler(0,180,0);
        var capture=fly.GetComponent<CourseFlythroughCapture>();if(!capture)capture=fly.AddComponent<CourseFlythroughCapture>();
        capture.courseCamera=cam;capture.whitebox=tour;capture.enabled=true;
        Physics.SyncTransforms();
        capture.timeline=MakeTimeline();
        Directory.CreateDirectory(Output+"/Screenshots");
        File.WriteAllText(Output+"/recording-plan.json",JsonUtility.ToJson(new CourseTimelineReport{cameraPrefab="Assets/StudentTools/FlyCamera/FlyCamera.prefab",eyeHeight=1.37f,moveSpeed=1.6f,segments=capture.timeline},true));
        SetResolution();EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());AssetDatabase.SaveAssets();
        Debug.Log("COURSE_CAPTURE_READY segments="+capture.timeline.Length+" seconds="+capture.timeline.Sum(s=>s.duration)+" missingURPRemoved="+removed);
    }
    public static void SetResolution()
    {
        var asm=typeof(Editor).Assembly;var sizesType=asm.GetType("UnityEditor.GameViewSizes");
        var singleton=typeof(ScriptableSingleton<>).MakeGenericType(sizesType);
        var sizes=singleton.GetProperty("instance",BindingFlags.Public|BindingFlags.Static).GetValue(null);
        var group=sizesType.GetProperty("currentGroup",Flags).GetValue(sizes);
        var count=(int)group.GetType().GetMethod("GetTotalCount").Invoke(group,null);
        var sizeType=asm.GetType("UnityEditor.GameViewSize");var enumType=asm.GetType("UnityEditor.GameViewSizeType");
        var size=Activator.CreateInstance(sizeType,new object[]{Enum.Parse(enumType,"FixedResolution"),1920,1080,"Course Capture 1080p"});
        group.GetType().GetMethod("AddCustomSize").Invoke(group,new[]{size});
        var view=EditorWindow.GetWindow(asm.GetType("UnityEditor.GameView"));
        view.GetType().GetProperty("selectedSizeIndex",Flags).SetValue(view,count);view.Show();view.Focus();
    }
    static bool Clear(Vector3 p)
    {
        return Physics.Raycast(p+Vector3.up*.2f,Vector3.down,.4f,~(1<<8)) && !Physics.CheckCapsule(p+Vector3.up*.23f,p+Vector3.up*1.32f,.205f,~(1<<8),QueryTriggerInteraction.Ignore);
    }
    static Vector3 P(Vector2Int p){return new Vector3(p.x*.25f,.025f,p.y*.25f);}
    static Vector2Int K(Vector3 p){return new Vector2Int(Mathf.RoundToInt(p.x*4),Mathf.RoundToInt(p.z*4));}
    static Vector3[] Route(Vector3 from,Vector3 to)
    {
        var start=K(from);var goal=K(to);var parents=new Dictionary<Vector2Int,Vector2Int>();var visited=new HashSet<Vector2Int>();var q=new Queue<Vector2Int>();parents[start]=start;visited.Add(start);q.Enqueue(start);
        while(q.Count>0&&!parents.ContainsKey(goal))
        {
            var k=q.Dequeue();foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.right,Vector2Int.left})
            {var n=k+d;if(n.x<2||n.x>90||n.y<2||n.y>70||visited.Contains(n))continue;visited.Add(n);if(!Clear(P(n))||!Clear((P(n)+P(k))*.5f))continue;parents[n]=k;q.Enqueue(n);}
        }
        if(!parents.ContainsKey(goal))throw new Exception("No collision-safe route to "+to);
        var path=new List<Vector3>();for(var k=goal;;k=parents[k]){path.Add(P(k));if(k==start)break;}path.Reverse();
        var compact=new List<Vector3>{path[0]};for(int i=1;i<path.Count-1;i++)if((path[i]-path[i-1]).normalized!=(path[i+1]-path[i]).normalized)compact.Add(path[i]);compact.Add(path[path.Count-1]);
        return compact.Select(p=>new Vector3(p.x,1.37f,p.z)).ToArray();
    }
    static CourseSegment[] MakeTimeline()
    {
        var list=new List<CourseSegment>();Vector3 position=new Vector3(3,1.37f,12);Quaternion rotation=Quaternion.Euler(0,180,0);
        Action<float,Vector3,Quaternion,string,bool,bool> add=(seconds,p,r,title,cutaway,cut)=>{list.Add(new CourseSegment{duration=seconds,from=position,to=p,fromRotation=rotation,toRotation=r,title=title,cutaway=cutaway,cut=cut});position=p;rotation=r;};
        Func<Vector3,Vector3,Quaternion> look=(p,target)=>Quaternion.LookRotation(target-p);
        position=new Vector3(32,29,-20);rotation=look(position,new Vector3(11.5f,-2.5f,8.7f));
        add(7,new Vector3(29,27,-18),look(new Vector3(29,27,-18),new Vector3(11.5f,-2.5f,8.7f)),"01   SPATIAL OVERVIEW",true,false);
        position=new Vector3(3,1.37f,12);rotation=Quaternion.Euler(0,180,0);
        add(2.5f,position,rotation,"02   YOUR ROOM / DEPARTURE",false,true);
        var rooms=new[]{
            new {p=new Vector3(3,0,4),target=new Vector3(1.3f,.8f,2.7f),title="03   LIVING ROOM"},
            new {p=new Vector3(6,0,6),target=new Vector3(7.5f,.85f,5.65f),title="04   PARENT ROOM / SIGHTLINES"},
            new {p=new Vector3(16,0,3),target=new Vector3(15.05f,.85f,5.3f),title="05   STUDY / NARROW PASSAGE"},
            new {p=new Vector3(21,0,3),target=new Vector3(20.8f,.8f,4.6f),title="06   DINING ROOM"},
            new {p=new Vector3(21,0,11.5f),target=new Vector3(22.12f,.9f,10),title="07   UTILITY / FURNITURE CLEARANCE"},
            new {p=new Vector3(21,0,16),target=new Vector3(17,1.1f,16),title="08   RETURN CORRIDOR"},
            new {p=new Vector3(12,0,16.25f),target=new Vector3(12,.86f,17.12f),title="09   LAUNDRY / RETRIEVAL POINT"},
            new {p=new Vector3(16.5f,0,12),target=new Vector3(14.84f,1.15f,10.8f),title="10   STORAGE / ALTERNATIVE ROUTE"},
            new {p=new Vector3(3,0,12),target=new Vector3(1.75f,.9f,12),title="11   BACK TO YOUR ROOM"}};
        foreach(var room in rooms)
        {
            var path=Route(position,room.p);
            for(int i=1;i<path.Length;i++)
            {
                Vector3 delta=path[i]-position;if(delta.magnitude<.01f)continue;var face=Quaternion.LookRotation(delta.normalized);
                float degrees=Quaternion.Angle(rotation,face);if(degrees>3)add(Mathf.Max(.45f,degrees/105),position,face,room.title,false,false);
                add(delta.magnitude/1.6f,path[i],face,room.title,false,false);
            }
            var aim=look(position,room.target);add(Mathf.Max(.6f,Quaternion.Angle(rotation,aim)/85),position,aim,room.title,false,false);
            add(2.6f,position,aim,room.title,false,false);
        }
        position=new Vector3(29,27,-18);rotation=look(position,new Vector3(11.5f,-2.5f,8.7f));
        add(5,new Vector3(32,29,-20),look(new Vector3(32,29,-20),new Vector3(11.5f,-2.5f,8.7f)),"12   NIGHT 03 / MODULAR WHITEBOX",true,true);
        return list.ToArray();
    }
    [MenuItem("Night Back/Course Capture/Record Flythrough")]
    public static void Record(){if(!EditorApplication.isPlaying)throw new Exception("Enter Play Mode first");UnityEngine.Object.FindObjectOfType<CourseFlythroughCapture>().Begin(false);}
    public static string CaptureStills()
    {
        var c=UnityEngine.Object.FindObjectOfType<CourseFlythroughCapture>();if(!c)throw new Exception("Open recording scene first");
        var cam=c.courseCamera;var p=cam.transform.position;var r=cam.transform.rotation;float fov=cam.fieldOfView;bool old=c.whitebox.overview;
        var shots=new[]{
            new {name="01-Overview",p=new Vector3(32,29,-20),target=new Vector3(11.5f,-2.5f,8.7f),cut=true,fov=35f},
            new {name="02-Top-View",p=new Vector3(11.5f,31,9),target=new Vector3(11.5f,0,9.01f),cut=true,fov=45f},
            new {name="03-Your-Room",p=new Vector3(4.3f,1.37f,12.8f),target=new Vector3(2.5f,.9f,11.4f),cut=false,fov=78f},
            new {name="04-Living-Room",p=new Vector3(3.5f,1.37f,4.4f),target=new Vector3(1.8f,.8f,2.6f),cut=false,fov=78f},
            new {name="05-Parent-Room",p=new Vector3(6,1.37f,6),target=new Vector3(7.5f,.85f,5.65f),cut=false,fov=78f},
            new {name="06-Study",p=new Vector3(16.5f,1.37f,2.9f),target=new Vector3(15.05f,.9f,5.3f),cut=false,fov=78f},
            new {name="07-Dining",p=new Vector3(21,1.37f,2.8f),target=new Vector3(20.8f,.8f,4.6f),cut=false,fov=78f},
            new {name="08-Storage",p=new Vector3(16.5f,1.37f,12),target=new Vector3(14.9f,1.1f,10.8f),cut=false,fov=78f},
            new {name="09-Laundry-Target",p=new Vector3(12,1.37f,16.1f),target=new Vector3(12,.86f,17.12f),cut=false,fov=78f},
            new {name="10-Return-Corridor",p=new Vector3(20,1.37f,16.1f),target=new Vector3(16,1.1f,16),cut=false,fov=78f}};
        Directory.CreateDirectory(Output+"/Screenshots");
        foreach(var s in shots)
        {
            c.SetCutaway(s.cut);cam.transform.position=s.p;cam.transform.LookAt(s.target);cam.fieldOfView=s.fov;
            var rt=new RenderTexture(1920,1080,24){antiAliasing=4};var prev=RenderTexture.active;var target=cam.targetTexture;
            cam.targetTexture=rt;cam.aspect=16f/9;cam.Render();RenderTexture.active=rt;
            var tex=new Texture2D(1920,1080,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1920,1080),0,0);tex.Apply();
            File.WriteAllBytes(Output+"/Screenshots/"+s.name+".png",tex.EncodeToPNG());
            cam.targetTexture=target;RenderTexture.active=prev;UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
        }
        cam.transform.SetPositionAndRotation(p,r);cam.fieldOfView=fov;c.SetCutaway(old);return "10 course-camera screenshots saved at 1920x1080";
    }
}
#endif
