// @role Reads controller input and haptic feedback
// @tune deadzone:float:0.05-0.35:Initial controller deadzone 0.16; NightApp replaces it each frame with settings.stickDeadzone; settings UI already supports this range.

using System;
using System.Runtime.InteropServices;
using UnityEngine;
namespace NightBackPort
{
    // Raw controller input lives in this project. No InputManager edits are needed.
    public sealed class NightController
    {
        public float deadzone=.16f;
        public bool connected,dualSense; public readonly float[] values=new float[22];
        float[] previous=new float[22]; bool nativeAvailable=true;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        [DllImport("NightBackController")] static extern int NB_Read([Out] float[] values,int count);
        [DllImport("NightBackController")] static extern void NB_Resistance(float strength);
        [DllImport("NightBackController")] static extern void NB_Pulse(float intensity,float sharpness,float duration);
        [DllImport("NightBackController")] static extern void NB_Stop();
#endif
        public void Poll()
        {
            Array.Copy(values,previous,22); Array.Clear(values,0,22); connected=false;
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if(nativeAvailable)try{connected=NB_Read(values,22)==1;dualSense=values[18]>.5f;}catch(DllNotFoundException){nativeAvailable=false;}catch(EntryPointNotFoundException){nativeAvailable=false;}
#endif
        }
        public bool Held(int i){return values[i]>.5f;} public bool Down(int i){return values[i]>.5f&&previous[i]<=.5f;}
        public Vector2 Move { get{return Dead(new Vector2(values[0],values[1]),1);} }
        public Vector2 Look { get{return Dead(new Vector2(values[2],values[3]),1.5f);} }
        Vector2 Dead(Vector2 v,float curve){float length=v.magnitude,d=Mathf.Clamp(deadzone,.05f,.35f);if(length<=d)return Vector2.zero;return v/length*Mathf.Pow((Mathf.Min(1,length)-d)/(1-d),curve);}
        Vector2 lastMenuDirection;float nextMenuRepeat;
        public Vector2 MenuStep(float now)
        {
            if(!connected){lastMenuDirection=Vector2.zero;nextMenuRepeat=0;return Vector2.zero;}
            Vector2 axis=new Vector2(values[16],values[17]);
            if(axis.sqrMagnitude<.25f)axis=Move;
            Vector2 direction=Mathf.Max(Mathf.Abs(axis.x),Mathf.Abs(axis.y))<.55f?Vector2.zero:
                Mathf.Abs(axis.y)>=Mathf.Abs(axis.x)?new Vector2(0,Mathf.Sign(axis.y)):new Vector2(Mathf.Sign(axis.x),0);
            if(direction==Vector2.zero){lastMenuDirection=direction;nextMenuRepeat=0;return Vector2.zero;}
            if(direction!=lastMenuDirection){lastMenuDirection=direction;nextMenuRepeat=now+.35f;return direction;}
            if(now<nextMenuRepeat)return Vector2.zero;
            nextMenuRepeat=now+.12f;return direction;
        }
        public void Resistance(float value)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if(nativeAvailable&&connected)NB_Resistance(value);
#endif
        }
        public void Pulse(float strength,float sharpness=.4f,float duration=.12f)
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if(nativeAvailable&&connected)NB_Pulse(strength,sharpness,duration);
#endif
        }
        public void Stop()
        {
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            if(nativeAvailable)try{NB_Stop();}catch(DllNotFoundException){nativeAvailable=false;}
#endif
        }
    }
}
