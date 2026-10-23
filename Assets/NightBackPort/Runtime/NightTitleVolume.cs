// @role Creates the solid title geometry

using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // A single closed, bevelled mesh: menu and intro share the same world-space object.
    // It deliberately has no collider, so the camera and title hotspots remain unobstructed.
    public sealed class NightTitleVolume : MonoBehaviour
    {
        const float Distance=1.18f;
        Mesh mesh;
        Material material;
        bool passed;
        public bool Passed {get{return passed;}}
        public float CameraDepth {get;private set;}
        public float BackDepth {get{return mesh?mesh.bounds.max.z*transform.localScale.z:0;}}
        public void Build()
        {
            mesh=Resources.Load<Mesh>("NightBack/TitleVolume/Lettering");
            if(!mesh)throw new System.InvalidOperationException("The extruded title mesh is missing.");
            gameObject.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=gameObject.AddComponent<MeshRenderer>();
            material=new Material(Resources.Load<Shader>("NightBack/TitleVolume/TitleVolume"));
            renderer.sharedMaterial=material;renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            gameObject.SetActive(false);
        }
        public void FitMenu(Camera camera)
        {
            // Match the former 112-unit UI title under the existing 1440 x 900, 0.5-match panel.
            float height=2*Distance*Mathf.Tan(camera.fieldOfView*Mathf.Deg2Rad*.5f);
            float panelHeight=Mathf.Sqrt(1440*900/camera.aspect);
            float em=height*112/panelHeight;
            transform.SetPositionAndRotation(camera.ViewportToWorldPoint(new Vector3(.057f,.74f,Distance)),camera.transform.rotation);
            transform.localScale=Vector3.one*em;
            CameraDepth=-Distance;
        }
        public void SetPage(string page)
        {
            if(page=="menu")passed=false;
            gameObject.SetActive(page=="menu"||(page=="intro"&&!passed));
        }
        public void BeginIntro(){passed=false;gameObject.SetActive(true);}
        public void StepIntro(Camera camera)
        {
            if(passed)return;
            CameraDepth=Vector3.Dot(camera.transform.position-transform.position,transform.forward);
            // Hide only after the camera has cleared the back face. The shader softens near-plane cuts.
            if(CameraDepth>BackDepth+camera.nearClipPlane){passed=true;gameObject.SetActive(false);}
        }
        void OnDestroy(){if(material){if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}}
    }
}
