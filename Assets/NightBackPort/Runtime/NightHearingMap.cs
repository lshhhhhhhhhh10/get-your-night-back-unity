// @role Shows audible footsteps and snoring on the map
// @calls NightAcoustics

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightBackPort
{
    // Listener-side cues only: no parent position tracking and no changes to hearing or AI.
    public sealed class NightHearingMap
    {
        public struct Cue {public string kind;public Vector3 arrival;public float strength,age;}
        struct Step {public Vector3 position;public float at,amount;}
        readonly List<Step> steps=new List<Step>();
        readonly List<Cue> visible=new List<Cue>();
        NightGame owner;
        public void Clear(){owner=null;steps.Clear();visible.Clear();}
        void Bind(NightGame game){if(ReferenceEquals(owner,game))return;Clear();owner=game;}
        public void Observe(NightGame game,NightEvent sound)
        {
            Bind(game);
            if(game==null||!game.active||sound==null||sound.kind!="parentStep"||sound.strength<=0||!string.IsNullOrEmpty(sound.text))return;
            steps.Add(new Step{position=sound.position,at=game.time,amount=sound.strength});
            if(steps.Count>4)steps.RemoveAt(0);
        }
        public static Vector2 Direction(Vector3 arrival,Vector3 listener,float yaw)
        {
            Vector3 d=arrival-listener;float a=yaw*Mathf.Deg2Rad;
            return new Vector2(d.x*Mathf.Cos(a)-d.z*Mathf.Sin(a),-(d.x*Mathf.Sin(a)+d.z*Mathf.Cos(a)));
        }
        void Add(NightGame game,string kind,Vector3 source,float gain,float age,float fade,float amount=40)
        {
            var path=game.SoundPath(kind,source,game.ListenerPosition);
            var bands=NightAcoustics.Multiply(NightAcoustics.EmittedBands(kind,amount,game.FloorAt(source)),path.bands)*gain;
            float level=NightAcoustics.BandLevel(bands);
            // Relative acoustic energy, before user volume controls; accessibility still works when muted.
            float strength=Mathf.Clamp01((Mathf.Sqrt(Mathf.Max(0,level))-.035f)/.21f)*fade;
            if(strength>.025f)visible.Add(new Cue{kind=kind,arrival=path.arrival,strength=strength,age=age});
        }
        public IList<Cue> Read(NightGame game,bool active)
        {
            Bind(game);visible.Clear();
            if(game==null||!game.active||game.status!="playing"||!active){steps.Clear();return visible;}
            steps.RemoveAll(s=>game.time-s.at>1.05f||game.time<s.at);
            foreach(var s in steps){float age=game.time-s.at;Add(game,"parentStep",s.position,1,age,Mathf.Pow(1-age/1.05f,1.5f),s.amount);}
            foreach(var env in game.EnvironmentSources())if(env.kind=="snore"&&env.active)
                Add(game,"snore",env.position,env.gain,env.age,1);
            return visible;
        }
    }

    public sealed class NightMap : NightDrawing
    {
        public NightApp app;
        public NightMap(){generateVisualContent+=Draw;}
        Color Ink(float r,float g,float b,float alpha){return new Color(r,g,b,alpha);}
        void Shape(Vector2[] points,Color color)
        {
            if(points.Length<3)return;var mesh=context.Allocate(points.Length,(points.Length-2)*3);
            foreach(var p in points)mesh.SetNextVertex(new Vertex{position=new Vector3(p.x,p.y,Vertex.nearZ),tint=color});
            for(int i=1;i<points.Length-1;i++){mesh.SetNextIndex(0);mesh.SetNextIndex((ushort)i);mesh.SetNextIndex((ushort)(i+1));}
        }
        void Line(Vector2 a,Vector2 b,float width,Color color)
        {Vector2 d=(b-a).normalized,n=new Vector2(-d.y,d.x)*width*.5f;Shape(new[]{a-n,b-n,b+n,a+n},color);}
        void Arc(Vector2 center,float radius,float from,float to,float width,Color color)
        {int count=Mathf.Max(6,Mathf.CeilToInt((to-from)/12));for(int i=0;i<count;i++){float a=Mathf.Lerp(from,to,i/(float)count)*Mathf.Deg2Rad,b=Mathf.Lerp(from,to,(i+1)/(float)count)*Mathf.Deg2Rad;Line(center+new Vector2(Mathf.Cos(a),Mathf.Sin(a))*radius,center+new Vector2(Mathf.Cos(b),Mathf.Sin(b))*radius,width,color);}}
        void Foot(Vector2 center,float scale,Color color)
        {
            Shape(new[]{center+new Vector2(-2,-5)*scale,center+new Vector2(2,-6)*scale,center+new Vector2(3,0)*scale,center+new Vector2(-2,1)*scale},color);
            Shape(new[]{center+new Vector2(-2,3)*scale,center+new Vector2(2,3)*scale,center+new Vector2(2,6)*scale,center+new Vector2(-1,6)*scale},color);
        }
        void Draw(MeshGenerationContext c)
        {
            if(app==null||app.game==null)return;context=c;var g=app.game;float w=contentRect.width;
            Vector2 center=new Vector2(w*.5f,w*.5f);float radius=w*.43f,scale=w/10.8f;
            Vector2 Map(Vector3 pos){return center+NightHearingMap.Direction(pos,g.player,app.yaw)*scale;}
            // Tiles rotate with the look direction; the gold player marker always faces up.
            int px=Mathf.RoundToInt(g.player.x),pz=Mathf.RoundToInt(g.player.z);
            for(int z=pz-6;z<=pz+6;z++)for(int x=px-6;x<=px+6;x++){
                Vector3 pos=new Vector3(x,0,z);if(NightGeometry.Distance(pos,g.player)>4.15f)continue;
                bool wall=false;foreach(var point in g.level.walls)if(Mathf.RoundToInt(point.x)==x&&Mathf.RoundToInt(point.y)==z){wall=true;break;}
                Shape(new[]{Map(pos+new Vector3(-.46f,0,-.46f)),Map(pos+new Vector3(-.46f,0,.46f)),Map(pos+new Vector3(.46f,0,.46f)),Map(pos+new Vector3(.46f,0,-.46f))},wall?Ink(.29f,.39f,.45f,.68f):Ink(.14f,.22f,.28f,.38f));
            }
            foreach(var f in g.level.furniture){var pos=new Vector3(f.x,0,f.z);if(NightGeometry.Distance(pos,g.player)>3.9f)continue;Vector2 at=Map(pos);Quad(at.x-2,at.y-2,4,4,Ink(.41f,.53f,.50f,.45f));}
            Arc(center,radius,0,360,1,Ink(.55f,.71f,.70f,.22f));
            Line(center+new Vector2(0,-radius-3),center+new Vector2(0,-radius+3),2,Ink(.88f,.83f,.66f,.6f));
            Shape(new[]{center+new Vector2(0,-7),center+new Vector2(5,5),center+new Vector2(0,3),center+new Vector2(-5,5)},Ink(1,.80f,.43f,1));
            if(!app.audioSystem)return;
            var cues=app.audioSystem.hearingMap.Read(g,app.page=="playing"&&!app.paused);
            foreach(var cue in cues){
                Vector2 delta=NightHearingMap.Direction(cue.arrival,g.ListenerPosition,app.yaw),offset=delta*scale;
                bool edge=offset.magnitude>radius-15;offset=Vector2.ClampMagnitude(offset,radius-15);
                if(offset.magnitude<18)offset=(offset.sqrMagnitude>.01f?offset.normalized:Vector2.up)*18;
                Vector2 at=center+offset;bool snore=cue.kind=="snore";
                float alpha=Mathf.Clamp01(cue.strength*1.5f);
                Color ink=snore?Ink(.60f,.86f,.82f,alpha):Ink(1,.77f,.44f,alpha);
                float angle=Mathf.Atan2(offset.y,offset.x)*Mathf.Rad2Deg;
                Arc(center,radius,angle-9,angle+9,1+cue.strength*2,ink);
                if(edge){Vector2 d=offset.normalized,n=new Vector2(-d.y,d.x);Line(at+d*9-n*3,at+d*12,1.4f,ink);Line(at+d*12,at+d*9+n*3,1.4f,ink);}
                if(snore){
                    // Two curved breath marks remain distinct from the alternating footprint pair.
                    Arc(at+new Vector2(-3,0),4,-80,85,1.7f,ink);Arc(at+new Vector2(3,0),4,-80,85,1.7f,ink);
                }else{Foot(at+new Vector2(-4,-2),.62f,ink);Foot(at+new Vector2(4,2),.62f,ink);}
                int rings=1+Mathf.FloorToInt(cue.strength*2.8f);
                for(int n=0;n<rings;n++){float phase=Mathf.Repeat(cue.age*(snore?.75f:1.15f)+n*.28f,1);var wave=ink;wave.a*=.45f*(1-phase);Arc(at,7+phase*(5+cue.strength*8),0,360,.8f+cue.strength*.65f,wave);}
            }
        }
    }
}
