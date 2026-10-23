using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[Serializable] public class WBWalkPath { public string name; public Vector3[] points; }
[Serializable] public class WBWalkPaths { public WBWalkPath[] routes; }
[Serializable] public class WBWalkResult { public string room; public bool reached,returned; public float requestedDistance,travelled; public string error; }
[Serializable] public class WBWalkReport { public string time,mode; public bool passed; public List<WBWalkResult> routes=new List<WBWalkResult>(); }

public class WhiteboxWalkCheck : MonoBehaviour
{
    public bool running;
    public WBWalkReport report;
    WhiteboxTour tour;
    public void Begin()
    {
        if(running)return;
        tour=GetComponent<WhiteboxTour>();StartCoroutine(Run());
    }
    IEnumerator Run()
    {
        running=true;tour.automated=true;tour.SetOverview(false);
        var paths=JsonUtility.FromJson<WBWalkPaths>(File.ReadAllText(Application.dataPath+"/Night3Whitebox/Data/WalkRoutes.json"));
        report=new WBWalkReport{time=DateTime.Now.ToString("O"),mode="Accelerated Play Mode CharacterController walk. 20ms movement substeps; physical collision enabled. Not a human usability test."};
        foreach(var d in tour.doors)d.SetImmediate(true);
        tour.ResetPosition();yield return null;
        foreach(var route in paths.routes)
        {
            var result=new WBWalkResult{room=route.name};report.routes.Add(result);
            if(route.points.Length==0){result.error="No route generated";continue;}
            yield return Walk(route.points,result);
            result.reached=result.error==null;
            if(result.reached)
            {
                var reverse=(Vector3[])route.points.Clone();Array.Reverse(reverse);
                yield return Walk(reverse,result);result.returned=result.error==null;
            }
            if(result.error!=null)break;
        }
        report.passed=report.routes.Count==paths.routes.Length&&report.routes.TrueForAll(r=>r.reached&&r.returned);
        string path=Path.Combine(Path.GetDirectoryName(Application.dataPath),"Evidence/playmode-walkthrough.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path));File.WriteAllText(path,JsonUtility.ToJson(report,true));
        foreach(var d in tour.doors)d.SetImmediate(false);
        tour.ResetPosition();tour.SetOverview(true);tour.automated=false;running=false;
        Debug.Log("WHITEBOX_WALKTHROUGH "+report.passed+" rooms="+report.routes.Count+" report="+path);
    }
    IEnumerator Walk(Vector3[] points,WBWalkResult result)
    {
        int operations=0;
        foreach(var target in points)
        {
            int stalled=0;
            for(int step=0;step<120;step++)
            {
                Vector3 delta=target-transform.position;delta.y=0;
                if(delta.magnitude<.025f)break;
                float distance=Mathf.Min(tour.walkSpeed*.02f,delta.magnitude);
                var before=transform.position;
                tour.body.Move(delta.normalized*distance+Vector3.down*.06f);
                Vector3 moved=transform.position-before;moved.y=0;result.travelled+=moved.magnitude;result.requestedDistance+=distance;
                if(delta.sqrMagnitude>.0025f)transform.rotation=Quaternion.LookRotation(delta.normalized);
                if(moved.magnitude<.003f)stalled++;else stalled=0;
                if(stalled>=12){result.error="Blocked at "+transform.position+" approaching "+target;yield break;}
                if(++operations%16==0)yield return null;
            }
            Vector3 left=target-transform.position;left.y=0;
            if(left.magnitude>.08f){result.error="Waypoint not reached: "+target;yield break;}
        }
    }
}
