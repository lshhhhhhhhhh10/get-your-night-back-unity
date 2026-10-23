// @role Sequences the readable bedroom story walls
// @calls NightHomeWalls NightText
// @tune ReadingDurationScale:float:2.2-2.2:Baseline multiplier for story-wall reading duration; not changed.

using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Three continuous plaster surfaces with closed, bevelled relief glyphs.
    // This component never moves the camera or intercepts gameplay input.
    public sealed class NightStoryWall : MonoBehaviour
    {
        // Keep the authored glyph and line-break rhythm, stretched for comfortable reading.
        public const float ReadingDurationScale = 2.2f;
        public static readonly Vector3[] MountPositions={new Vector3(3.50f,1.42f,13.478f),new Vector3(6.488f,1.42f,12.0f),new Vector3(4.995f,1.42f,10.534f)};
        static readonly float[] WallYaw={0,90,180};
        [Serializable] public class SectionTiming {public float duration;public int glyphs;}
        [Serializable] public class StoryTiming {public SectionTiming[] zh,en;}
        NightApp app;Transform visual;Material wallMaterial;readonly Transform[] walls=new Transform[3];
        readonly Material[] letters=new Material[3];readonly Renderer[] textRenderers=new Renderer[3];
        readonly float[] elapsed=new float[3];StoryTiming timing;bool english,started,overview;
        public int SectionCount {get{return walls.Length;}}
        public bool IsEnglish {get{return english;}}
        public bool Started {get{return started;}}
        public Transform Visual {get{return visual;}}
        public bool Revealed {get{for(int i=0;i<3;i++)if(elapsed[i]<DurationAt(i))return false;return true;}}
        public float DurationAt(int i){return (english?timing.en:timing.zh)[i].duration * ReadingDurationScale;}
        public float TimeAt(int i){return elapsed[i];}
        public Transform GetWall(int i){return walls[i];}
        public Bounds TextBounds(int i){return textRenderers[i].bounds;}

        public void Build(NightApp owner,bool resumed)
        {
            app=owner;timing=JsonUtility.FromJson<StoryTiming>(Resources.Load<TextAsset>("NightBack/StoryWall/timing").text);
            visual=new GameObject("Three walls · after lights out").transform;visual.SetParent(transform,false);
            wallMaterial=new Material(Resources.Load<Shader>("NightBack/StoryWall/StoryWall"));
            wallMaterial.SetTexture("_PlasterTex",NightHomeWalls.PlasterTexture);
            wallMaterial.SetTexture("_PlasterNormal",NightHomeWalls.PlasterNormal);
            for(int i=0;i<3;i++){
                walls[i]=new GameObject(new[]{"01 · The house rule","02 · Your own time","03 · Tonight's plan"}[i]).transform;
                walls[i].SetParent(visual,false);walls[i].localPosition=MountPositions[i];walls[i].localRotation=Quaternion.Euler(0,WallYaw[i],0);
                letters[i]=new Material(wallMaterial);letters[i].SetFloat("_Letters",1);letters[i].renderQueue=2001;
                BuildPart(walls[i],"Continuous fine plaster",wallMaterial);
                textRenderers[i]=BuildPart(walls[i],"Original rounded font · bevelled relief",letters[i]);
            }
            SelectLanguage(NightText.english);started=resumed;
            for(int i=0;i<3;i++)elapsed[i]=resumed?DurationAt(i):0;
            ApplyReveal();SetOverview(false);
        }
        Renderer BuildPart(Transform parent,string name,Material material)
        {
            var part=new GameObject(name);part.transform.SetParent(parent,false);part.AddComponent<MeshFilter>();
            var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        void SelectLanguage(bool value)
        {
            english=value;string code=english?"en":"zh";
            for(int i=0;i<3;i++){
                walls[i].GetChild(0).GetComponent<MeshFilter>().sharedMesh=Resources.Load<Mesh>("NightBack/StoryWall/"+code+"-"+i+"-wall");
                walls[i].GetChild(1).GetComponent<MeshFilter>().sharedMesh=Resources.Load<Mesh>("NightBack/StoryWall/"+code+"-"+i+"-letters");
            }
        }
        public void RefreshLanguage(bool value)
        {
            var fractions=new float[3];for(int i=0;i<3;i++)fractions[i]=Mathf.Clamp01(elapsed[i]/DurationAt(i));
            SelectLanguage(value);for(int i=0;i<3;i++)elapsed[i]=fractions[i]*DurationAt(i);ApplyReveal();
        }
        public void Begin(){started=false;for(int i=0;i<3;i++)elapsed[i]=0;SelectLanguage(NightText.english);ApplyReveal();SetOverview(overview);}
        public void StartExploration(){started=true;SetOverview(overview);}
        public void SetOverview(bool value){overview=value;if(visual)visual.gameObject.SetActive(started&&!overview);}
        void ApplyReveal(){for(int i=0;i<3;i++)letters[i].SetFloat("_RevealTime",elapsed[i] / ReadingDurationScale);}
        public bool CanRead(int i,Camera camera)
        {
            if(!started||overview||!camera)return false;
            var toward=camera.transform.position-TextBounds(i).center;
            return toward.magnitude<4.8f&&Vector3.Dot(toward.normalized,-walls[i].forward)>.025f&&GeometryUtility.TestPlanesAABB(GeometryUtility.CalculateFrustumPlanes(camera),TextBounds(i));
        }
        public void Tick(float dt,Camera camera)
        {
            if(!started||overview||app.page!="playing"||app.paused||app.game.mode!=null||dt<=0)return;
            // Only the next unfinished wall may write. Looking elsewhere never skips a wall.
            for(int i=0;i<3;i++){
                if(elapsed[i]>=DurationAt(i))continue;
                if(CanRead(i,camera))elapsed[i]=Mathf.Min(DurationAt(i),elapsed[i]+dt);
                break;
            }
            ApplyReveal();
        }
        void OnDestroy()
        {
            foreach(var m in letters)if(m){if(Application.isPlaying)Destroy(m);else DestroyImmediate(m);}
            if(wallMaterial){if(Application.isPlaying)Destroy(wallMaterial);else DestroyImmediate(wallMaterial);}
        }
    }
}
