// @role Plays spatial sounds, ambience and musical layers
// @calls NightSynth NightHearingMap NightSoundCaptions

using System;
using System.Collections.Generic;
using UnityEngine;
namespace NightBackPort
{
    // Local recordings and procedural source effects share propagation with parent hearing.
    public sealed class NightAudio : MonoBehaviour
    {
        public NightApp app;public int loadedSamples,playedEvents;public List<string> sampleErrors=new List<string>();
        class Voice{public AudioSource source;public NightBandFilter filter;public string kind;public Vector3 position;public float volume;public bool paused,ui;public AudioLowPassFilter lowPass;}
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();readonly Dictionary<string,int> footSequences=new Dictionary<string,int>();
        readonly List<Voice> voices=new List<Voice>();readonly Dictionary<string,Voice> ambience=new Dictionary<string,Voice>();
        public readonly NightHearingMap hearingMap=new NightHearingMap();
        readonly NightSoundCaptions soundCaptions=new NightSoundCaptions();
        public NightSoundCaptions Captions {get{return soundCaptions;}}
        public string CurrentCaption {get{return app==null?"":soundCaptions.Read(app.game,app.yaw*Mathf.Deg2Rad,Time.unscaledTime,app.page=="playing"&&!app.paused,NightText.english);}}
        AudioSource music;Transform listener;float nextNote,parentCueUntil;int beat,floorSequence;NightGame lastGame;bool musicPaused;
        static readonly int[] Melody={74,0,77,81,0,77,72,0,70,0,74,77,0,74,69,0,67,0,70,74,0,70,65,0,69,0,72,76,0,72,73,0};
        void Awake()
        {
            if(app==null)app=GetComponent<NightApp>();
            foreach(var c in Resources.LoadAll<AudioClip>("NightBack/Audio")){clips[c.name]=c;loadedSamples++;}
            foreach(string name in new[]{"door-handle","hinge-real","door-bump","door-latch","drawer","metal-drop","snore-real","washer-spin","floor-creak-1"})if(!clips.ContainsKey(name))sampleErrors.Add(name);
            foreach(string name in new[]{"win","lose","found","notice","cloth","lockPin","lockScrape","catMeow","catChirp","catchTouch","parentGiggle","catPurr","catToy","catHop","radio","toy","phoneBuzz","phoneRing","phoneMute","switch","pencilDrop","bed","creak","crash","wobble","clock","tension"})clips[name]=NightSynth.Effect(name);
            Segment("doorHandle","door-handle",0,.7f,.7f);Segment("doorSoft","door-hinge",.15f,.64f,.18f);Segment("doorCreak","door-hinge",.10f,.67f,.66f);
            Segment("doorBump","door-bump",.305f,.34f,3);Segment("latch","door-latch",.32f,.32f,.55f);Segment("search","drawer",0,.55f,.22f);
            for(int i=1;i<=3;i++){Segment("floorPressure"+i,"floor-creak-"+i,0,.32f,1);Segment("floorSoft"+i,"floor-creak-"+i,0,.68f,1);}
            for(int i=0;i<36;i++)voices.Add(NewVoice("One shot "+i));
            ambience["snore"]=NewVoice("Parent breathing");ambience["washer"]=NewVoice("Washing machine");ambience["hinge"]=NewVoice("Door friction");ambience["hinge"].lowPass=ambience["hinge"].source.gameObject.AddComponent<AudioLowPassFilter>();
            music=new GameObject("Original night melody").AddComponent<AudioSource>();music.transform.SetParent(transform);music.spatialBlend=0;music.playOnAwake=false;
            foreach(int n in Melody)if(n>0&&!clips.ContainsKey("note"+n))clips["note"+n]=NightSynth.Music(n);
            foreach(int n in new[]{50,46,43,45})clips["bass"+n]=NightSynth.Music(n,true);
            // Listener stays at the player's ears through overview and close-up cameras.
            if(app.viewCamera){var cameraListener=app.viewCamera.GetComponent<AudioListener>();if(cameraListener)cameraListener.enabled=false;}
            listener=new GameObject("Player ears").transform;listener.SetParent(transform);listener.gameObject.AddComponent<AudioListener>();
        }
        void Segment(string key,string sample,float offset,float seconds,float gain){if(clips.ContainsKey(sample))clips[key]=NightSynth.Segment(clips[sample],key,offset,seconds,gain);}
        Voice NewVoice(string name)
        {var o=new GameObject(name);o.transform.SetParent(transform);var s=o.AddComponent<AudioSource>();s.playOnAwake=false;s.spatialBlend=1;s.rolloffMode=AudioRolloffMode.Custom;s.SetCustomCurve(AudioSourceCurveType.CustomRolloff,AnimationCurve.Linear(0,1,100,1));s.maxDistance=100;s.dopplerLevel=0;s.spread=0;return new Voice{source=s,filter=o.AddComponent<NightBandFilter>()};}
        public void Consume(NightGame game)
        {
            if(lastGame!=game){StopAll();lastGame=game;}
            foreach(var e in game.events){if(!string.IsNullOrEmpty(e.text)||e.strength<=0)continue;soundCaptions.Observe(game,e,Time.unscaledTime);hearingMap.Observe(game,e);Play(game,e);}
            game.events.Clear();
        }
        string Sample(NightGame game,NightEvent e,out float gain,out float rate)
        {
            string k=e.kind;gain=1;rate=1;
            if(k=="step"||k=="parentStep"||k=="crouchStep"||k=="tileStep"){
                string floor=game.FloorAt(e.position);int seq;footSequences.TryGetValue(floor,out seq);footSequences[floor]=seq+1;
                gain=(k=="parentStep"?.92f:k=="crouchStep"?.07f:.27f)*(floor=="carpet"?.58f:1);rate=(k=="parentStep"?.9f:1.04f)+(seq%3-1)*.035f;
                return "step-"+floor+"-"+new[]{1,3,2,4}[seq%4];
            }
            if(k.StartsWith("floor")){gain=k=="floorCreak"?.82f:k=="floorPressure"?.12f:.27f*Mathf.Clamp(e.strength/25,.02f,1);return (k=="floorPressure"||k=="floorSoft"?k:"floor-creak-")+(floorSequence++%3+1);}
            if(k=="doorSoft")rate=.94f;if(k=="doorCreak")rate=.75f;if(k=="latch")rate=1.05f;if(k=="search")rate=.9f;
            if(k=="doorBump")gain=.18f+Mathf.Clamp01((e.strength-24)/66)*.82f;
            if(k=="metalDrop"){gain=.8f*Mathf.Clamp(e.strength/65,.05f,1);rate=.93f;return "metal-drop";}
            if(k=="crash"||k=="pencilDrop")gain=Mathf.Clamp(e.strength/(k=="crash"?90:40),.05f,1);
            if(k=="snore"||k=="washer"||k=="hingeMotion")return null;
            return clips.ContainsKey(k)?k:"cloth";
        }
        void Play(NightGame game,NightEvent e)
        {
            float gain,rate;string sample=Sample(game,e,out gain,out rate);if(sample==null||!clips.ContainsKey(sample))return;
            var v=voices.Find(a=>!a.source.isPlaying&&!a.paused)??voices[playedEvents%voices.Count];v.source.Stop();v.paused=false;v.kind=e.kind;v.position=e.position;v.source.clip=clips[sample];
            v.ui=e.kind=="win"||e.kind=="lose"||e.kind=="found"||e.kind=="notice";v.source.spatialBlend=v.ui?0:1;
            v.volume=gain;v.source.pitch=rate;v.source.loop=false;ApplyPath(game,v);v.source.Play();playedEvents++;
            if(e.kind=="parentStep")parentCueUntil=Time.unscaledTime+.95f;
            if(app.settings.haptics&&e.kind!="step"&&e.kind!="parentStep"&&e.kind!="crouchStep")app.controller.Pulse(Mathf.Clamp01(e.strength/70f)*.5f*app.settings.hapticIntensity);
        }
        void ApplyPath(NightGame game,Voice v)
        {
            if(v.ui){v.filter.low=v.filter.mid=v.filter.high=1;v.source.volume=app.settings.effects*app.settings.master;return;}
            var path=game.SoundPath(v.kind,v.position,game.ListenerPosition);float max=Mathf.Max(.0001f,Mathf.Max(path.bands.x,Mathf.Max(path.bands.y,path.bands.z)));
            v.filter.low=path.bands.x/max;v.filter.mid=path.bands.y/max;v.filter.high=path.bands.z/max;
            var arrival=path.arrival;arrival.y=Mathf.Max(.15f,arrival.y);v.source.transform.position=arrival;
            float volumeSetting=v.kind=="snore"||v.kind=="washer"?app.settings.ambience:app.settings.effects;
            v.source.volume=Mathf.Clamp01(v.volume*max*volumeSetting*app.settings.master);
        }
        void Update()
        {
            if(app==null||app.game==null)return;var g=app.game;
            listener.position=g.ListenerPosition;listener.rotation=Quaternion.Euler(0,app.yaw,0);
            bool run=app.page=="playing"&&!app.paused&&g.active, result=app.page=="result";
            if(!run){soundCaptions.Clear();hearingMap.Clear();foreach(var v in voices)if(v.source.isPlaying&&!(result&&v.ui)){v.source.Pause();v.paused=true;}foreach(var v in ambience.Values)v.source.Stop();if(music.isPlaying){music.Pause();musicPaused=true;}return;}
            foreach(var v in voices){if(v.paused){v.source.UnPause();v.paused=false;}if(v.source.isPlaying)ApplyPath(g,v);}
            if(musicPaused){music.UnPause();musicPaused=false;nextNote=Time.unscaledTime+.2f;}
            var active=new HashSet<string>();foreach(var env in g.EnvironmentSources()){
                if(!env.active||!ambience.ContainsKey(env.kind))continue;active.Add(env.kind);var v=ambience[env.kind];v.kind=env.kind;v.position=env.position;v.volume=env.gain*(env.kind=="snore"?.90f:.65f);
                v.source.clip=clips[env.kind=="snore"?"snore-real":"washer-spin"];v.source.loop=true;v.source.pitch=g.IsCinematic?.12f:1;ApplyPath(g,v);if(!v.source.isPlaying){v.source.time=Mathf.Clamp(env.age%v.source.clip.length,0,v.source.clip.length-.01f);v.source.Play();}
            }
            var hinge=ambience["hinge"];
            if(g.mode!=null&&g.mode.type=="door"&&g.mode.index>=0&&g.mode.index<g.doors.Count&&g.doors[g.mode.index].speed>.005f){
                var d=g.doors[g.mode.index];active.Add("hinge");hinge.kind="hingeMotion";hinge.position=d.Position;hinge.volume=.14f+g.mode.balance*.95f;hinge.lowPass.cutoffFrequency=1100+g.mode.balance*4500;hinge.source.clip=clips["hinge-real"];hinge.source.pitch=.78f+d.speed*.40f;hinge.source.loop=true;ApplyPath(g,hinge);if(!hinge.source.isPlaying)hinge.source.Play();
            }
            foreach(var pair in ambience)if(!active.Contains(pair.Key))pair.Value.source.Stop();
            bool tension=g.parent.state!="sleep";float duck=g.mode!=null&&g.mode.type=="door"?.25f:active.Count>0?.24f:Time.unscaledTime<parentCueUntil?.30f:1;
            music.volume=app.settings.music*app.settings.master*duck*(tension?.40f:.72f);
            if(Time.unscaledTime>nextNote){nextNote=Time.unscaledTime+.625f;int note=Melody[beat%Melody.Length];music.panStereo=Mathf.Sin(beat)*.25f;if(note>0)music.PlayOneShot(clips["note"+note]);if(beat%4==0)music.PlayOneShot(clips["bass"+new[]{50,46,43,45}[(beat/8)%4]]);if(tension&&beat%2==0)music.PlayOneShot(clips["tension"]);beat++;}
        }
        public void StopAll(){soundCaptions.Clear();hearingMap.Clear();foreach(var v in voices){v.source.Stop();v.paused=false;}foreach(var v in ambience.Values)v.source.Stop();if(music)music.Stop();musicPaused=false;}
        void OnDisable(){StopAll();}
    }
    public sealed class NightBandFilter:MonoBehaviour
    {
        public volatile float low=1,mid=1,high=1;float[] l=new float[8],h=new float[8];float a,b;
        void Awake(){float rate=AudioSettings.outputSampleRate;a=1-Mathf.Exp(-2*Mathf.PI*650/rate);b=1-Mathf.Exp(-2*Mathf.PI*3500/rate);}
        void OnAudioFilterRead(float[] data,int channels){float lo=low,mi=mid,hi=high;for(int i=0;i<data.Length;i++){int c=i%channels;if(c>=8)continue;float x=data[i];l[c]+=a*(x-l[c]);h[c]+=b*(x-h[c]);data[i]=l[c]*lo+(h[c]-l[c])*mi+(x-h[c])*hi;}}
    }
}
