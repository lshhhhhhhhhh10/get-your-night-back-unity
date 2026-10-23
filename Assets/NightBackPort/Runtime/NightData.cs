// @role Loads night layouts and holds game-state data

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    [Serializable] public class NightLevel
    {
        public int index,width,depth; public string name,subtitle,description;
        public Vector3 home,parentHome,parentBed;
        public Vector2[] walls,floors,creaks; public NightOpening[] openings;
        public NightFurniture[] furniture; public NightSpot[] spots;
        public int device; public float stepWidth; public bool nightVisit;
        public static NightLevel Load(int index) { var text=Resources.Load<TextAsset>("NightBack/level"+index); if(!text) throw new Exception("Missing NightBack level "+index); var level=JsonUtility.FromJson<NightLevel>(text.text);
            if(index<3){
                level.name="9/"+(index+1);level.home=new Vector3(3.30f,0,11.85f);
                foreach(var item in level.furniture){
                    if(item.id=="child-bed"){item.x=1.75f;item.z=12.5f;item.w=2.22f;item.d=1.67f;item.h=.76f;item.yaw=0;}
                    if(item.id=="bedroom-lamp"){item.x=.715f;item.z=11.2f;item.w=.422f;item.d=.422f;item.h=1.55f;}
                    if(item.id=="bedside"){item.x=1.16f;item.z=11.1f;item.w=1.205f;item.d=1.095f;item.h=.81f;item.yaw=0;}
                }
                var furniture=new List<NightFurniture>(level.furniture);
                furniture.Add(new NightFurniture{id="bedroom-left-cabinet",type="cabinet",x=3.36f,z=13.12f,w=.685f,d=.695f,h=.69f,cover=true});
                furniture.Add(new NightFurniture{id="bedroom-chest",type="cabinet",x=5.8f,z=11.15f,w=.795f,d=.705f,h=.78f,cover=true});
                level.furniture=furniture.ToArray();
            }
            return level; }
    }
    [Serializable] public class NightOpening { public float x,z; public string axis,name; }
    [Serializable] public class NightFurniture
    {
        public string id,type,name,color; public float x,z,w,d,h,yaw; public bool cover,search,vase,warm;
        public Vector3 Position { get { return new Vector3(x,0,z); } }
    }
    [Serializable] public class NightSpot
    {
        public string id,name; public float x,z; public bool searched,device,unlocked,incidentUsed;
        public Vector3 Position { get { return new Vector3(x,0,z); } }
    }
    [Serializable] public class NightDoor
    {
        public string name; public float x,z,progress,speed,force; public int direction=1; public bool impacted;
        public Vector3 Position { get { return new Vector3(x,0,z); } }
    }
    [Serializable] public class NightParent
    {
        public Vector3 position,target; public float heading,alert,recognition,timer,lastRetarget;
        public string state="sleep",intent="patrol",phase="rest";
        public List<Vector3> route=new List<Vector3>(); public List<Vector3> itinerary=new List<Vector3>();
    }
    [Serializable] public class NightMode
    {
        public string type,id,hint,phase,kind; public float elapsed,progress,pointer,force,balance,hold,timeout,lift,torque,returnX,returnY;
        public int index,stage,pin; public bool caught,ready; public Vector3 target; public float[] values; public bool[] solved;
    }
    [Serializable] public class NightMetrics
    {
        public int steps,goodSteps,loudSounds,searches,rescued,failedRescues,doors,doorBumps,maskedSteps;
        public float noiseBurden,exposure; public int score; public string grade;
    }
    [Serializable] public class NightEvent
    {
        public string kind,text; public float strength,time; public Vector3 position;
    }
    [Serializable] public class NightCat
    {
        public Vector3 position,target; public string state="idle"; public float timer,trust,heading; public bool jumpUsed,met;
    }
    [Serializable] public class NightPhone { public string state="idle"; public float timer,hold; public int rings; public bool armed; }
    [Serializable] public struct NightInput
    {
        public Vector2 move,look; public float force,leftForce,rightForce;
        public bool analog,actionPressed,actionHeld,confirm,reverse,cancel,crouchPressed,petPressed,toyPressed,muteHeld;
    }
}
