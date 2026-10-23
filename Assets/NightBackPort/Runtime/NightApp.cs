// @role Coordinates input, simulation, views and saves
// @calls NightController NightGame_Core NightGame_Interactions NightGame_Tutorial NightWorld NightUI NightAudio NightText

using System;
using System.IO;
using UnityEngine;

namespace NightBackPort
{
    // One native scene owns input, simulation, rendering, audio and a separate local save.
    public sealed class NightApp : MonoBehaviour
    {
        public NightGame game; public NightWorld world; public Camera viewCamera;
        public NightSettings settings=new NightSettings(); public NightController controller=new NightController();
        public bool paused,overview=true,thirdPerson,cursorFree; public string page="menu";
        public int selectedNight,departureChoice,unlocked=1; public float yaw=180,pitch=4;
        public NightUI ui; public NightAudio audioSystem;
        public bool verificationMode; public int processedSounds;
        float force,autoSave; bool focus=true,releaseGate=true,hadPad; string lastStatus,cameraContext;
        public static NightApp Instance {get;private set;}
        public string DataDirectory
        {
            get{
#if UNITY_EDITOR
                return Path.Combine(Directory.GetParent(Application.dataPath).FullName,"NightBackEvidence","PlayerData");
#else
                return Path.GetFullPath(Path.Combine(Application.dataPath,"../..","NightBackPlayerData"));
#endif
            }
        }
        public string SavePath {get{return Path.Combine(DataDirectory,"save.json");}}
        public string TutorialSavePath {get{return Path.Combine(DataDirectory,"tutorial-save.json");}}
        public bool HasSave {get{NightSave saved;return TryReadSave(SavePath,false,out saved);}}
        public bool HasTutorialSave {get{NightSave saved;return TryReadSave(TutorialSavePath,true,out saved)&&saved.game.status=="playing";}}
        void Awake()
        {
            Instance=this;Application.targetFrameRate=60;
#if !UNITY_EDITOR
            Screen.SetResolution(1440,900,FullScreenMode.Windowed);
#endif
            LoadSettings();NightText.english=settings.english;
            if(!world)world=gameObject.AddComponent<NightWorld>();
            if(!viewCamera){var c=new GameObject("Night Camera");viewCamera=c.AddComponent<Camera>();c.AddComponent<AudioListener>();}
            var legacyLens=viewCamera.GetComponent<NightBedroomLens>();if(legacyLens)legacyLens.enabled=false;
            viewCamera.nearClipPlane=.035f;viewCamera.farClipPlane=85;viewCamera.fieldOfView=72;
            viewCamera.backgroundColor=new Color(.028f,.042f,.073f);viewCamera.clearFlags=CameraClearFlags.SolidColor;
            ui=gameObject.AddComponent<NightUI>();ui.app=this;
            audioSystem=gameObject.AddComponent<NightAudio>();audioSystem.app=this;
            Preview(0);ui.Show("menu");SetCursor();
        }
        void LoadSettings()
        {
            try{var p=Path.Combine(DataDirectory,"settings.json");if(File.Exists(p))settings=JsonUtility.FromJson<NightSettings>(File.ReadAllText(p));
                var progress=Path.Combine(DataDirectory,"progress.json");if(File.Exists(progress))unlocked=Mathf.Clamp(JsonUtility.FromJson<NightProgress>(File.ReadAllText(progress)).unlocked,1,3);
                if(HasSave){var save=JsonUtility.FromJson<NightSave>(File.ReadAllText(SavePath));unlocked=Mathf.Max(unlocked,Mathf.Clamp(save.unlocked,1,3));}}
            catch(Exception e){Debug.LogWarning("Night Back settings: "+e.Message);}
            if(settings==null)settings=new NightSettings();
        }
        public void SaveSettings()
        {
            if(verificationMode)return;Directory.CreateDirectory(DataDirectory);File.WriteAllText(Path.Combine(DataDirectory,"progress.json"),JsonUtility.ToJson(new NightProgress{unlocked=unlocked}));File.WriteAllText(Path.Combine(DataDirectory,"settings.json"),JsonUtility.ToJson(settings,true));
            // Normal Tick/confirm reaches the existing result branch, which calls this method.
            if(game!=null&&game.IsTutorial&&game.status=="won")SaveGame();
        }
        public void Preview(int night)
        {
            selectedNight=Mathf.Clamp(night,0,3);game=new NightGame(selectedNight,departureChoice*90);world.Build(game);world.SetSkins(settings.playerSkin,settings.parentSkin);
            overview=false;world.SetOverview(false);yaw=180;pitch=4;lastStatus=game.status;UpdateCamera(true);
        }
        public void StartNight(int night)
        {
            controller.Stop();if(night<3)page="menu";Preview(night);if(night<3){BeginBedroomIntro();return;}game.Start();page="playing";paused=false;overview=night!=3;world.SetOverview(overview);thirdPerson=false;cursorFree=false;releaseGate=true;autoSave=0;
            ui.Show("playing");SaveGame();SetCursor();
        }
        public void BeginBedroomIntro()
        {
            if(page=="intro"||world.bedroomTitle==null)return;
            ReleaseInputs();paused=false;overview=false;thirdPerson=false;cursorFree=false;world.SetOverview(false);world.SetShowcase(false);world.ClearCloseup();
            page="intro";ui.Show("intro");SetCursor();world.bedroomTitle.BeginIntro(viewCamera);
        }
        void TickBedroomIntro(float dt)
        {
            if(!focus)return;
            if(Input.GetKeyDown(KeyCode.Escape)||controller.Down(7)){Menu();return;}
            if(!world.bedroomTitle.StepIntro(dt,viewCamera))return;
            game.player=world.bedroomTitle.PlayStartPosition;game.Start();
            var story=world.bedroomTitle.StoryWall;if(story)story.StartExploration();
            yaw=story?46:180;pitch=story?-4:4;game.heading=yaw*Mathf.Deg2Rad;page="playing";paused=false;overview=false;thirdPerson=false;cursorFree=false;releaseGate=true;autoSave=0;lastStatus=game.status;
            world.Sync(game);ui.Show("playing");SaveGame();SetCursor();UpdateCamera(true);
        }
        public void Continue()
        { ContinueFrom(SavePath,false); }
        public void ContinueTutorial()
        {
            // The web practice entry resumes an unfinished lesson, otherwise starts it again.
            if(!HasTutorialSave){StartNight(3);return;}ContinueFrom(TutorialSavePath,true);
        }
        public bool CompletePractice()
        {
            if(game==null||!game.IsTutorial||!game.FinishPractice())return false;
            audioSystem.Consume(game);page="result";paused=false;lastStatus=game.status;ReleaseInputs();SaveGame();ui.Show("result");SetCursor();UpdateCamera(false);return true;
        }
        static bool TryReadSave(string path,bool tutorial,out NightSave saved)
        {
            saved=null;
            try{
                if(!File.Exists(path))return false;var value=JsonUtility.FromJson<NightSave>(File.ReadAllText(path));
                if(value==null||value.version!=1||value.game==null)return false;
                var g=value.game;if(tutorial?g.levelIndex!=3:g.levelIndex<0||g.levelIndex>2)return false;
                if(g.status!="playing"&&g.status!="won"&&g.status!="lost")return false;
                saved=value;return true;
            }catch{return false;}
        }
        void ContinueFrom(string path,bool tutorial)
        {
            try{
                NightSave save;if(!TryReadSave(path,tutorial,out save))throw new Exception("Unsupported save or wrong save slot");
                if(save.game.status!="playing"){StartNight(tutorial?3:save.game.status=="won"?Mathf.Min(2,save.game.levelIndex+1):save.game.levelIndex);return;}
                var restored=new NightGame(save.game.levelIndex);JsonUtility.FromJsonOverwrite(JsonUtility.ToJson(save.game),restored);
                if(!restored.IsTutorial){restored.level=NightLevel.Load(restored.levelIndex);if(!restored.CanOccupy(restored.player)&&restored.player.x<7&&restored.player.z>10)restored.player=restored.level.home;}
                if(!restored.RepairAfterLoad())throw new Exception("Invalid game state");restored.ReleaseInteractionInputs();restored.active=true;restored.status="playing";restored.events.Clear();game=restored;
                selectedNight=game.levelIndex;if(!tutorial)unlocked=Mathf.Max(unlocked,Mathf.Clamp(save.unlocked,1,3));
                departureChoice=Mathf.Clamp(Mathf.RoundToInt(game.clock.departureSeconds/90),0,2);lastStatus=game.status;autoSave=0;
                world.Build(game);world.SetSkins(settings.playerSkin,settings.parentSkin);overview=false;world.SetOverview(false);
                yaw=game.heading*Mathf.Rad2Deg;page="playing";paused=false;releaseGate=true;ui.Show("playing");SetCursor();
            }catch(Exception e){Debug.LogWarning("Night Back save could not be loaded: "+e.Message);ui.ShowError(NightText.Pick("存档无法读取，可以开始新的一夜。","This save could not be loaded. Start a new night."));}
        }
        public void SaveGame()
        {
            if(game==null||(game.status!="playing"&&game.status!="won"&&game.status!="lost")||verificationMode)return;
            Directory.CreateDirectory(DataDirectory);
            var save=new NightSave{game=game,settings=settings,unlocked=unlocked,savedAt=DateTime.Now.ToString("O")};
            // Each slot has its own atomic replacement and backup; practice never writes save.json.
            string path=game.IsTutorial?TutorialSavePath:SavePath,tmp=path+".tmp";File.WriteAllText(tmp,JsonUtility.ToJson(save,true));
            if(File.Exists(path)){File.Copy(path,path+".backup",true);File.Delete(path);}File.Move(tmp,path);
        }
        public void Pause()
        {
            if(page!="playing")return;paused=true;ReleaseInputs();SaveGame();page="pause";ui.Show("pause");SetCursor();
        }
        public void Resume(){paused=false;page="playing";releaseGate=true;ui.Show("playing");SetCursor();}
        public void Menu(){SaveGame();paused=true;ReleaseInputs();page="menu";Preview(Mathf.Min(selectedNight,2));ui.Show("menu");SetCursor();}
        public void ShowPage(string name){ReleaseInputs();page=name;ui.Show(name);UpdateCamera(true);SetCursor();}
        public void ChangeLanguage(bool english){settings.english=english;NightText.english=english;if(world.bedroomTitle&&world.bedroomTitle.StoryWall)world.bedroomTitle.StoryWall.RefreshLanguage(english);SaveSettings();ui.Show(page);}
        public void ReleaseInputs(){force=0;controller.Stop();if(game!=null)game.ReleaseInteractionInputs();releaseGate=true;}
        void OnApplicationFocus(bool value){focus=value;if(!value&&page=="playing")Pause();else if(!value)ReleaseInputs();}
        void OnApplicationPause(bool value){if(value&&page=="playing")Pause();}
        void OnDisable(){ReleaseInputs();SaveGame();Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
        void OnApplicationQuit(){ReleaseInputs();SaveGame();SaveSettings();}
        void Update()
        {
            if(game==null)return;controller.deadzone=settings.stickDeadzone;controller.Poll();
            if(hadPad&&!controller.connected&&page=="playing")Pause();hadPad=controller.connected;
            if(page=="intro"){TickBedroomIntro(Mathf.Min(Time.unscaledDeltaTime,.05f));return;}
            if(page=="menu"&&world.bedroomTitle){if(!focus)return;UpdateCamera(false);world.bedroomTitle.HandleMenuInput();return;}
            if(page!="playing"){
                if(!focus)return;
                var step=controller.MenuStep(Time.unscaledTime);
                if(Input.GetKeyDown(KeyCode.Tab))step.y=Input.GetKey(KeyCode.LeftShift)||Input.GetKey(KeyCode.RightShift)?1:-1;
                if(Input.GetKeyDown(KeyCode.DownArrow))step.y=-1;if(Input.GetKeyDown(KeyCode.UpArrow))step.y=1;
                if(Input.GetKeyDown(KeyCode.LeftArrow))step.x=-1;if(Input.GetKeyDown(KeyCode.RightArrow))step.x=1;
                HandleMenuNavigation(step,Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.KeypadEnter)||Input.GetKeyDown(KeyCode.Space)||controller.Down(6),Input.GetKeyDown(KeyCode.Escape)||controller.Down(7));
                if(page=="wardrobe")world.ShowcaseOrbit(new Vector2(controller.Look.x,-controller.Look.y)*180*Time.unscaledDeltaTime);
                else if(page!="playing")ui.ScrollMenu(-controller.Look.y);
                UpdateCamera(false);return;
            }
            if(!focus||paused||verificationMode)return;
            if(Input.GetKeyDown(KeyCode.Tab)){cursorFree=!cursorFree;ReleaseInputs();SetCursor();}
            bool cancel=Input.GetKeyDown(KeyCode.Escape)||controller.Down(7);
            if(Input.GetKeyDown(KeyCode.P)||controller.Down(12)){Pause();return;}
            if(cancel&&game.mode==null){Pause();return;}
            if(Input.GetKeyDown(KeyCode.M)||controller.Down(9)){overview=!overview;world.SetOverview(overview);SetCursor();}
            if(Input.GetKeyDown(KeyCode.V)||controller.Down(15)){thirdPerson=!thirdPerson;overview=false;world.SetOverview(false);SetCursor();}
            Vector2 raw=new Vector2(((Input.GetKey(KeyCode.D)||Input.GetKey(KeyCode.RightArrow))?1:0)-((Input.GetKey(KeyCode.A)||Input.GetKey(KeyCode.LeftArrow))?1:0),((Input.GetKey(KeyCode.W)||Input.GetKey(KeyCode.UpArrow))?1:0)-((Input.GetKey(KeyCode.S)||Input.GetKey(KeyCode.DownArrow))?1:0));
            if(controller.connected)raw+=controller.Move;else if(Input.GetJoystickNames().Length>0)raw+=new Vector2(Input.GetAxisRaw("Horizontal"),Input.GetAxisRaw("Vertical"));
            raw=Vector2.ClampMagnitude(raw,1);
            bool mouseUI=ui.PointerOverControls();
            bool keyboardHeld=Input.GetKey(KeyCode.E)||(!mouseUI&&Input.GetMouseButton(0));
            bool keyboardLeft=(!mouseUI&&Input.GetMouseButton(1))||Input.GetKey(KeyCode.LeftShift)||(game.mode!=null&&Input.GetKey(KeyCode.Q))||(game.mode!=null&&game.mode.type=="lockpick"&&(Input.GetKey(KeyCode.R)||controller.Held(10)));
            bool graspHeld=controller.Held(8);
            float rightPressure=controller.connected?Mathf.Clamp01(controller.values[5]):0,leftPressure=controller.connected?Mathf.Clamp01(controller.values[4]):0;
            if(rightPressure<=.035f)rightPressure=0;if(leftPressure<=.035f)leftPressure=0;
            if(releaseGate){
                if(!keyboardHeld&&!keyboardLeft&&!graspHeld&&rightPressure<.035f&&leftPressure<.035f)releaseGate=false;
                else{keyboardHeld=false;keyboardLeft=false;graspHeld=false;rightPressure=leftPressure=0;}
            }
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);
            if(game.mode==null){
                if(raw.sqrMagnitude>.01f&&overview){overview=false;world.SetOverview(false);SetCursor();}
                if(!overview&&!cursorFree){yaw+=(Input.GetAxisRaw("Mouse X")*2.2f*settings.sensitivity+controller.Look.x*140*dt*settings.gamepadSensitivity);pitch=Mathf.Clamp(pitch-Input.GetAxisRaw("Mouse Y")*1.7f*settings.sensitivity-controller.Look.y*95*dt*settings.gamepadSensitivity,-65,70);}
            }
            // E/鼠标沿各机制的源键盘曲线；只有 R2 传绝对模拟压力，□只负责抓握/静音。
            force=rightPressure;
            Vector2 worldMove=raw;
            if(game.mode==null){float a=yaw*Mathf.Deg2Rad;worldMove=new Vector2(Mathf.Sin(a)*raw.y+Mathf.Cos(a)*raw.x,Mathf.Cos(a)*raw.y-Mathf.Sin(a)*raw.x);game.heading=a;}
            var input=new NightInput{
                analog=controller.connected&&(leftPressure>.035f||rightPressure>.035f||controller.Look.sqrMagnitude>.01f),move=InteractionMove(worldMove),look=controller.Look+new Vector2(Input.GetAxisRaw("Mouse X"),Input.GetAxisRaw("Mouse Y")),
                actionPressed=!releaseGate&&(Input.GetKeyDown(KeyCode.E)||(!mouseUI&&Input.GetMouseButtonDown(0))||controller.Down(8)),actionHeld=keyboardHeld,
                confirm=Input.GetKeyDown(KeyCode.Space)||Input.GetKeyDown(KeyCode.Return)||controller.Down(6),reverse=Input.GetKeyDown(KeyCode.R)||controller.Down(10),cancel=cancel,
                crouchPressed=Input.GetKeyDown(KeyCode.C)||controller.Down(11),petPressed=Input.GetKeyDown(KeyCode.F),toyPressed=Input.GetKeyDown(KeyCode.Q)||controller.Down(13),
                force=releaseGate?0:force,rightForce=releaseGate?0:rightPressure,leftForce=releaseGate?0:(leftPressure>.035f?leftPressure:keyboardLeft?(game.mode!=null&&game.mode.type=="lockpick"?1:.55f):0),muteHeld=keyboardHeld||graspHeld};
            game.Tick(dt,input);world.Sync(game);audioSystem.Consume(game);
            if(settings.adaptive&&settings.haptics&&game.mode!=null&&game.mode.type=="door")controller.Resistance((.12f+Mathf.Abs(Mathf.Sin(game.mode.progress*13))*.45f)*settings.triggerStrength);else controller.Resistance(0);
            if(game.status!=lastStatus){lastStatus=game.status;if(game.status=="won"||game.status=="lost"||game.status=="win"||game.status=="lose"){
                bool won=game.status=="won"||game.status=="win";if(won&&game.levelIndex<3)unlocked=Mathf.Max(unlocked,Mathf.Min(3,game.levelIndex+2));
                page="result";ReleaseInputs();SaveSettings();ui.Show("result");SetCursor();
            }}
            autoSave+=dt;if(autoSave>12&&game.mode==null){autoSave=0;SaveGame();}
            ui.Refresh();UpdateCamera(false);
            if(world.bedroomTitle&&world.bedroomTitle.StoryWall)world.bedroomTitle.StoryWall.Tick(dt,viewCamera);
        }
        public Vector2 InteractionMove(Vector2 raw)
        {
            if(game.mode==null||game.mode.type!="catch")return raw;
            var frame=world.GetIncidentFrame(game.mode.kind);
            if(frame&&Vector3.Dot(viewCamera.transform.right,frame.right)<0)raw.x=-raw.x;
            return raw;
        }
        public void HandleMenuNavigation(Vector2 step,bool confirm,bool cancel)
        {
            if(page=="playing"||page=="intro"||page=="menu")return;
            if(cancel){ui.GoBack();return;}
            if(step.y!=0)ui.MoveFocus(step.y>0?-1:1);else if(step.x!=0)ui.AdjustFocused(step.x>0?1:-1);
            if(confirm)ui.ActivateFocused();
        }
        public void SetCursor(){bool lockIt=page=="playing"&&!overview&&!cursorFree;Cursor.lockState=lockIt?CursorLockMode.Locked:CursorLockMode.None;Cursor.visible=!lockIt;}
        public void UpdateCamera(bool instant)
        {
            if(!viewCamera||game==null)return;
            string nextContext="world";
            if(game.mode!=null){var mode=game.mode;if(mode.type=="lockpick")nextContext="lock";else if(mode.type=="catch"||mode.type=="reaction")nextContext="incident:"+mode.kind;else if(mode.type=="search")nextContext="search:"+mode.id;}
            if(nextContext!=cameraContext){instant=true;cameraContext=nextContext;}
            world.SetOverview(overview||page=="result");
            Vector3 pos,look;
            bool showcase=page=="wardrobe";world.SetShowcase(showcase,page=="wardrobe");world.ClearCloseup();viewCamera.cullingMask=-1;
            if(world.bedroomTitle&&(page=="menu"||page=="credits"||page=="exit"||(page=="settings"&&game.status=="ready"))){
                world.SetOverview(false);world.bedroomTitle.SetMenuCamera(viewCamera);if(world.playerVisual)world.playerVisual.gameObject.SetActive(false);viewCamera.cullingMask&=~NightControlDemonstration.PreviewMask;return;
            }
            if(showcase){viewCamera.cullingMask=NightWorld.DetailMask;viewCamera.orthographic=false;viewCamera.fieldOfView=42;pos=world.ShowcaseCameraPosition+new Vector3(-2.3f,.15f,0);look=world.ShowcaseCenter+new Vector3(-2.3f,.15f,0);}
            else if(overview||page=="result"||page=="menu"||page=="settings"||page=="wardrobe"){
                Vector3 center=new Vector3((game.level.width-1)*.5f,0,(game.level.depth-1)*.5f);pos=center+new Vector3(2,21,16);look=center;
                viewCamera.orthographic=true;viewCamera.orthographicSize=Mathf.Max(8,game.level.width*.57f);
            }else{
                viewCamera.orthographic=false;float height=game.crouched?.83f:1.37f;
                if(settings.headBob&&game.mode==null&&game.velocity.sqrMagnitude>.01f)height+=Mathf.Sin(game.realTime*10)*.013f;
                var eye=game.player+Vector3.up*height;var q=Quaternion.Euler(pitch,yaw,0);
                pos=eye;look=eye+q*Vector3.forward;
                if(thirdPerson){var desired=eye-q*Vector3.forward*2.5f+Vector3.up*.6f;RaycastHit hit;var delta=desired-eye;if(Physics.SphereCast(eye,.12f,delta.normalized,out hit,delta.magnitude))pos=hit.point+hit.normal*.15f;else pos=desired;look=eye+q*Vector3.forward*2;}
                viewCamera.fieldOfView=72;
                if(game.mode!=null){
                    var m=game.mode;
                    if(m.type=="lockpick"&&world.lockVisual){pos=world.lockVisual.TransformPoint(new Vector3(0,1.25f,7.4f));look=world.lockVisual.TransformPoint(new Vector3(0,.53f,0));viewCamera.fieldOfView=36;}
                    else if(m.type=="catch"||m.type=="reaction"){
                        string kind=m.kind;viewCamera.cullingMask=world.SetCloseup(kind);var frame=world.GetIncidentFrame(kind);if(frame){float yy=kind=="pencils"?1.22f:kind=="vase"?1.28f:.96f;float zz=kind=="vase"?4.4f:kind=="pencils"?2.6f:kind=="tin"?3.4f:2.25f;float ty=kind=="vase"?.42f:kind=="pencils"?.2f:.07f;zz/=Mathf.Min(1,Mathf.Max(.1f,viewCamera.aspect)/.95f);pos=frame.TransformPoint(new Vector3(0,yy,zz));look=frame.TransformPoint(new Vector3(0,ty,.08f));viewCamera.fieldOfView=40;game.rescueAspect=viewCamera.aspect;}
                    }else if(m.type=="search"){
                        viewCamera.cullingMask=world.SetCloseup(m.id);var prop=world.FindFurniture(m.id);if(prop){var target=prop.position+Vector3.up*.85f;var delta=game.player-prop.position;delta.y=0;if(delta.magnitude<.1f)delta=Vector3.forward;pos=target+delta.normalized*1.8f+Vector3.up*1.08f;look=target;viewCamera.fieldOfView=48;}
                    }else if(m.type=="tickle"){
                        var bed=game.level.parentBed;pos=bed+new Vector3(.1f,1.2f,2.2f);look=bed+new Vector3(0,.52f,.75f);viewCamera.fieldOfView=48;
                    }
                }
            }
            viewCamera.cullingMask &= ~NightControlDemonstration.PreviewMask;
            viewCamera.transform.position=instant?pos:Vector3.Lerp(viewCamera.transform.position,pos,1-Mathf.Exp(-Time.unscaledDeltaTime*14));
            var rotation=Quaternion.LookRotation(look-pos);viewCamera.transform.rotation=instant?rotation:Quaternion.Slerp(viewCamera.transform.rotation,rotation,1-Mathf.Exp(-Time.unscaledDeltaTime*18));
            if(world.playerVisual)world.playerVisual.gameObject.SetActive(overview||thirdPerson||page=="result");
        }
    }
}
