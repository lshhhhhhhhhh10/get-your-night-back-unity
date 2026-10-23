// @role Draws raised panels and rating stars

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightBackPort
{
    public abstract class NightFacetedVisual : VisualElement
    {
        protected MeshGenerationContext meshContext;

        protected static Color ColorOf(string hex)
        {
            ColorUtility.TryParseHtmlString(hex,out var color);return color;
        }

        protected void Polygon(Vector2[] points,Color[] colors)
        {
            var mesh=meshContext.Allocate(points.Length,(points.Length-2)*3);
            for(int i=0;i<points.Length;i++)mesh.SetNextVertex(new Vertex{position=new Vector3(points[i].x,points[i].y,Vertex.nearZ),tint=colors[i%colors.Length]});
            for(ushort i=1;i<points.Length-1;i++){mesh.SetNextIndex(0);mesh.SetNextIndex(i);mesh.SetNextIndex((ushort)(i+1));}
        }

        protected void Face(Color color,params Vector2[] points){Polygon(points,new[]{color});}
    }

    public sealed class NightBoardSurface : NightFacetedVisual
    {
        public enum Kind{Board,Plate,Gold,Slate}
        readonly Kind kind;
        public bool Highlighted;

        public NightBoardSurface(Kind kind)
        {
            this.kind=kind;pickingMode=PickingMode.Ignore;AddToClassList("board-surface");
            generateVisualContent+=Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            meshContext=context;float w=contentRect.width,h=contentRect.height;
            if(w<10||h<10)return;
            if(kind==Kind.Board){DrawBoard(w,h);return;}
            bool gold=kind==Kind.Gold,plate=kind==Kind.Plate;
            float d=plate?6:9;
            var a=new Vector2(4,1);var b=new Vector2(w-5,3);var c=new Vector2(w-2,h-d);var e=new Vector2(1,h-d-3);
            Face(new Color(0,0,0,.23f),new Vector2(2,7),new Vector2(w-2,8),new Vector2(w,h),new Vector2(0,h-2));
            Face(ColorOf(gold?"#885823":plate?"#343339":"#202733"),e,c,new Vector2(w-6,h),new Vector2(0,h-3));
            Polygon(new[]{a,b,c,e},new[]{ColorOf(gold?"#F6BE51":plate?"#6B6464":"#485165"),ColorOf(gold?"#EBA638":plate?"#625C60":"#3D4557"),ColorOf(gold?"#CB872E":plate?"#504D53":"#303747"),ColorOf(gold?"#D59332":plate?"#565158":"#353D50")});
            Face(ColorOf(gold?"#FFE095":plate?"#8F8180":"#677084"),a,b,new Vector2(b.x,b.y+2),new Vector2(a.x+1,a.y+2));
            Face(ColorOf(gold?"#FFC85D":plate?"#756B6D":"#50596B"),b,c,new Vector2(c.x-2,c.y-1),new Vector2(b.x-2,b.y+2));
            if(Highlighted)Face(new Color(1,.87f,.58f,.14f),a,b,c,e);
        }

        void DrawBoard(float w,float h)
        {
            Face(new Color(0,0,0,.28f),new Vector2(32,23),new Vector2(w-5,18),new Vector2(w+8,h-1),new Vector2(8,h+8));
            Face(ColorOf("#35343E"),new Vector2(16,30),new Vector2(w-29,19),new Vector2(w-2,h-11),new Vector2(0,h-28));
            Face(ColorOf("#51463F"),new Vector2(29,18),new Vector2(w-24,1),new Vector2(w-7,h-20),new Vector2(6,h-14));
            var a=new Vector2(31,15);var b=new Vector2(w-24,1);var c=new Vector2(w-7,h-20);var d=new Vector2(13,h-29);
            Polygon(new[]{a,b,c,d},new[]{ColorOf("#837268"),ColorOf("#91785F"),ColorOf("#61544B"),ColorOf("#595458")});
            Polygon(new[]{b,new Vector2(w-17,17),new Vector2(w,h-13),c},new[]{ColorOf("#F5C779"),ColorOf("#CE9851"),ColorOf("#87603D"),ColorOf("#AF8050")});
            Face(ColorOf("#39373C"),d,c,new Vector2(w,h-13),new Vector2(8,h-18));
            Face(ColorOf("#BDA083"),a,b,new Vector2(b.x,b.y+1.5f),new Vector2(a.x,a.y+1.5f));
            Face(new Color(1,.85f,.64f,.04f),a,new Vector2(w*.62f,9),new Vector2(w*.51f,h-23),d);
        }
    }

    public sealed class NightRatingStars : NightFacetedVisual
    {
        public readonly int Score;
        public NightRatingStars(int score)
        {
            Score=Mathf.Clamp(score,0,100);pickingMode=PickingMode.Ignore;generateVisualContent+=Draw;
        }

        void Draw(MeshGenerationContext context)
        {
            meshContext=context;float width=contentRect.width,height=contentRect.height;
            if(width<1||height<1)return;
            float step=width/5,radius=Mathf.Min(step*.37f,(height-9)*.5f);
            for(int s=0;s<5;s++)
            {
                var center=new Vector2(step*(s+.5f),height*.47f);
                var points=new Vector2[10];var shadow=new Vector2[10];
                for(int i=0;i<10;i++){float angle=(-90+i*36)*Mathf.Deg2Rad,r=i%2==0?radius:radius*.47f;points[i]=center+new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*r;shadow[i]=points[i]+new Vector2(-2,5);}
                float fill=Mathf.Clamp01(Score/20f-s),limit=center.x-radius+2*radius*fill;
                for(int i=0;i<10;i++)
                {
                    int next=(i+1)%10;
                    Face(ColorOf("#302F35"),center+new Vector2(-2,5),shadow[i],shadow[next]);
                }
                for(int i=0;i<10;i++)
                {
                    int next=(i+1)%10;bool light=i<3||i>=8;
                    var triangle=new[]{center,points[i],points[next]};
                    Face(ColorOf(light?"#625D60":i<6?"#45444D":"#555158"),triangle);
                    if(fill<=0)continue;
                    var clipped=ClipLeft(triangle,limit);
                    if(clipped.Length>=3)Face(ColorOf(light?"#FFC75C":i<6?"#B77A2C":"#E7A338"),clipped);
                }
            }
        }

        static Vector2[] ClipLeft(Vector2[] polygon,float limit)
        {
            var result=new List<Vector2>(4);
            for(int i=0;i<polygon.Length;i++)
            {
                var a=polygon[i];var b=polygon[(i+1)%polygon.Length];bool inside=a.x<=limit,nextInside=b.x<=limit;
                if(inside)result.Add(a);
                if(inside!=nextInside)result.Add(Vector2.Lerp(a,b,(limit-a.x)/(b.x-a.x)));
            }
            return result.ToArray();
        }
    }
}
