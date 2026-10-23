// @role Assembles the shared household art style
// @calls NightHomeComfort NightHomeProps NightHomeWalls NightHomeWood

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace NightBackPort
{
    // Shared, deterministic modelling kit. Rebuilds only the visual layer; data owns all navigation/acoustics.
    public static class NightHomeArt
    {
        static readonly Dictionary<string,Material> palette=new Dictionary<string,Material>();
        static readonly Dictionary<string,Mesh> primitives=new Dictionary<string,Mesh>();
        public static Transform Group(Transform parent,string name){var g=new GameObject(name.StartsWith("Art")?name:"Art "+name);g.transform.SetParent(parent,false);return g.transform;}
        public static Material Material(string key)
        {
            Material m;if(palette.TryGetValue(key,out m)&&m)return m;
            var shader=Resources.Load<Shader>("NightBack/NativeSurface");if(!shader)shader=Shader.Find("Standard");
            m=new Material(shader);m.name="Home Studio - "+key;Color c;float rough=.72f;
            switch(key){case "wood":c=new Color(.48f,.29f,.16f);rough=.56f;break;case "walnut":c=new Color(.23f,.135f,.087f);rough=.6f;break;
            case "linen":c=new Color(.72f,.68f,.59f);rough=.93f;break;case "navy":c=new Color(.14f,.22f,.34f);rough=.92f;break;
            case "sage":c=new Color(.30f,.40f,.36f);rough=.89f;break;case "brass":c=new Color(.57f,.37f,.16f);rough=.43f;break;
            case "shade":c=new Color(.89f,.72f,.47f);rough=.88f;break;case "ceramic":c=new Color(.69f,.70f,.64f);rough=.37f;break;case "charcoal":c=new Color(.047f,.064f,.084f);rough=.48f;break;
            case "paper":c=new Color(.79f,.73f,.62f);rough=.94f;break;case "leather":c=new Color(.37f,.235f,.16f);rough=.8f;break;
            default:c=new Color(.4f,.4f,.4f);break;}
            m.color=c;m.SetFloat("_Glossiness",1-rough);m.SetFloat("_Metallic",key=="brass"?.52f:0);m.SetFloat("_UseVertexColor",1);
            if(key=="wood"||key=="walnut"||key=="linen"||key=="navy"||key=="sage"||key=="paper"||key=="leather"){
                var texture=new Texture2D(128,128,TextureFormat.RGB24,true);texture.name="Home Studio grain - "+key;texture.wrapMode=TextureWrapMode.Repeat;texture.filterMode=FilterMode.Trilinear;texture.anisoLevel=2;
                var pixels=new Color[128*128];bool wood=key=="wood"||key=="walnut";
                for(int y=0;y<128;y++)for(int x=0;x<128;x++){
                    float n=Mathf.PerlinNoise(x*(wood?.032f:.39f)+12.8f,y*(wood?.24f:.39f)+4.7f);
                    float thread=wood?Mathf.Sin((y+3*Mathf.Sin(x*.035f))*1.8f)*.025f:((x%2==0?1:-1)+(y%2==0?1:-1))*.012f;
                    float v=.88f+n*.17f+thread;pixels[y*128+x]=new Color(v,v,v,1);
                }texture.SetPixels(pixels);texture.Apply();m.mainTexture=texture;
            }
            if(key=="shade"){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",new Color(.18f,.095f,.027f));}
            palette[key]=m;return m;
        }
        static Transform MeshObject(Transform parent,string name,Vector3 pos,Mesh mesh,string material)
        {var t=Group(parent,name);t.localPosition=pos;t.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;var r=t.gameObject.AddComponent<MeshRenderer>();r.sharedMaterial=Material(material);r.shadowCastingMode=ShadowCastingMode.On;return t;}
        public static Transform Box(Transform p,string name,Vector3 pos,Vector3 size,string material,float bevel=.015f)
        {
            size=new Vector3(Mathf.Max(.001f,size.x),Mathf.Max(.001f,size.y),Mathf.Max(.001f,size.z));bevel=Mathf.Clamp(bevel,.0001f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.45f);
            string key="b"+size.ToString("F5")+bevel.ToString("F5",System.Globalization.CultureInfo.InvariantCulture);Mesh mesh;
            if(!primitives.TryGetValue(key,out mesh)||!mesh){mesh=RoundedBox(size,bevel);primitives[key]=mesh;}
            return MeshObject(p,name,pos,mesh,material);
        }
        static Mesh RoundedBox(Vector3 size,float b)
        {
            var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var colors=new List<Color>();var tri=new List<int>();Vector3 half=size*.5f,inner=half-Vector3.one*b;
            Vector3[] normals={Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back};
            foreach(Vector3 normal in normals){Vector3 u=Mathf.Abs(normal.y)>.5f?Vector3.right:Vector3.Cross(Vector3.up,normal);Vector3 w=Vector3.Cross(normal,u);float a=Vector3.Dot(half,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z))),c=Vector3.Dot(half,new Vector3(Mathf.Abs(w.x),Mathf.Abs(w.y),Mathf.Abs(w.z)));float extent=Vector3.Dot(half,new Vector3(Mathf.Abs(normal.x),Mathf.Abs(normal.y),Mathf.Abs(normal.z)));
                float[] us={-a,-a+b,a-b,a},vs={-c,-c+b,c-b,c};int start=v.Count;
                for(int j=0;j<4;j++)for(int i=0;i<4;i++){Vector3 q=normal*extent+u*us[i]+w*vs[j];Vector3 core=new Vector3(Mathf.Clamp(q.x,-inner.x,inner.x),Mathf.Clamp(q.y,-inner.y,inner.y),Mathf.Clamp(q.z,-inner.z,inner.z));Vector3 d=(q-core).normalized;Vector3 point=core+d*b;v.Add(point);n.Add(d);uv.Add(new Vector2((us[i]+a)*1.5f,(vs[j]+c)*1.5f));float shade=Mathf.Lerp(.76f,1,Mathf.InverseLerp(-half.y,half.y,point.y));colors.Add(new Color(shade,shade,shade,1));}
                for(int j=0;j<3;j++)for(int i=0;i<3;i++){int k=start+j*4+i;tri.Add(k);tri.Add(k+1);tri.Add(k+5);tri.Add(k);tri.Add(k+5);tri.Add(k+4);}
            }var m=new Mesh{name="Home Studio beveled solid"};m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetColors(colors);m.SetTriangles(tri,0);m.RecalculateBounds();return m;
        }
        public static Transform Cylinder(Transform p,string name,Vector3 pos,float radius,float height,string material,int sides=16)
        {
            string key="c"+radius.ToString("F5")+":"+height.ToString("F5")+":"+sides;Mesh mesh;
            if(!primitives.TryGetValue(key,out mesh)||!mesh){var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var tr=new List<int>();
                for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;Vector3 normal=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));for(int j=0;j<2;j++){v.Add(normal*radius+Vector3.up*(j==0?-height*.5f:height*.5f));n.Add(normal);uv.Add(new Vector2((float)i/sides,j));}}
                for(int i=0;i<sides;i++){int k=i*2;tr.Add(k);tr.Add(k+1);tr.Add(k+3);tr.Add(k);tr.Add(k+3);tr.Add(k+2);}
                for(int side=0;side<2;side++){int s=v.Count;Vector3 normal=side==0?Vector3.down:Vector3.up;v.Add(normal*height*.5f);n.Add(normal);uv.Add(Vector2.one*.5f);for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;v.Add(new Vector3(Mathf.Cos(a)*radius,normal.y*height*.5f,Mathf.Sin(a)*radius));n.Add(normal);uv.Add(new Vector2(Mathf.Cos(a)*.5f+.5f,Mathf.Sin(a)*.5f+.5f));}for(int i=0;i<sides;i++){tr.Add(s);tr.Add(s+i+(side==0?1:2));tr.Add(s+i+(side==0?2:1));}}
                mesh=new Mesh{name="Home Studio turned cylinder"};mesh.SetVertices(v);mesh.SetNormals(n);mesh.SetUVs(0,uv);mesh.SetTriangles(tr,0);mesh.RecalculateBounds();primitives[key]=mesh;
            }return MeshObject(p,name,pos,mesh,material);
        }
        public static Transform Sphere(Transform p,string name,Vector3 pos,Vector3 size,string material)
        {
            Mesh m;const string key="ellipsoid";if(!primitives.TryGetValue(key,out m)||!m){var v=new List<Vector3>();var n=new List<Vector3>();var uv=new List<Vector2>();var t=new List<int>();const int rings=8,segs=16;
                for(int y=0;y<=rings;y++){float phi=y*Mathf.PI/rings;for(int x=0;x<=segs;x++){float a=x*2*Mathf.PI/segs;Vector3 normal=new Vector3(Mathf.Sin(phi)*Mathf.Cos(a),Mathf.Cos(phi),Mathf.Sin(phi)*Mathf.Sin(a));v.Add(normal*.5f);n.Add(normal);uv.Add(new Vector2((float)x/segs,(float)y/rings));}}
                for(int y=0;y<rings;y++)for(int x=0;x<segs;x++){int k=y*(segs+1)+x;t.Add(k);t.Add(k+1);t.Add(k+segs+2);t.Add(k);t.Add(k+segs+2);t.Add(k+segs+1);}m=new Mesh{name="Home Studio soft form"};m.SetVertices(v);m.SetNormals(n);m.SetUVs(0,uv);m.SetTriangles(t,0);m.RecalculateBounds();primitives[key]=m;
            }var tr=MeshObject(p,name,pos,m,material);tr.localScale=size;return tr;
        }
        static bool Animated(Transform t){string s=t.name;return s.StartsWith("Drawer:")||s.StartsWith("Book:")||s.StartsWith("IncidentFrame:")||s.StartsWith("IncidentObject:")||s.StartsWith("Pencil:")||s=="TinLid"||s=="Flowers"||s=="Padlock"||s.StartsWith("Lure:");}
        static bool RetainOriginal(Transform t,Transform root){for(var a=t;a&&a!=root;a=a.parent){string n=a.name;if(n.StartsWith("Incident")||n.StartsWith("Pencil:")||n=="TinLid"||n=="Flowers"||n=="Padlock")return true;}return false;}
        public static void Finish(Transform root)
        {
            // Each animation pivot is its own batching island. Close-up clones keep the same live hierarchy.
            var islands=new Dictionary<Transform,Dictionary<Material,List<MeshFilter>>>();
            foreach(var f in root.GetComponentsInChildren<MeshFilter>(true)){
                if(!f.name.StartsWith("Art")||!f.sharedMesh||f.name.StartsWith("Art Batch"))continue;var r=f.GetComponent<MeshRenderer>();if(!r||!r.enabled)continue;
                Transform anchor=root;for(var a=f.transform.parent;a&&a!=root;a=a.parent)if(Animated(a)){anchor=a;break;}
                Dictionary<Material,List<MeshFilter>> mats;if(!islands.TryGetValue(anchor,out mats)){mats=new Dictionary<Material,List<MeshFilter>>();islands[anchor]=mats;}List<MeshFilter> list;if(!mats.TryGetValue(r.sharedMaterial,out list)){list=new List<MeshFilter>();mats[r.sharedMaterial]=list;}list.Add(f);
            }
            foreach(var island in islands)foreach(var pair in island.Value){var combines=new List<CombineInstance>();foreach(var f in pair.Value)combines.Add(new CombineInstance{mesh=f.sharedMesh,transform=island.Key.worldToLocalMatrix*f.transform.localToWorldMatrix});var mesh=new Mesh{name="Home Studio batch - "+root.name+" - "+pair.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines.ToArray(),true,true);mesh.RecalculateBounds();var g=Group(island.Key,"Art Batch "+pair.Key.name);g.gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;g.gameObject.AddComponent<MeshRenderer>().sharedMaterial=pair.Key;var owner=root.GetComponent<NightHomeArtResources>();if(!owner)owner=root.gameObject.AddComponent<NightHomeArtResources>();owner.meshes.Add(mesh);foreach(var f in pair.Value){f.GetComponent<MeshRenderer>().enabled=false;if(f.transform.childCount==0){if(Application.isPlaying)UnityEngine.Object.Destroy(f.gameObject);else UnityEngine.Object.DestroyImmediate(f.gameObject);}}}
        }
        static void DressSurfaces(Transform house)
        {
            foreach(var r in house.GetComponentsInChildren<MeshRenderer>(true)){
                bool floor=r.transform.parent==house;bool shell=r.transform.parent&&(r.transform.parent.name=="FullWalls"||r.transform.parent.name=="OverviewWalls"||r.transform.parent.name=="Roof");
                int slot;if((!floor&&!shell)||!r.name.StartsWith("Material ")||!int.TryParse(r.name.Substring(9),out slot))continue;
                if(!(floor&&slot>=2&&slot<=10)&&!(shell&&slot>=127&&slot<=131))continue;
                string key="surface-"+slot;Material mat;if(!palette.TryGetValue(key,out mat)||!mat){mat=new Material(r.sharedMaterial);mat.name="Home Studio - "+key;
                    if(slot<=10){mat.mainTexture=Material("wood").mainTexture;mat.SetFloat("_Glossiness",.12f);}
                    else if(slot==127){mat.color=new Color(.105f,.16f,.235f);mat.SetFloat("_Glossiness",.08f);}
                    else if(slot==128)mat.color=new Color(.27f,.31f,.36f);
                    else if(slot==129)mat.color=new Color(.49f,.47f,.44f);
                    else {mat.color=Material(slot==130?"wood":"walnut").color;mat.mainTexture=Material("wood").mainTexture;}
                    palette[key]=mat;
                }r.sharedMaterial=mat;
            }
        }
        public static void Apply(Transform house,NightGame game)
        {
            DressSurfaces(house);
            var nodes=house.GetComponentsInChildren<Transform>(true);var lookup=new Dictionary<string,Transform>();foreach(var t in nodes)if(!lookup.ContainsKey(t.name))lookup[t.name]=t;
            foreach(var f in game.level.furniture){if(!game.IsTutorial&&(f.id=="child-bed"||f.id=="bedside"||f.id=="bedroom-lamp"))continue;Transform root;if(!lookup.TryGetValue("Furniture:"+f.id,out root))continue;
                foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))if(!RetainOriginal(r.transform,root))r.enabled=false;
                bool rebuilt=NightHomeWood.Build(root,f)||NightHomeComfort.Build(root,f);if(!rebuilt)foreach(var r in root.GetComponentsInChildren<MeshRenderer>(true))r.enabled=true;
                Finish(root);
            }
            NightHomeProps.Apply(house);
            Finish(house);
            foreach(var t in nodes){if(!t.name.StartsWith("Door:")||(!game.IsTutorial&&t.name=="Door:0"))continue;
                foreach(var r in t.GetComponentsInChildren<MeshRenderer>(true)){bool hands=false;for(var q=r.transform;q&&q!=t;q=q.parent)if(q.name=="DoorHands")hands=true;if(!hands)r.enabled=false;}
                Box(t,"Art Door frame",new Vector3(.47f,1.08f,0),new Vector3(.92f,2.16f,.105f),"walnut",.015f);
                foreach(float side in new[]{-1f,1f}){Box(t,"Art Door face",new Vector3(.47f,1.08f,side*.058f),new Vector3(.82f,2.05f,.016f),"wood",.007f);foreach(float y in new[]{.54f,1.53f}){Box(t,"Art Door recessed shadow",new Vector3(.47f,y,side*.069f),new Vector3(.65f,.78f,.015f),"walnut",.012f);Box(t,"Art Door inset",new Vector3(.47f,y,side*.08f),new Vector3(.59f,.71f,.012f),"wood",.01f);}Box(t,"Art Door lock plate",new Vector3(.8f,1.02f,side*.085f),new Vector3(.066f,.20f,.02f),"brass",.015f);Box(t,"Art Door lever",new Vector3(.75f,1.06f,side*.115f),new Vector3(.17f,.034f,.035f),"brass",.014f);}
                Finish(t);
            }
            NightHomeWalls.Build(house, game);
        }
    }
}
