using System;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
namespace NightBackPort.Editor
{
    // The evaluated Blender meshes retain their authored UVs, normals and semantic pivots.
    // No modelling package is required on player machines.
    public static class NightBedroomImporter
    {
        const string Art="Assets/NightBackPort/Art/BedroomStudio";
        const string Output="Assets/NightBackPort/Resources/NightBack/BedroomStudio";
        [Serializable] class Model { public Part[] parts; }
        [Serializable] class Part { public string name,texture; public float[] pivot,vertices,normals,uv; public int[] triangles; }
        static Material LoadPropsMaterial(Shader shader)
        {
            const string texPath=Art+"/TitlePropsBaked.png";
            if(!File.Exists(texPath))return null;
            AssetDatabase.ImportAsset(texPath,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.maxTextureSize=2048;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.anisoLevel=4;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            string path=Output+"/TitleProps.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(shader);AssetDatabase.CreateAsset(mat,path);}
            mat.shader=shader;mat.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);mat.color=Color.white;mat.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(mat);return mat;
        }
        [MenuItem("Night Back/Bedroom/Import Blender Bedroom")]
        public static void Import()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before importing the bedroom");
            Directory.CreateDirectory(Output+"/Meshes");AssetDatabase.Refresh();
            string texPath=Art+"/BedroomBaked.png";
            AssetDatabase.ImportAsset(texPath,ImportAssetOptions.ForceUpdate);
            var importer=(TextureImporter)AssetImporter.GetAtPath(texPath);
            importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.maxTextureSize=4096;
            importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.anisoLevel=4;
            importer.filterMode=FilterMode.Bilinear;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
            var shader=Shader.Find("NightBack/Bedroom Baked");if(!shader)throw new InvalidOperationException("Bedroom shader missing");
            string matPath=Output+"/Bedroom.mat";var material=AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if(!material){material=new Material(shader);AssetDatabase.CreateAsset(material,matPath);}
            material.shader=shader;material.mainTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);material.color=Color.white;material.SetColor("_EmissionColor",Color.black);EditorUtility.SetDirty(material);
            var data=JsonUtility.FromJson<Model>(File.ReadAllText(Art+"/BedroomMesh.json"));
            var propsMaterial=LoadPropsMaterial(shader);
            var root=new GameObject("Reference Bedroom · Blender");int triangleCount=0;
            try{
                foreach(var p in data.parts){
                    string path=Output+"/Meshes/"+p.name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);bool fresh=!mesh;
                    if(fresh)mesh=new Mesh();else mesh.Clear();mesh.name=p.name;mesh.indexFormat=IndexFormat.UInt32;
                    var v=new Vector3[p.vertices.Length/3];var n=new Vector3[v.Length];var uv=new Vector2[v.Length];
                    for(int i=0;i<v.Length;i++){v[i]=new Vector3(p.vertices[i*3],p.vertices[i*3+1],p.vertices[i*3+2]);n[i]=new Vector3(p.normals[i*3],p.normals[i*3+1],p.normals[i*3+2]);uv[i]=new Vector2(p.uv[i*2],p.uv[i*2+1]);}
                    mesh.vertices=v;mesh.normals=n;mesh.uv=uv;mesh.triangles=p.triangles;mesh.RecalculateBounds();triangleCount+=p.triangles.Length/3;
                    if(fresh)AssetDatabase.CreateAsset(mesh,path);else EditorUtility.SetDirty(mesh);
                    mesh.UploadMeshData(false);
                    var node=new GameObject(p.name);node.transform.SetParent(root.transform,false);node.transform.localPosition=new Vector3(p.pivot[0],p.pivot[1],p.pivot[2]);
                    node.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=node.AddComponent<MeshRenderer>();renderer.sharedMaterial=p.texture=="TitlePropsBaked"&&propsMaterial?propsMaterial:material;
                    renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                }
                PrefabUtility.SaveAsPrefabAsset(root,Output+"/ReferenceBedroom.prefab");AssetDatabase.SaveAssets();
                Debug.Log("BEDROOM_BLENDER_IMPORTED groups="+data.parts.Length+" triangles="+triangleCount);
            }finally{UnityEngine.Object.DestroyImmediate(root);}
        }
    }
}
