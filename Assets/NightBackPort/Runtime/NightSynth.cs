// @role Synthesizes local audio clips

using System;
using UnityEngine;
namespace NightBackPort
{
    // The browser's original oscillators/noise envelopes rendered into local native PCM.
    public static class NightSynth
    {
        const int Rate=24000;
        sealed class Mix
        {
            public readonly float[] data=new float[Rate*3];readonly System.Random random=new System.Random(271828);
            public float end;
            public void Note(float start,float hz,float duration,float level,string shape="sine",float endHz=0)
            {
                end=Mathf.Max(end,start+duration+.03f);double phase=0;
                for(int i=0;i<(duration+.03f)*Rate;i++) {float t=i/(float)Rate;if(t>duration)continue;
                    float f=endHz>0?hz*Mathf.Pow(endHz/hz,t/duration):hz;phase+=f/Rate;float p=(float)(phase-Math.Floor(phase));
                    float x=shape=="triangle"?1-4*Mathf.Abs(p-.5f):shape=="sawtooth"?2*p-1:Mathf.Sin(p*Mathf.PI*2);
                    float e=t<.012f?Mathf.Lerp(.0001f,level,t/.012f):level*Mathf.Pow(.0001f/Mathf.Max(.0002f,level),(t-.012f)/Mathf.Max(.001f,duration-.012f));
                    int at=Mathf.RoundToInt(start*Rate)+i;if(at<data.Length)data[at]+=x*e;
                }
            }
            public void Noise(float start,float duration,float level,float frequency,float q=1)
            {
                end=Mathf.Max(end,start+duration);double omega=2*Math.PI*frequency/Rate,alpha=Math.Sin(omega)/(2*q),a0=1+alpha,b0=alpha/a0,b2=-alpha/a0,a1=-2*Math.Cos(omega)/a0,a2=(1-alpha)/a0,x1=0,x2=0,y1=0,y2=0;
                for(int i=0;i<duration*Rate;i++){float t=i/(float)Rate;double x=random.NextDouble()*2-1,y=b0*x+b2*x2-a1*y1-a2*y2;x2=x1;x1=x;y2=y1;y1=y;float attack=Mathf.Min(.06f,duration*.2f),e=t<attack?t/attack:Mathf.Pow(.0001f/level,(t-attack)/(duration-attack));int at=Mathf.RoundToInt(start*Rate)+i;if(at<data.Length)data[at]+=(float)y*level*e;}
            }
            public AudioClip Clip(string name){int n=Mathf.Min(data.Length,Mathf.CeilToInt(Mathf.Max(.1f,end)*Rate));var pcm=new float[n];Array.Copy(data,pcm,n);var c=AudioClip.Create(name,n,1,Rate,false);c.SetData(pcm,0);return c;}
        }
        public static float Hz(int n){return 440*Mathf.Pow(2,(n-69)/12f);}
        public static AudioClip Music(int note,bool bass=false)
        {var m=new Mix();if(bass){m.Note(0,Hz(note),1.8f,.075f,"triangle");m.Note(.025f,Hz(note+7),1.6f,.035f);}else{m.Note(0,Hz(note),1.25f,.105f);m.Note(0,Hz(note)*2,.38f,.023f);}return m.Clip("music"+note);}
        public static AudioClip Effect(string kind)
        {
            var m=new Mix();switch(kind){
            case "lockPin":m.Note(0,1550,.065f,.035f,"triangle");m.Noise(0,.024f,.065f,2600,3);break;
            case "lockScrape":m.Noise(0,.15f,.14f,1850,2.5f);m.Note(0,380,.11f,.024f,"triangle");break;
            case "catMeow":case "catChirp":m.Note(0,kind=="catMeow"?480:700,.12f,.045f,"sawtooth",kind=="catMeow"?780:1000);m.Note(.10f,kind=="catMeow"?780:1000,.44f,.035f,"sawtooth",340);m.Noise(0,.5f,.015f,1400,1.3f);break;
            case "catchTouch":m.Noise(0,.12f,.07f,600,.8f);break;
            case "parentGiggle":for(int i=0;i<3;i++){m.Note(i*.13f,190+i*32,.1f,.045f,"triangle");m.Noise(i*.13f,.09f,.055f,750,1.2f);}break;
            case "catPurr":for(int i=0;i<24;i++)m.Note(i*.045f,65,.04f,.035f,"triangle");m.Noise(0,1.1f,.055f,180,1.2f);break;
            case "catToy":for(int i=0;i<3;i++){m.Note(i*.13f,150+i*50,.08f,.035f,"triangle");m.Noise(i*.13f,.05f,.04f,500);}break;
            case "catHop":m.Noise(0,.12f,.06f,420);break;
            case "radio":m.Noise(0,.35f,.08f,900,.8f);var f=new[]{330,440,392,494};for(int i=0;i<4;i++)m.Note(i*.2f,f[i],.24f,.1f,"triangle");break;
            case "toy":for(int i=0;i<2;i++){m.Note(i*.35f,620-i*140,.18f,.12f,"triangle");m.Noise(i*.35f,.15f,.1f,1050,3);}break;
            case "phoneBuzz":for(int i=0;i<2;i++){m.Note(i*.18f,110,.12f,.045f,"sawtooth");m.Noise(i*.18f,.1f,.04f,240,2);}break;
            case "phoneRing":for(int i=0;i<4;i++)m.Note(i*.18f,i%2==0?660:880,.13f,.14f);break;
            case "phoneMute":case "switch":m.Noise(0,.06f,.04f,1200);if(kind=="phoneMute")m.Note(0,390,.16f,.035f);break;
            case "pencilDrop":for(int i=0;i<5;i++){m.Note(i*.07f,320+i*73,.08f,.055f);m.Noise(i*.06f,.09f,.1f,850+i*120);}break;
            case "bed":case "creak":m.Noise(0,.8f,.28f,kind=="bed"?190:400,7);m.Note(0,190,.48f,.105f,"triangle");m.Note(.13f,148,.52f,.063f,"triangle");break;
            case "crash":m.Note(0,86,.35f,.22f);m.Noise(0,.6f,.42f,1900);break;
            case "wobble":for(int i=0;i<3;i++)m.Note(i*.15f,740-i*60,.22f,.08f,"triangle");break;
            case "cloth":m.Noise(0,.4f,.05f,1250,.6f);break;
            case "tension":m.Note(0,82,.18f,.095f,"triangle");break;
            default:var notes=kind=="win"?new[]{67,71,74,79}:kind=="lose"?new[]{57,54,50}:kind=="found"?new[]{69,76}:kind=="notice"?new[]{80,81}:new[]{64,71};for(int i=0;i<notes.Length;i++)m.Note(i*.12f,Hz(notes[i]),.4f,.12f);break;
            }return m.Clip(kind);
        }
        public static AudioClip Segment(AudioClip source,string name,float offset,float duration,float volume)
        {
            source.LoadAudioData();int count=Mathf.Min(Mathf.RoundToInt(duration*source.frequency),source.samples-Mathf.RoundToInt(offset*source.frequency));
            var data=new float[count*source.channels];source.GetData(data,Mathf.RoundToInt(offset*source.frequency));
            for(int i=0;i<data.Length;i++){float t=(i/source.channels)/(float)source.frequency,end=count/(float)source.frequency;data[i]*=volume*Mathf.Clamp01(Mathf.Min(t/.012f,(end-t)/.05f));}
            var c=AudioClip.Create(name,count,source.channels,source.frequency,false);c.SetData(data,0);return c;
        }
    }
}
