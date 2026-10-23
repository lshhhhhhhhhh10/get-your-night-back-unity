// @role Handles bedroom menu objects and getting out of bed
// @calls NightStoryWall NightTitleVolume NightClock
// @tune IntroDuration:float:4.8-4.8:Baseline duration of getting out of bed; not changed.

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Blender-authored geometry lives in the actual bedroom, sharing its playable door.
    public sealed class NightBedroomTitle : MonoBehaviour
    {
        public static readonly Vector3 DeskPosition=new Vector3(1.16f,0,11.10f);
        public static readonly Vector3 RightSideLanding=new Vector3(2.15f,0,11.25f);
        public const float DeskYaw=0;
        public Vector3 CameraPosition { get { return new Vector3(.95f,1.30f,12.45f); } }
        public Vector3 CameraTarget { get { return CameraPosition+Quaternion.Euler(17,146,0)*Vector3.forward; } }
        public Vector3 DoorReadyPosition { get { return new Vector3(3f,0,11.02f); } }
        public float IntroProgress { get { return introTime/(StoryWall?3.3f:IntroDuration); } }
        public Vector3 PlayStartPosition {get{return StoryWall?RightSideLanding:DoorReadyPosition;}}
        public const float IntroDuration=4.8f;
        public string Hovered { get; private set; }
        public string LastAction { get; private set; }
        public bool Flipping { get { return flipTime>=0; } }
        public int SelectedDay { get; private set; }
        NightApp app;
        public NightTitleVolume TitleVolume {get;private set;}
        public NightStoryWall StoryWall {get;private set;}
        Transform model,calendarPage,blanket,alarmButton,minute,hour;
        Vector3 blanketRest,buttonRest;
        TextMesh dayText,timeText;
        float flipTime=-1,introTime,pulse,hoverSoundTime; int flipTarget,focusIndex;
        bool keyboardFocus;
        public bool CalendarEditing {get;private set;}
        public string FocusedId {get;private set;}
        static readonly string[] NavigationIds={"start","time","calendar","settings","wardrobe","credits","exit","language-zh","language-en"};
        public Vector2 FocusScreenPosition {get{return ScreenPosition(FocusedId);}}
        public bool HasNavigationFocus {get{return keyboardFocus;}}
        Vector3 initialCamera; Quaternion initialRotation;
        AudioSource sound;
        Font font;
        readonly Dictionary<string,AudioClip> clips=new Dictionary<string,AudioClip>();
        readonly List<Material> ownedMaterials=new List<Material>();
        readonly List<Hotspot> hotspots=new List<Hotspot>();
        Renderer[] bakedRenderers;
        class Hotspot { public string id; public Transform node; public Vector3 rest; public Collider hit; public Renderer[] renderers; public float amount; }

        public void Build(NightGame game,NightWorld world)
        {
            SelectedDay=game.levelIndex+1;app=world.GetComponent<NightApp>();
            font=Resources.Load<Font>("NightBack/Fonts/LilitaOne-Regular");
            if(!font)font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var prefab=Resources.Load<GameObject>("NightBack/BedroomStudio/ReferenceBedroom");
            if(!prefab)throw new InvalidOperationException("Import the Blender bedroom with Night Back/Bedroom/Import Blender Bedroom");
            model=Instantiate(prefab,transform,false).transform;model.name="Reference Bedroom · Blender";
            foreach(string id in new[]{"child-bed","bedside","bedroom-lamp"}){var old=world.FindFurniture(id);if(old)old.gameObject.SetActive(false);}
            var clock=transform.parent.Find("House Routine Wall Clock");if(clock)clock.gameObject.SetActive(false);
            var door=world.GetDoorTransform(0);var detailedDoor=model.Find("door");
            if(door&&detailedDoor){
                foreach(var r in door.GetComponentsInChildren<Renderer>())r.enabled=false;
                detailedDoor.SetParent(door,false);detailedDoor.localPosition=Vector3.zero;detailedDoor.localRotation=Quaternion.identity;
            }
            var renderers=new List<Renderer>(model.GetComponentsInChildren<Renderer>());
            if(detailedDoor)renderers.AddRange(detailedDoor.GetComponentsInChildren<Renderer>());bakedRenderers=renderers.ToArray();
            Solid("Bed collision",new Vector3(1.75f,.38f,12.5f),new Vector3(2.22f,.76f,1.67f));
            Solid("Right bedside cabinet collision",DeskPosition+Vector3.up*.405f,new Vector3(1.205f,.81f,1.095f));
            Solid("Left cabinet collision",new Vector3(3.36f,.345f,13.12f),new Vector3(.685f,.69f,.695f));
            Solid("Chest collision",new Vector3(5.80f,.39f,11.15f),new Vector3(.795f,.78f,.705f));
            blanket=model.Find("quilt");blanketRest=blanket.localPosition;
            alarmButton=model.Find("start");buttonRest=alarmButton.localPosition;minute=model.Find("minute");hour=model.Find("hour");
            Register(alarmButton,"start",.055f);
            var dial=Group(transform,"Departure dial",new Vector3(1.56f,1.04f,10.965f));Hot(dial,"time",Vector3.zero,new Vector3(.36f,.36f,.035f));
            // The physical hands show departure; the hover hint provides the digital time.
            BuildCalendar();
            foreach(string id in new[]{"settings","wardrobe","credits","exit"}){
                var item=model.Find(id);Register(item,id,.045f);
                if(id!="exit")Text(item,"Readable brass engraving",id.ToUpperInvariant(),new Vector3(0,.014f,.118f),.0037f,new Color(.95f,.72f,.39f));
            }
            Register(model.Find("photos"),"credits",.03f);
            SetBlanketPose(game.active?1:0);
            sound=gameObject.AddComponent<AudioSource>();sound.playOnAwake=false;sound.spatialBlend=0;
            foreach(string id in new[]{"switch","cloth","bed","lockPin"})clips[id]=NightSynth.Effect(id);
            UpdateTime(game.clock.displayTime);
            var lettering=new GameObject("GET YOUR NIGHT BACK · solid lettering");lettering.transform.SetParent(transform,false);
            TitleVolume=lettering.AddComponent<NightTitleVolume>();TitleVolume.Build();
            if(game.levelIndex==0){var story=new GameObject("First night · after lights out");story.transform.SetParent(transform,false);StoryWall=story.AddComponent<NightStoryWall>();StoryWall.Build(app,game.active);}
        }
        Transform Group(Transform parent,string name,Vector3 pos)
        {var t=new GameObject(name).transform;t.SetParent(parent,false);t.localPosition=pos;return t;}
        void Remove(UnityEngine.Object o){if(!o)return;if(Application.isPlaying)Destroy(o);else DestroyImmediate(o);}
        void Solid(string name,Vector3 center,Vector3 size)
        {var t=Group(transform,name,center);t.gameObject.AddComponent<BoxCollider>().size=size;}
        Transform Box(Transform parent,string name,Vector3 pos,Vector3 size,Color color)
        {
            var t=GameObject.CreatePrimitive(PrimitiveType.Cube).transform;t.name=name;t.SetParent(parent,false);t.localPosition=pos;t.localScale=size;Remove(t.GetComponent<Collider>());
            var mat=new Material(Shader.Find("Unlit/Color")){color=color};ownedMaterials.Add(mat);t.GetComponent<Renderer>().sharedMaterial=mat;return t;
        }
        TextMesh Text(Transform parent,string name,string value,Vector3 pos,float size,Color color,bool onTable=false)
        {
            var t=Group(parent,name,pos);t.localRotation=Quaternion.Euler(onTable?90:0,180,0);var text=t.gameObject.AddComponent<TextMesh>();
            text.text=value;text.font=font;text.fontSize=80;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.alignment=TextAlignment.Center;text.color=color;
            var r=text.GetComponent<Renderer>();r.sharedMaterial=font.material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return text;
        }
        Hotspot Hot(Transform node,string id,Vector3 center,Vector3 size)
        {
            var hit=node.gameObject.AddComponent<BoxCollider>();hit.center=center;hit.size=size;hit.isTrigger=true;
            var h=new Hotspot{id=id,node=node,hit=hit,rest=node.localPosition,renderers=node.GetComponentsInChildren<Renderer>()};hotspots.Add(h);return h;
        }
        void Register(Transform part,string id,float margin)
        {
            var bounds=part.GetComponent<Renderer>().bounds;
            Hot(part,id,part.InverseTransformPoint(bounds.center),bounds.size+Vector3.one*margin);
        }
        void BuildCalendar()
        {
            var calendar=Group(transform,"September calendar interaction",new Vector3(1.15f,.816f,10.855f));
            dayText=Text(calendar,"Date",SelectedDay.ToString(),new Vector3(0,.197f,.036f),.019f,new Color(.16f,.085f,.04f));
            Text(calendar,"Date code","9 / "+SelectedDay,new Vector3(0,.055f,.055f),.0042f,new Color(.34f,.20f,.10f));
            Hot(calendar,"calendar",new Vector3(0,.19f,.04f),new Vector3(.285f,.40f,.065f));
            calendarPage=Group(calendar,"Turning paper leaf",new Vector3(0,.393f,-.014f));calendarPage.gameObject.SetActive(false);
            Box(calendarPage,"Turning page",new Vector3(0,-.190f,.021f),new Vector3(.276f,.38f,.004f),new Color(.71f,.49f,.31f));
            Text(calendarPage,"Turning month","SEP",new Vector3(0,-.071f,.026f),.0055f,new Color(.16f,.09f,.045f));
            Text(calendarPage,"Turning date",SelectedDay.ToString(),new Vector3(0,-.195f,.027f),.019f,new Color(.16f,.09f,.045f));
            for(int i=0;i<2;i++){
                var tab=Group(calendar,i==0?"Previous date":"Next date",new Vector3(i==0?.174f:-.174f,.058f,.052f));
                Box(tab,"Brass page tab",Vector3.zero,new Vector3(.038f,.065f,.021f),new Color(.52f,.31f,.13f));
                Text(tab,"Arrow",i==0?"‹":"›",new Vector3(0,0,.013f),.0058f,new Color(.95f,.76f,.48f));
                Hot(tab,i==0?"previous":"next",Vector3.zero,new Vector3(.052f,.09f,.055f));
            }
        }
        public void SetOverview(bool overview){if(model&&model.Find("architecture"))model.Find("architecture").gameObject.SetActive(!overview);if(StoryWall)StoryWall.SetOverview(overview);}
        public void SetLightFactor(float factor)
        {
            if(bakedRenderers==null)return;
            foreach(var r in bakedRenderers)if(r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);var tint=r.name=="quilt"?new Color(.72f,.80f,.94f):r.name=="lamp"?new Color(1,.84f,.58f):Color.white;b.SetColor("_Color",tint*factor);r.SetPropertyBlock(b);}
        }
        public void UpdateTime(string value)
        {
            if(timeText)timeText.text=value;int minutes=22*60+30+(app?app.departureChoice:0)*15;
            if(minute)minute.localRotation=Quaternion.Euler(0,0,minutes*6);if(hour)hour.localRotation=Quaternion.Euler(0,0,minutes*.5f);
        }
        public float MenuFov(float aspect){return Mathf.Rad2Deg*2*Mathf.Atan(Mathf.Tan(29*Mathf.Deg2Rad)*1.5355731f/Mathf.Max(.9f,aspect));}
        public void SetMenuCamera(Camera camera)
        {
            camera.orthographic=false;camera.fieldOfView=MenuFov(camera.aspect);camera.transform.SetPositionAndRotation(CameraPosition,Quaternion.LookRotation(CameraTarget-CameraPosition));
            if(TitleVolume)TitleVolume.FitMenu(camera);

        }
        public void HandleMenuInput()
        {
            if(!app)app=GetComponentInParent<NightApp>();if(!app)return;
            float dt=Mathf.Min(Time.unscaledDeltaTime,.05f);pulse=Mathf.Max(0,pulse-dt);
            if(Flipping){
                flipTime+=dt;calendarPage.localRotation=Quaternion.Euler(-170*Mathf.SmoothStep(0,1,flipTime/.58f),0,0);
                if(flipTime>=.58f){int target=flipTarget;bool editing=CalendarEditing;string focus=FocusedId;bool navigation=keyboardFocus;
                    app.Preview(target);app.ui.Show("menu");var next=app.world.bedroomTitle;
                    next.CalendarEditing=editing;if(navigation)next.SelectFocus(focus??"calendar");}return;
            }
            var step=app.controller.MenuStep(Time.unscaledTime);
            if(Input.GetKeyDown(KeyCode.Tab))Focus(Input.GetKey(KeyCode.LeftShift)?-1:1);
            if(Input.GetKeyDown(KeyCode.DownArrow))step=Vector2.down;
            if(Input.GetKeyDown(KeyCode.UpArrow))step=Vector2.up;
            if(Input.GetKeyDown(KeyCode.LeftArrow))step=Vector2.left;
            if(Input.GetKeyDown(KeyCode.RightArrow))step=Vector2.right;
            if(Input.GetKeyDown(KeyCode.Escape)||app.controller.Down(7))CancelCalendar();
            HandleNavigation(step,false);
            if(app.controller.Down(10))TurnCalendar(-1);
            else if(app.controller.Down(11))TurnCalendar(1);
            if(Input.GetKeyDown(KeyCode.Return)||Input.GetKeyDown(KeyCode.Space)||app.controller.Down(6)){HandleNavigation(Vector2.zero,true);if(app.page!="menu")return;}
            if(Input.GetAxisRaw("Mouse X")!=0||Input.GetAxisRaw("Mouse Y")!=0){keyboardFocus=false;CalendarEditing=false;}
            string hover=keyboardFocus?FocusedId:null;
            if(!keyboardFocus&&!app.ui.PointerOverControls()){
                var hits=Physics.RaycastAll(app.viewCamera.ScreenPointToRay(Input.mousePosition),10,~0,QueryTriggerInteraction.Collide);float nearest=float.MaxValue;
                foreach(var h in hotspots)foreach(var hit in hits)if(hit.collider==h.hit&&hit.distance<nearest){nearest=hit.distance;hover=h.id;}
            }
            Hovered=hover;
            foreach(var h in hotspots){h.amount=Mathf.MoveTowards(h.amount,h.id==hover?1:0,dt*8);h.node.localPosition=h.rest+Vector3.up*(h.id=="start"?-.003f*h.amount-pulse*.025f:.012f*h.amount);
                foreach(var r in h.renderers){if(r is MeshRenderer&&r.GetComponent<TextMesh>()==null){var block=new MaterialPropertyBlock();r.GetPropertyBlock(block);block.SetColor("_EmissionColor",(r.sharedMaterial.HasProperty("_EmissionColor")?r.sharedMaterial.GetColor("_EmissionColor"):Color.black)+new Color(.24f,.13f,.045f)*h.amount);r.SetPropertyBlock(block);}}
            }
            app.ui.SetBedroomHint(Hint(hover));app.ui.SetBedroomCursor(FocusScreenPosition,keyboardFocus,CalendarEditing);
            if(Input.GetMouseButtonDown(0)&&hover!=null)Activate(hover);
        }
        public void HandleNavigation(Vector2 step,bool confirm)
        {
            if(Flipping)return;
            if(CalendarEditing){
                if(confirm){CancelCalendar();return;}
                if(step.x!=0){TurnCalendar(step.x>0?1:-1);return;}
                if(step.y!=0)CalendarEditing=false;
            }
            if(step!=Vector2.zero)MoveSpatialFocus(step);
            if(confirm){if(!keyboardFocus)SelectFocus("start");Activate(FocusedId);}
        }
        public void CancelCalendar(){CalendarEditing=false;}
        public void TurnCalendar(int direction){Activate(direction<0?"previous":"next");}
        public void SelectFocus(string id){FocusedId=id;focusIndex=Array.IndexOf(NavigationIds,id);keyboardFocus=true;Hovered=id;}
        public void Focus(int delta){SelectFocus(NavigationIds[keyboardFocus?(focusIndex+delta+NavigationIds.Length)%NavigationIds.Length:(delta<0?NavigationIds.Length-1:0)]);}
        Vector2 ScreenPosition(string id)
        {
            if(id=="language-zh"||id=="language-en")return app.ui.BedroomLanguagePosition(id=="language-en");
            if(string.IsNullOrEmpty(id))return Vector2.zero;
            return app.viewCamera.WorldToScreenPoint(HotspotPosition(id));
        }
        public void MoveSpatialFocus(Vector2 direction)
        {
            if(!keyboardFocus){SelectFocus("start");return;}
            Vector2 origin=ScreenPosition(FocusedId);string best=null;float score=float.MaxValue;
            foreach(string id in NavigationIds){
                if(id==FocusedId)continue;Vector2 delta=ScreenPosition(id)-origin;
                float forward=Vector2.Dot(delta,direction);if(forward<=2)continue;
                float side=Mathf.Abs(delta.x*direction.y-delta.y*direction.x);
                float cost=delta.magnitude+side*2.5f;
                if(cost<score){score=cost;best=id;}
            }
            if(best!=null)SelectFocus(best);
        }
        public Vector3 HotspotPosition(string id){var h=hotspots.Find(p=>p.id==id);return h==null?Vector3.zero:h.hit.bounds.center;}
        public string Hint(string id)
        {
            switch(id){
                case "start":return NightText.Pick("按下闹钟，轻轻起身。","Press the alarm. Slip out of bed.");
                case "calendar":case "next":case "previous":return CalendarEditing?NightText.Pick("←  9/"+SelectedDay+"  →    × / ○","←  9/"+SelectedDay+"  →    A / B"):NightText.Pick("9/"+SelectedDay+"    ×  翻日历","9/"+SelectedDay+"    A  Calendar");
                case "language-zh":return "中文";case "language-en":return "English";
                case "time":return NightText.Pick("转动表盘，选择出发时间 · ","Turn the dial to choose departure · ")+app.game.clock.displayTime;
                case "settings":return NightText.Pick("设置 · 声音、镜头和手柄","Settings · Sound, camera & controller");
                case "wardrobe":return NightText.Pick("衣柜 · 预览并选择睡衣","Wardrobe · Try on your nightwear");
                case "credits":return NightText.Pick("照片里的制作记录","Credits · Behind the photographs");
                case "practice":return NightText.Pick("练习本 · 学习五个基础动作","Notebook · Practice the five actions");
                case "continue":return app.HasSave?NightText.Pick("书签 · 继续上次行动","Bookmark · Continue your last night"):NightText.Pick("还没有行动存档。按闹钟开始。","No saved night yet. Press the alarm to begin.");
                case "exit":return NightText.Pick("挂好钥匙，结束今晚的游戏。","Hang up the keys. Call it a night.");
                default:return NightText.Pick("9/"+SelectedDay+"    ·    点击闹钟开始，翻日历选择日期","9/"+SelectedDay+"    ·    Press START. Turn the calendar to choose a date.");
            }
        }
        public void PlaySound(string id){if(!sound||!app)return;AudioClip clip;if(clips.TryGetValue(id,out clip))sound.PlayOneShot(clip,app.settings.master*app.settings.effects*.9f);}
        public void Activate(string id)
        {
            if(!app||app.page!="menu"||Flipping)return;LastAction=id;PlaySound(id=="calendar"||id=="next"||id=="previous"?"cloth":"switch");
            switch(id){
                case "start":app.BeginBedroomIntro();break;
                case "calendar":CalendarEditing=!CalendarEditing;SelectFocus("calendar");break;
                case "language-zh":app.ChangeLanguage(false);break;
                case "language-en":app.ChangeLanguage(true);break;
                case "next":case "previous":
                    flipTarget=(app.selectedNight+(id=="previous"?2:1))%3;flipTime=0;calendarPage.gameObject.SetActive(true);dayText.text=(flipTarget+1).ToString();break;
                case "time":app.departureChoice=(app.departureChoice+1)%3;app.game.clock=new NightClock(app.departureChoice*90);UpdateTime(app.game.clock.displayTime);app.world.Sync(app.game);break;
                case "settings":app.ui.OpenTitleSettings();break;
                case "wardrobe":app.ShowPage("wardrobe");break;
                case "credits":app.ShowPage("credits");break;
                case "practice":app.ContinueTutorial();break;
                case "continue":if(app.HasSave)app.Continue();else app.ui.ShowBedroomNotice(Hint("continue"));break;
                case "exit":app.ShowPage("exit");break;
            }
        }
        public void BeginIntro(Camera camera)
        {introTime=0;initialCamera=camera.transform.position;initialRotation=camera.transform.rotation;pulse=.22f;if(TitleVolume)TitleVolume.BeginIntro();if(StoryWall)StoryWall.Begin();PlaySound("bed");}
        void SetBlanketPose(float release)
        {
            if(!blanket)return;
            // The quilt stays on its own mattress throughout the rise, only drawing toward the foot.
            blanket.localPosition=blanketRest+new Vector3(.055f*release,-.008f*release,0);
        }
        public bool StepIntro(float dt,Camera camera)
        {
            introTime=Mathf.Min(StoryWall?3.3f:IntroDuration,introTime+dt);float t=introTime;
            alarmButton.localPosition=buttonRest-Vector3.up*(Mathf.Sin(Mathf.Clamp01(t/.45f)*Mathf.PI)*.009f);
            Vector3 sitUp=new Vector3(1.8f,1.48f,12.20f),edge=new Vector3(2.15f,1.10f,11.66f),standing=RightSideLanding+Vector3.up*1.37f,finish=DoorReadyPosition+Vector3.up*1.37f;
            Quaternion seatedRotation=Quaternion.Euler(24,169,0),finalRotation=Quaternion.Euler(StoryWall?-4:4,StoryWall?46:180,0);Vector3 position;Quaternion rotation;
            if(t<.4f){position=initialCamera;rotation=initialRotation;}
            else if(t<1.45f){float u=Mathf.SmoothStep(0,1,(t-.4f)/1.05f);position=Vector3.Lerp(initialCamera,sitUp,u);rotation=Quaternion.Slerp(initialRotation,seatedRotation,u);}
            else if(t<2.4f){float u=Mathf.SmoothStep(0,1,(t-1.45f)/.95f);position=Vector3.Lerp(sitUp,edge,u);rotation=seatedRotation;}
            else if(t<3.3f){float u=Mathf.SmoothStep(0,1,(t-2.4f)/.9f);position=Vector3.Lerp(edge,standing,u);rotation=Quaternion.Slerp(seatedRotation,finalRotation,u);}
            else {float u=Mathf.SmoothStep(0,1,(t-3.3f)/1.5f);position=Vector3.Lerp(standing,finish,u);if(app.settings.headBob)position.y+=Mathf.Sin(u*Mathf.PI*4)*.012f*Mathf.Sin(u*Mathf.PI);rotation=finalRotation;}
            SetBlanketPose(Mathf.SmoothStep(0,1,Mathf.Clamp01((t-.5f)/1.5f)));
            camera.fieldOfView=Mathf.Lerp(MenuFov(camera.aspect),72,Mathf.SmoothStep(0,1,(t-2.4f)/2.4f));camera.transform.SetPositionAndRotation(position,rotation);
            if(TitleVolume)TitleVolume.StepIntro(camera);
            return introTime>=(StoryWall?3.3f:IntroDuration);
        }
        void OnDestroy()
        {
            foreach(var m in ownedMaterials)if(m){
#if UNITY_EDITOR
                if(UnityEditor.AssetDatabase.Contains(m))continue;
#endif
                Remove(m);
            }
            foreach(var clip in clips.Values)Remove(clip);
        }
    }
}
