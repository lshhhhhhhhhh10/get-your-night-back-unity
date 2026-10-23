using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
namespace NightBackPort.Editor
{
    public static class NightPortAuthoring
    {
        public const string Root="Assets/NightBackPort";
        public const string ScenePath=Root+"/Scenes/GetYourNightBack.scene";
        [MenuItem("Night Back/Time Edition/Build and Open Game")]
        public static void BuildScene()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before authoring");
            if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Existing scene has unsaved edits; preserve it before opening the separate game scene.");
            Directory.CreateDirectory(Root+"/Scenes");Directory.CreateDirectory("NightBackEvidence");
            ConfigurePlugins();
            var panel=AssetDatabase.LoadAssetAtPath<PanelSettings>(Root+"/NightPanelSettings.asset");
            if(!panel){panel=ScriptableObject.CreateInstance<PanelSettings>();AssetDatabase.CreateAsset(panel,Root+"/NightPanelSettings.asset");}
            panel.scaleMode=PanelScaleMode.ScaleWithScreenSize;panel.referenceResolution=new Vector2Int(1440,900);panel.screenMatchMode=PanelScreenMatchMode.MatchWidthOrHeight;panel.match=.5f;EditorUtility.SetDirty(panel);
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            scene.name="GetYourNightBack";
            var root=new GameObject("Get Your Night Back · Time Edition");root.AddComponent<UIDocument>().panelSettings=panel;var app=root.AddComponent<NightApp>();
            var camera=new GameObject("Night Camera");app.viewCamera=camera.AddComponent<Camera>();camera.AddComponent<AudioListener>();
            app.world=root.AddComponent<NightWorld>();
            // Build a persistent, inspectable map in Edit Mode, then rebuild runtime state in Awake.
            app.world.Build(new NightGame(0));app.world.SetOverview(false);
            PersistRenderAssets(root);
            app.viewCamera.transform.position=new Vector3(11,21,25);app.viewCamera.transform.LookAt(new Vector3(9,0,9));app.viewCamera.orthographic=true;app.viewCamera.orthographicSize=11;if(app.world.bedroomTitle)app.world.bedroomTitle.SetMenuCamera(app.viewCamera);
            EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
            File.WriteAllText("NightBackEvidence/scene-created.json",JsonUtility.ToJson(new BuildReceipt{scene=ScenePath,editor=Application.unityVersion,created=DateTime.Now.ToString("O"),saved=true},true));
            Debug.Log("NIGHT_BACK_SCENE_SAVED "+ScenePath);
        }
        static string SafeName(string name,string fallback)
        {if(string.IsNullOrWhiteSpace(name))return fallback;foreach(char c in Path.GetInvalidFileNameChars())name=name.Replace(c,'_');return name.Trim('.',' ');}
        static void PersistRenderAssets(GameObject root)
        {
            string folder=Root+"/Baked";Directory.CreateDirectory(folder);AssetDatabase.Refresh();
            foreach(var meshFilter in root.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh=meshFilter.sharedMesh;if(!mesh||AssetDatabase.Contains(mesh))continue;
                var name=SafeName(mesh.name,"Mesh");string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+name+".asset");AssetDatabase.CreateAsset(mesh,path);
            }
            foreach(var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mesh=renderer.sharedMesh;if(!mesh||AssetDatabase.Contains(mesh))continue;
                string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+SafeName(mesh.name,"Mesh")+".asset");AssetDatabase.CreateAsset(mesh,path);
            }
            foreach(var renderer in root.GetComponentsInChildren<Renderer>(true))foreach(var material in renderer.sharedMaterials)
            {
                if(!material||AssetDatabase.Contains(material))continue;
                foreach(string property in material.GetTexturePropertyNames()){var texture=material.GetTexture(property) as Texture2D;if(texture&&!AssetDatabase.Contains(texture)){string texPath=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+SafeName(texture.name,"Texture")+".asset");AssetDatabase.CreateAsset(texture,texPath);}}
                string path=AssetDatabase.GenerateUniqueAssetPath(folder+"/"+SafeName(material.name,"Material")+".mat");AssetDatabase.CreateAsset(material,path);
            }
        }
        [Serializable] class BuildReceipt{public string scene,editor,created;public bool saved;}
        [MenuItem("Night Back/Time Edition/Open Game")]
        public static void OpenGame(){if(EditorSceneManager.GetActiveScene().isDirty)throw new InvalidOperationException("Current scene has unsaved changes");EditorSceneManager.OpenScene(ScenePath);}
        static void ConfigurePlugins()
        {
            var plugin=AssetImporter.GetAtPath(Root+"/Plugins/macOS/libNightBackController.dylib") as PluginImporter;
            if(plugin){plugin.SetCompatibleWithAnyPlatform(false);plugin.SetCompatibleWithEditor(true);plugin.SetEditorData("OS","OSX");plugin.SetEditorData("CPU","AnyCPU");plugin.SetCompatibleWithPlatform(BuildTarget.StandaloneOSX,true);plugin.SetPlatformData(BuildTarget.StandaloneOSX,"CPU","AnyCPU");plugin.SaveAndReimport();}
            var source=AssetImporter.GetAtPath(Root+"/Plugins/macOS/NightBackController.mm") as PluginImporter;
            if(source){source.SetCompatibleWithAnyPlatform(false);source.SetCompatibleWithEditor(false);source.SaveAndReimport();}
        }
        [MenuItem("Night Back/Time Edition/Build Mac Player")]
        public static void BuildPlayer()
        {
            Directory.CreateDirectory("NightBackEvidence/Build");
            var options=new BuildPlayerOptions{scenes=new[]{ScenePath},locationPathName="NightBackEvidence/Build/Get Your Night Back.app",target=BuildTarget.StandaloneOSX,options=BuildOptions.None};
            var report=BuildPipeline.BuildPlayer(options);
            File.WriteAllText("NightBackEvidence/build-messages.json",JsonUtility.ToJson(new BuildMessages{messages=report.steps.SelectMany(step=>step.messages).Where(message=>message.type==LogType.Warning||message.type==LogType.Error||message.type==LogType.Exception).Select(message=>message.type+": "+message.content).ToArray()},true));
            File.WriteAllText("NightBackEvidence/build-result.json", "{\"result\":\""+report.summary.result+"\",\"errors\":"+report.summary.totalErrors+",\"warnings\":"+report.summary.totalWarnings+",\"bytes\":"+report.summary.totalSize+"}");
            if(report.summary.result!=UnityEditor.Build.Reporting.BuildResult.Succeeded)throw new Exception("Player build failed: "+report.summary.result);
        }
        [Serializable] class BuildMessages { public string[] messages; }
    }
}
