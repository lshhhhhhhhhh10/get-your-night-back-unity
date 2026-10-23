using UnityEngine;
namespace NightBackPort
{
    [RequireComponent(typeof(Camera))]
    public sealed class NightBedroomLens:MonoBehaviour
    {
        Material material;
        void OnEnable(){GetComponent<Camera>().depthTextureMode|=DepthTextureMode.Depth;}
        void OnRenderImage(RenderTexture source,RenderTexture destination)
        {
            var a=NightApp.Instance;float strength=0;
            if(a&&a.world&&a.world.bedroomTitle){
                if(a.page=="menu"||a.page=="credits"||a.page=="exit"||(a.page=="settings"&&a.game.status=="ready"))strength=1;
                else if(a.page=="intro")strength=1-Mathf.SmoothStep(0,1,a.world.bedroomTitle.IntroProgress*2);
            }
            if(strength<=0){Graphics.Blit(source,destination);return;}
            if(!material){var shader=Resources.Load<Shader>("NightBack/BedroomLens");if(!shader){Graphics.Blit(source,destination);return;}material=new Material(shader);}
            material.SetFloat("_Strength",strength);Graphics.Blit(source,destination,material);
        }
        void OnDestroy(){if(material)Destroy(material);}
    }
}
