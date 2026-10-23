using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

[Serializable] public class WBCheck { public string name; public bool passed; public string detail; }
[Serializable] public class WBReport { public string editor,scene,time; public int prefabs,furniture,reachableNodes; public bool passed; public List<WBCheck> checks=new List<WBCheck>(); }
[Serializable] public class WBRoute { public string name; public Vector3[] points; }
[Serializable] public class WBRoutes { public WBRoute[] routes; }

public static class WhiteboxValidation
{
    [MenuItem("Night Back/Validate Geometry")]
    static void ValidateMenu(){if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode first.");return;}Validate();}
    [MenuItem("Night Back/Save Evidence Images")]
    static void CaptureMenu(){if(EditorApplication.isPlaying){Debug.LogWarning("Stop Play Mode first.");return;}Capture();}
    [MenuItem("Night Back/Run Play Mode Walk Check")]
    static void WalkMenu(){if(!EditorApplication.isPlaying){Debug.LogWarning("Enter Play Mode first.");return;}UnityEngine.Object.FindObjectOfType<WhiteboxWalkCheck>().Begin();}
    const string Root="Assets/Night3Whitebox";
    const float Step=.25f;
    static Dictionary<Vector2Int,Vector2Int> parents;
    static bool Clear(Vector3 p)
    {
        if(!Physics.Raycast(p+Vector3.up*.2f,Vector3.down,.4f,~(1<<8),QueryTriggerInteraction.Ignore))return false;
        return !Physics.CheckCapsule(p+Vector3.up*.23f,p+Vector3.up*1.32f,.205f,~(1<<8),QueryTriggerInteraction.Ignore);
    }
    static Vector3 Point(Vector2Int k){return new Vector3(k.x*Step,.025f,k.y*Step);}
    static Vector2Int Key(Vector3 p){return new Vector2Int(Mathf.RoundToInt(p.x/Step),Mathf.RoundToInt(p.z/Step));}
    public static Vector3[] Path(Vector3 target)
    {
        var k=Key(target);if(!parents.ContainsKey(k))return new Vector3[0];var path=new List<Vector3>();
        for(int i=0;i<10000;i++){path.Add(Point(k));if(parents[k]==k)break;k=parents[k];}path.Reverse();return path.ToArray();
    }
    public static string Validate()
    {
        var tour=UnityEngine.Object.FindObjectOfType<WhiteboxTour>();if(!tour)throw new Exception("No walkthrough controller");
        bool wasOverview=tour.overview;tour.SetOverview(false);tour.body.enabled=false;
        var report=new WBReport{editor=Application.unityVersion,scene=EditorSceneManager.GetActiveScene().path,time=DateTime.Now.ToString("O")};
        Action<string,bool,string> check=(n,p,d)=>report.checks.Add(new WBCheck{name=n,passed=p,detail=d});
        check("Correct saved scene",report.scene==WhiteboxBuilder.ScenePath,report.scene);
        var all=UnityEngine.Object.FindObjectsOfType<Transform>(true);
        check("No missing scripts",all.All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Checked every scene GameObject");
        report.prefabs=AssetDatabase.FindAssets("t:Prefab",new[]{Root+"/Prefabs"}).Length;
        report.furniture=GameObject.Find("02 Furniture | 33 source props").transform.childCount;
        check("Reusable prefab kit",report.prefabs>=16,report.prefabs+" Prefab assets");
        check("Furniture source count",report.furniture==33,report.furniture+" furniture instances");
        var furniture=GameObject.Find("02 Furniture | 33 source props").transform;
        check("Furniture remains prefab instances",furniture.Cast<Transform>().All(t=>PrefabUtility.IsPartOfPrefabInstance(t.gameObject)),"All 33 direct children checked");
        check("No missing materials",all.SelectMany(t=>t.GetComponents<Renderer>()).All(r=>r.sharedMaterials.All(m=>m&&m.shader)),"Every renderer material and shader resolved");
        foreach(var d in tour.doors)d.SetImmediate(false);Physics.SyncTransforms();
        bool blocked=Physics.CheckCapsule(new Vector3(3,.23f,10),new Vector3(3,1.32f,10),.20f,~(1<<8));
        check("Closed bedroom door blocks body",blocked,"0.40 unit player width at bedroom doorway");
        foreach(var d in tour.doors)d.SetImmediate(true);Physics.SyncTransforms();
        check("Open bedroom door clears body",Clear(new Vector3(3,0,10)),"Door leaf at 88 degrees, fixed wall and jamb colliders");
        parents=new Dictionary<Vector2Int,Vector2Int>();var start=Key(tour.spawn);var q=new Queue<Vector2Int>();q.Enqueue(start);parents[start]=start;
        var blockedCache=new Dictionary<Vector2Int,bool>();
        while(q.Count>0)
        {
            var k=q.Dequeue();
            foreach(var delta in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})
            {
                var n=k+delta;if(n.x<2||n.x>90||n.y<2||n.y>70||parents.ContainsKey(n))continue;
                if(!blockedCache.TryGetValue(n,out bool can)){can=Clear(Point(n));blockedCache[n]=can;}
                if(!can||!Clear((Point(n)+Point(k))*.5f))continue;parents[n]=k;q.Enqueue(n);
            }
        }
        report.reachableNodes=parents.Count;
        var destinations=new Dictionary<string,Vector3>{{"Living room",new Vector3(3,0,4)},{"Parent room",new Vector3(6,0,6)},{"Study",new Vector3(16,0,3)},{"Dining",new Vector3(21,0,3)},{"Storage",new Vector3(16.5f,0,12)},{"Utility",new Vector3(21,0,11.5f)},{"Laundry target approach",new Vector3(12,0,16.5f)},{"Return corridor",new Vector3(21,0,16)}};
        var routes=new List<WBRoute>();
        foreach(var pair in destinations){var path=Path(pair.Value);check("Reachable: "+pair.Key,path.Length>0,path.Length+" quarter-unit nodes from start with actual 3D colliders");routes.Add(new WBRoute{name=pair.Key,points=path});}
        check("Source walk parameters",Mathf.Abs(tour.walkSpeed-2.05f)<.001f&&Mathf.Abs(tour.crouchSpeed-1.05f)<.001f&&Mathf.Abs(tour.body.radius-.2f)<.001f,"Walk 2.05, crouch 1.05, radius .20; 1 unit = 1 metre for this blockout");
        report.passed=report.checks.All(c=>c.passed);
        Directory.CreateDirectory(Root+"/Reports");Directory.CreateDirectory("Evidence");
        string json=JsonUtility.ToJson(report,true);File.WriteAllText("Evidence/geometry-validation.json",json);File.WriteAllText(Root+"/Data/WalkRoutes.json",JsonUtility.ToJson(new WBRoutes{routes=routes.ToArray()},true));
        foreach(var d in tour.doors)d.SetImmediate(false);tour.SetOverview(wasOverview);AssetDatabase.Refresh();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        Debug.Log("WHITEBOX_VALIDATION "+report.passed+" checks="+report.checks.Count+" reachable="+parents.Count);
        return json;
    }
    static void Render(Camera camera,string path,int width=1800,int height=1200)
    {
        var rt=new RenderTexture(width,height,24,RenderTextureFormat.ARGB32);rt.antiAliasing=4;
        var old=camera.targetTexture;var active=RenderTexture.active;float oldAspect=camera.aspect;
        camera.targetTexture=rt;camera.aspect=(float)width/height;camera.Render();RenderTexture.active=rt;
        var tex=new Texture2D(width,height,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,width,height),0,0);tex.Apply();
        File.WriteAllBytes(path,tex.EncodeToPNG());camera.targetTexture=old;camera.aspect=oldAspect;RenderTexture.active=active;
        UnityEngine.Object.DestroyImmediate(tex);rt.Release();UnityEngine.Object.DestroyImmediate(rt);
    }
    public static string Capture()
    {
        Directory.CreateDirectory("Evidence");var tour=UnityEngine.Object.FindObjectOfType<WhiteboxTour>();bool was=tour.overview;
        tour.SetOverview(true);Render(tour.overviewCamera,"Evidence/01-overview.png");
        var p=tour.overviewCamera.transform.position;var r=tour.overviewCamera.transform.rotation;float size=tour.overviewCamera.orthographicSize;
        tour.overviewCamera.transform.position=new Vector3(11.5f,38,9);tour.overviewCamera.transform.rotation=Quaternion.Euler(90,0,0);tour.overviewCamera.orthographicSize=11.8f;
        Render(tour.overviewCamera,"Evidence/02-floor-plan.png",1800,1300);tour.overviewCamera.transform.SetPositionAndRotation(p,r);tour.overviewCamera.orthographicSize=size;
        tour.SetOverview(false);var wp=tour.walkCamera.transform.position;var wr=tour.walkCamera.transform.rotation;
        var poses=new[]{new Vector3(3.7f,1.37f,12.8f),new Vector3(11,1.37f,8.7f),new Vector3(16.5f,1.37f,2.9f),new Vector3(12,1.37f,16.1f)};
        var aims=new[]{new Vector3(3,1.15f,10),new Vector3(14,1.1f,9),new Vector3(15.1f,1,5),new Vector3(12,.86f,17.12f)};
        for(int i=0;i<poses.Length;i++){tour.walkCamera.transform.position=poses[i];tour.walkCamera.transform.LookAt(aims[i]);Render(tour.walkCamera,$"Evidence/0{i+3}-eye-level.png",1600,1000);}
        tour.walkCamera.transform.SetPositionAndRotation(wp,wr);tour.SetOverview(was);EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
        return "Evidence/01-overview.png; 02-floor-plan.png; 03-06-eye-level.png";
    }
}
