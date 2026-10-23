using System;
using System.IO;
using System.Collections;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Media;
#endif

[Serializable] public class CourseSegment
{public float duration;public Vector3 from,to;public Quaternion fromRotation,toRotation;public string title;public bool cutaway,cut;}
[Serializable] public class CourseTimelineReport
{public string cameraPrefab;public float eyeHeight,moveSpeed;public CourseSegment[] segments;}
[Serializable] public class CourseCaptureResult
{public string file,camera,prefab,method,date;public int width,height,fps,frames;public float seconds;public bool completed;}

public class CourseFlythroughCapture:MonoBehaviour
{
    public Camera courseCamera;
    public WhiteboxTour whitebox;
    public CourseSegment[] timeline;
    public bool recording;
    public int frame;
    public string status="Ready";
    public string chapter;
    public float fade;
    bool cutaway;
    GUIStyle title,caption;
#if UNITY_EDITOR
    MediaEncoder encoder;
    int previousRate;
    const string Output="CourseCapture/Submission-20260928";
    public void Begin(bool probe)
    {
        if(recording)return;if(!Application.isPlaying)throw new Exception("Play Mode required");
        StartCoroutine(Record(probe));
    }
    IEnumerator Record(bool probe)
    {
        yield return new WaitForEndOfFrame();
        if(Screen.width!=1920||Screen.height!=1080)throw new Exception("Game View must be 1920x1080, actual "+Screen.width+"x"+Screen.height);
        foreach(var door in whitebox.doors)door.SetImmediate(true);
        recording=true;frame=0;status="Recording";previousRate=Time.captureFramerate;Time.captureFramerate=30;
        Directory.CreateDirectory(Output);
        string path=Output+(probe?"/capture-probe.mp4":"/Night03-FlyCamera-Walkthrough.mp4");
        var attr=new VideoTrackEncoderAttributes(new H264EncoderAttributes{gopSize=30,numConsecutiveBFrames=2,profile=VideoEncodingProfile.H264High})
        {frameRate=new MediaRational(30),width=1920,height=1080,targetBitRate=14000000};
        encoder=new MediaEncoder(path,attr);
        try
        {
            foreach(var segment in timeline)
            {
                SetCutaway(segment.cutaway);chapter=segment.title;
                int frames=Mathf.Max(1,Mathf.RoundToInt(segment.duration*30));
                for(int n=0;n<frames;n++)
                {
                    float u=frames==1?1:(float)n/(frames-1);float eased=u*u*(3-2*u);
                    courseCamera.transform.position=Vector3.Lerp(segment.from,segment.to,u);
                    courseCamera.transform.rotation=Quaternion.Slerp(segment.fromRotation,segment.toRotation,eased);
                    courseCamera.fieldOfView=segment.cutaway?35:78;
                    fade=segment.cut?Mathf.Clamp01(1-n/12f):0;
                    yield return new WaitForEndOfFrame();
                    var tex=ScreenCapture.CaptureScreenshotAsTexture();
                    if(tex.width!=1920||tex.height!=1080)throw new Exception("Capture resolution changed");
                    bool accepted=encoder.AddFrame(tex);Destroy(tex);if(!accepted)throw new Exception("Encoder rejected frame "+frame);
                    frame++;status="Recording "+frame+" frames / "+chapter;
                    if(probe&&frame>=60)break;
                }
                if(probe)break;
            }
            encoder.Dispose();encoder=null;
            var report=new CourseCaptureResult{file=path,camera=courseCamera.name,prefab="Assets/StudentTools/FlyCamera/FlyCamera.prefab",method="Play Mode Game View screen capture; original course FlyCamera prefab and controller; scripted camera route; native H264 MediaEncoder; no audio",date=DateTime.Now.ToString("O"),width=1920,height=1080,fps=30,frames=frame,seconds=frame/30f,completed=true};
            File.WriteAllText(Output+(probe?"/probe-report.json":"/capture-report.json"),JsonUtility.ToJson(report,true));
            status="Completed "+path;Debug.Log("COURSE_CAPTURE_COMPLETE "+frame+" frames "+path);
        }
        finally
        {if(encoder!=null){encoder.Dispose();encoder=null;}Time.captureFramerate=previousRate;recording=false;fade=0;}
    }
    void OnDisable(){if(encoder!=null){encoder.Dispose();encoder=null;Time.captureFramerate=previousRate;}recording=false;}
#endif
    public void SetCutaway(bool value)
    {
        cutaway=value;whitebox.ceiling.SetActive(!value);whitebox.annotations.SetActive(value);
        foreach(var r in whitebox.upperWalls)if(r)r.enabled=!value;
        whitebox.walkCamera.enabled=false;whitebox.overviewCamera.enabled=false;whitebox.body.enabled=false;courseCamera.enabled=true;
    }
    void OnGUI()
    {
        if(!recording)return;
        if(title==null)
        {title=new GUIStyle(GUI.skin.label){fontSize=22,fontStyle=FontStyle.Bold};title.normal.textColor=Color.white;caption=new GUIStyle(GUI.skin.label){fontSize=15};caption.normal.textColor=new Color(.81f,.87f,.85f);}
        GUI.color=new Color(.035f,.06f,.075f,.88f);GUI.DrawTexture(new Rect(0,Screen.height-88,Screen.width,88),Texture2D.whiteTexture);GUI.color=Color.white;
        GUI.Label(new Rect(34,Screen.height-77,1500,30),"GET YOUR NIGHT BACK     /     "+chapter,title);
        GUI.Label(new Rect(35,Screen.height-41,1500,25),cutaway?"Third-night modular whitebox  |  Course FlyCamera  |  Cutaway overview":"Course FlyCamera  |  Eye-level walkthrough: 1.37 units  |  Doors opened for the spatial tour",caption);
        if(fade>0){GUI.color=new Color(.035f,.05f,.065f,fade);GUI.DrawTexture(new Rect(0,0,Screen.width,Screen.height),Texture2D.whiteTexture);GUI.color=Color.white;}
    }
}
