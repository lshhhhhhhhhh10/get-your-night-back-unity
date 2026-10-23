// @role Presents menus, interaction panels and results
// @calls NightControlDemonstration NightHearingMap NightBoardVisuals NightText

using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace NightBackPort
{
    public sealed class NightUI : MonoBehaviour
    {
        public NightApp app; public VisualElement root;
        List<Button> buttons=new List<Button>();List<Action> actions=new List<Action>();int focused;
        readonly List<VisualElement> controls=new List<VisualElement>();
        readonly List<Action> controlActions=new List<Action>();
        readonly Dictionary<string,int> pageFocus=new Dictionary<string,int>();
        VisualElement eventRoot;
        string helpBackPage="menu";
        public VisualElement FocusedControl {get{return focused>=0&&focused<controls.Count?controls[focused]:null;}}
        Label clock,phase,objective,hint,toast,modeTitle,modeHint,modeControl,modeReadout,tutorialTitle;
        VisualElement bedroomCursor; Button bedroomChinese,bedroomEnglish; Label bedroomHint; float bedroomNoticeUntil;
        Button finishPractice; VisualElement interaction;NightMap map;NightMechanism mechanism;
        string backPage="menu";
        NightControlDemonstration controlDemo; Label demoCaption,soundCaption; string demoScheme="keyboard",shownPage,previewPlayer,previewParent;
        static string L(string zh,string en){return NightText.Pick(zh,en);}
        Label Text(VisualElement parent,string text,string cls="body") {var label=new Label(text);label.AddToClassList(cls);parent.Add(label);return label;}
        VisualElement Box(VisualElement parent,string cls){var box=new VisualElement();foreach(var name in cls.Split(' '))box.AddToClassList(name);parent.Add(box);return box;}
        Button Button(VisualElement parent,string text,Action action,string cls="button")
        {var b=new Button(action){text=text};b.AddToClassList(cls);parent.Add(b);buttons.Add(b);actions.Add(action);RegisterControl(b,action);return b;}
        public void Show(string page)
        {
            var doc=GetComponent<UIDocument>();if(!doc)return;
            if(shownPage=="wardrobe"&&page!="wardrobe")app.world.SetSkins(app.settings.playerSkin,app.settings.parentSkin);
            if(page=="wardrobe"&&shownPage!="wardrobe"){previewPlayer=app.settings.playerSkin;previewParent=app.settings.parentSkin;app.world.ResetShowcaseOrbit();}
            if(shownPage!=null&&controls.Count>0&&shownPage!="playing")pageFocus[shownPage]=focused;
            shownPage=page;
            if(app.world.bedroomTitle&&app.world.bedroomTitle.TitleVolume)app.world.bedroomTitle.TitleVolume.SetPage(page);
            root=doc.rootVisualElement;BindInputEvents();root.focusController?.focusedElement?.Blur();root.Clear();root.AddToClassList("screen");
            var style=Resources.Load<StyleSheet>("NightBack/NightUI");if(style&&!root.styleSheets.Contains(style))root.styleSheets.Add(style);
            buttons.Clear();actions.Clear();controls.Clear();controlActions.Clear();focused=0;clock=null;interaction=null;map=null;demoCaption=null;soundCaption=null;finishPractice=null;
            if(controlDemo)controlDemo.SetVisible(false);
            bedroomHint=null;bedroomCursor=null;bedroomChinese=bedroomEnglish=null;
            if(page=="playing"){BuildHUD();Refresh();return;}
            if(page=="menu"){BuildBedroomMenu();return;}
            if(page=="intro"){root.pickingMode=PickingMode.Ignore;return;}
            root.pickingMode=PickingMode.Position;
            if(page=="pause"||page=="result")
            {
                var board=BuildBoardPage();
                if(page=="pause")BuildPause(board);else BuildResult(board);
                RestoreFocus();
                return;
            }
            var veil=Box(root,"veil"+(page=="menu"||page=="wardrobe"?" portrait-veil":""));var shell=Box(veil,"shell"+(page=="menu"?" menu-shell":page=="wardrobe"?" wardrobe-shell":""));
            var header=Box(shell,"header");Text(header,"NIGHT BACK  /  "+L("深夜行动部","DEPARTMENT OF LATE NIGHTS"),"eyebrow");
            var languages=Box(header,"row");Button(languages,"中文",()=>app.ChangeLanguage(false),"small-button");Button(languages,"EN",()=>app.ChangeLanguage(true),"small-button");
            if(page=="menu")BuildMenu(shell);
            else if(page=="pause")BuildPause(shell);
            else if(page=="result")BuildResult(shell);
            else if(page=="settings")BuildSettings(shell);
            else if(page=="wardrobe")BuildWardrobe(shell);
            else if(page=="help")BuildHelp(shell);
            else if(page=="credits")BuildCredits(shell);
            else if(page=="exit")BuildExit(shell);
            RestoreFocus();
            Text(shell,L("创作者与设计：Daniel  ·  AI 协作制作","Creator & design: Daniel  ·  Built with AI collaboration"),"credits");
        }
        void BuildMenu(VisualElement shell) { BuildBedroomMenu(); }
        void BuildBedroomMenu()
        {
            root.pickingMode=PickingMode.Ignore;
            var lang=Box(root,"bedroom-languages");bedroomChinese=Button(lang,"中文",()=>app.ChangeLanguage(false),"small-button");bedroomEnglish=Button(lang,"EN",()=>app.ChangeLanguage(true),"small-button");
            bedroomCursor=Box(root,"bedroom-cursor hidden");bedroomCursor.pickingMode=PickingMode.Ignore;
            bedroomHint=Text(root,"","bedroom-hint");bedroomHint.pickingMode=PickingMode.Ignore;
            var nav=Text(root,L("左摇杆 / ↑↓←→ 选择   × 确认   L1 / R1 翻页", "STICK / ARROWS  Select   A  Confirm   L1 / R1  Calendar"),"bedroom-navigation");nav.pickingMode=PickingMode.Ignore;
            SetBedroomHint(app.world.bedroomTitle?app.world.bedroomTitle.Hint(null):"");
        }
        public void SetBedroomHint(string text){if(bedroomHint!=null&&Time.unscaledTime>=bedroomNoticeUntil)bedroomHint.text=text;}
        public void ShowBedroomNotice(string text){if(bedroomHint!=null)bedroomHint.text=text;bedroomNoticeUntil=Time.unscaledTime+2.5f;}
        public Vector2 BedroomLanguagePosition(bool english)
        {
            var b=english?bedroomEnglish:bedroomChinese;if(b==null)return Vector2.zero;
            Vector2 point=b.worldBound.center;return new Vector2(point.x/root.worldBound.width*app.viewCamera.pixelWidth,(1-point.y/root.worldBound.height)*app.viewCamera.pixelHeight);
        }
        public void SetBedroomCursor(Vector2 screen,bool visible,bool editing)
        {
            if(bedroomCursor==null||root.panel==null)return;
            bedroomCursor.EnableInClassList("hidden",!visible);bedroomCursor.EnableInClassList("calendar-editing",editing);
            if(!visible)return;Vector2 p=new Vector2(screen.x/app.viewCamera.pixelWidth*root.worldBound.width,(1-screen.y/app.viewCamera.pixelHeight)*root.worldBound.height);
            bedroomCursor.style.left=p.x-10;bedroomCursor.style.top=p.y-10;
        }
        public void OpenTitleSettings(){backPage="menu";app.ShowPage("settings");}
        void BuildCredits(VisualElement shell)
        {
            var card=Box(shell,"center-card");Text(card,L("照片背面的名字","BEHIND THE PHOTOGRAPHS"),"eyebrow");Text(card,L("制作人员","Credits"),"card-title");
            Text(card,L("创意与设计：Daniel\nAI 协作、原生实现与床头场景：Codex\n卧室构图：依照用户提供的参考图重新建模。","Concept & design: Daniel\nAI collaboration, native code & bedside scene: Codex\nBedroom composition rebuilt from the supplied reference."),"body");
            Text(card,L("本场景使用程序化低多边形模型；沿用原项目人物、猫和声音。素材来源与许可保留在工程的 Provenance 记录中。","Procedural low-poly bedroom props. Characters, cat and audio retain the original project provenance and licenses."),"body");
            Button(card,L("把照片放回去","Put the photographs back"),()=>app.ShowPage("menu"),"primary");
        }
        void BuildExit(VisualElement shell)
        {
            var card=Box(shell,"center-card");Text(card,L("今晚先到这里？","CALL IT A NIGHT?"),"card-title");
            Text(card,L("上次行动的存档会保留。","Your saved night will be kept."),"body");
            Button(card,L("继续留在房间","Stay in the room"),()=>app.ShowPage("menu"),"primary");
            Button(card,L("退出游戏","Exit game"),()=>{app.SaveSettings();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying=false;
#else
                Application.Quit();
#endif
            },"secondary");
        }
        VisualElement BuildBoardPage()
        {
            var veil=Box(root,"board-veil");
            var scroll=new ScrollView(ScrollViewMode.Vertical){horizontalScrollerVisibility=ScrollerVisibility.Hidden};
            scroll.AddToClassList("board-scroll");veil.Add(scroll);
            var languages=Box(veil,"board-languages");
            Button(languages,"中文",()=>app.ChangeLanguage(false),"small-button");Button(languages,"EN",()=>app.ChangeLanguage(true),"small-button");
            var card=Box(scroll,"night-board");card.Add(new NightBoardSurface(NightBoardSurface.Kind.Board));
            return Box(card,"board-content");
        }
        void BoardTitle(VisualElement parent,string title)
        {
            var heading=Box(parent,"board-heading");
            heading.EnableInClassList("board-heading-long",title.Length>7);
            Text(heading,title,"board-heading-shadow").pickingMode=PickingMode.Ignore;
            Text(heading,title,"board-heading-edge").pickingMode=PickingMode.Ignore;
            Text(heading,title,"board-heading-face");
        }
        void BoardScore(VisualElement parent,string name,int value,int maximum,bool total=false)
        {
            var row=Box(parent,"board-score"+(total?" board-score-total":""));
            row.Add(new NightBoardSurface(NightBoardSurface.Kind.Plate));
            Text(row,name,"board-score-name");Text(row,value+" / "+maximum,"board-score-value");
        }
        Button BoardButton(VisualElement parent,string text,Action action,bool primary=false)
        {
            var slot=Box(parent,"board-action");
            var surface=new NightBoardSurface(primary?NightBoardSurface.Kind.Gold:NightBoardSurface.Kind.Slate);slot.Add(surface);
            var button=Button(slot,text,action,primary?"primary":"secondary");button.AddToClassList("board-button");
            button.RegisterCallback<PointerEnterEvent>(_=>{surface.Highlighted=true;surface.MarkDirtyRepaint();});
            button.RegisterCallback<PointerLeaveEvent>(_=>{surface.Highlighted=false;surface.MarkDirtyRepaint();});
            button.RegisterCallback<FocusEvent>(_=>{surface.Highlighted=true;surface.MarkDirtyRepaint();button.schedule.Execute(()=>button.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(button));});
            button.RegisterCallback<BlurEvent>(_=>{surface.Highlighted=false;surface.MarkDirtyRepaint();});
            return button;
        }
        void BuildPause(VisualElement card)
        {
            card.AddToClassList("pause-board");BoardTitle(card,"PAUSED");
            Text(card,L("先屏住呼吸。","HOLD THAT THOUGHT."),"board-subtitle");
            Text(card,L("时间、父母和正在发声的动作都暂停了。","The clock, your parent, and your actions are paused."),"board-note");
            var actions=Box(card,"board-actions");
            BoardButton(actions,L("继续行动","Resume"),()=>app.Resume(),true);
            BoardButton(actions,L("操作说明","Controls"),()=>OpenHelp("pause"));
            BoardButton(actions,L("设置","Settings"),()=>{backPage="pause";app.ShowPage("settings");});
            if(app.game.levelIndex==3){BoardButton(actions,L("从头练习","Restart practice"),()=>app.StartNight(3));if(app.game.tutorialStep==10&&app.game.mode==null)BoardButton(actions,L("完成基础练习","Complete practice"),()=>app.CompletePractice());}
            BoardButton(actions,L("保存并回主菜单","Save & return to menu"),()=>app.Menu());
            Text(card,L("ESC / ○ 继续行动","ESC / B TO RESUME"),"board-footnote");
        }
        void BuildResult(VisualElement card)
        {
            var g=app.game;bool won=g.status=="won"||g.status=="win",practice=g.levelIndex==3;g.EvaluatePerformance();
            card.AddToClassList("result-board");BoardTitle(card,won?(practice?"COMPLETE":"CLEARED"):"FAILED");
            if(practice)Text(card,L("基础动作，学会了。","YOU HAVE THE MOVES."),"board-subtitle");
            var stars=new NightRatingStars(g.metrics.score);stars.AddToClassList("board-stars");
            stars.tooltip=L("表现记录 ","PERFORMANCE ")+g.metrics.score+" / 100";card.Add(stars);
            int mission=won?40:0,sound=Mathf.RoundToInt(25*Mathf.Max(0,1-g.metrics.noiseBurden/160)),sight=Mathf.Max(0,10-g.exposures*5),action=g.metrics.score-mission-sound-sight;
            var scores=Box(card,"board-scores");
            BoardScore(scores,L("完成度","Overall"),g.metrics.score,100,true);
            BoardScore(scores,L("声音控制","Sound control"),sound,25);
            BoardScore(scores,L("动作判断","Action judgement"),action,25);
            BoardScore(scores,L("避开视线","Out of sight"),sight,10);
            var actions=Box(card,"board-actions");
            BoardButton(actions,L("再来一次","Try again"),()=>app.StartNight(app.game.levelIndex),true);
            if(won&&g.levelIndex<2)BoardButton(actions,L("下一夜 · ","Next night · ")+"9/"+(g.levelIndex+2),()=>app.StartNight(app.game.levelIndex+1));
            BoardButton(actions,L("回到主菜单","Main menu"),()=>app.Menu());
            VisualElement details=null;Button toggle=null;
            toggle=Button(card,L("＋ 行动记录","+ Run details"),()=>{bool open=details.ClassListContains("hidden");details.EnableInClassList("hidden",!open);toggle.text=open?L("－ 收起记录","− Hide details"):L("＋ 行动记录","+ Run details");},"board-details-toggle");
            details=Box(card,"board-details hidden");
            toggle.RegisterCallback<FocusEvent>(_=>toggle.schedule.Execute(()=>toggle.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(toggle)));
            Text(details,L("任务完成 ","Mission ")+mission+" / 40    "+L("等级 ","GRADE ")+g.metrics.grade,"board-detail-line");
            Text(details,practice?L("抓握、施力、松力、托住、轻放。接下来，试着把它们用在真实的一夜。","Grasp, push, release, support, and place. Now try them together in a real night."):won?L("任务完成。猫表示自己也有功劳。","Mission complete. The cat would like some credit."):L("一次声响不等于失败；留意翻身、脚步和门缝灯光。","One noise isn't a loss. Listen for movement and watch the door light."),"board-detail-line");
            Text(details,NightText.Translate(g.toast),"board-detail-line");
            Text(details,L("安静落脚 ","Quiet steps ")+g.metrics.goodSteps+" / "+g.metrics.steps+"   "+L("救场 ","Rescues ")+g.metrics.rescued+" / "+(g.metrics.rescued+g.metrics.failedRescues)+"   "+L("被看见 ","Exposures ")+g.exposures,"board-detail-line");
            for(int i=Mathf.Max(0,g.history.Count-2);i<g.history.Count;i++)Text(details,"· "+NightText.Translate(g.history[i]),"board-detail-line");
        }
        void Setting(VisualElement card,string label,float value,Action<float> change,Func<float,string> format=null)
        {
            var row=Box(card,"setting-row");var caption=Text(row,label,"body");caption.AddToClassList("setting-label");
            var s=new Slider(0,1){value=value,name="setting-"+controls.Count,tooltip=label};s.AddToClassList("volume-slider");row.Add(s);
            if(format==null)format=v=>Mathf.RoundToInt(v*100)+"%";
            var readout=Text(row,format(value),"setting-value");
            s.RegisterValueChangedCallback(e=>{change(e.newValue);readout.text=format(e.newValue);app.SaveSettings();});RegisterControl(s,null);
        }
        void BuildSettings(VisualElement shell)
        {
            var panel=Box(shell,"center-card settings-card");Text(panel,L("把耳朵照顾好","MAKE ROOM FOR THE QUIET"),"eyebrow");Text(panel,L("设置","Settings"),"card-title");
            Text(panel,L("上下选择 · 左右调节 · ENTER / × 确认 · ESC / ○ 返回","UP/DOWN SELECT · LEFT/RIGHT ADJUST · ENTER / A CONFIRM · ESC / B BACK"),"fine");
            var card=new ScrollView(ScrollViewMode.Vertical);card.AddToClassList("settings-scroll");panel.Add(card);
            Setting(card,L("总音量","Master volume"),app.settings.master,v=>app.settings.master=v);
            Setting(card,L("动作与提示音","Actions & cues"),app.settings.effects,v=>app.settings.effects=v);
            Setting(card,L("环境声","Ambience"),app.settings.ambience,v=>app.settings.ambience=v);
            Setting(card,L("夜曲","Music"),app.settings.music,v=>app.settings.music=v);
            Setting(card,L("鼠标灵敏度","Mouse sensitivity"),(app.settings.sensitivity-.3f)/1.7f,v=>app.settings.sensitivity=.3f+v*1.7f,v=>(.3f+v*1.7f).ToString("0.00")+"x");
            Setting(card,L("手柄转头速度","Controller look speed"),(app.settings.gamepadSensitivity-.3f)/1.7f,v=>app.settings.gamepadSensitivity=.3f+v*1.7f,v=>(.3f+v*1.7f).ToString("0.00")+"x");
            Setting(card,L("摇杆死区 5%–35%","Stick deadzone 5%–35%"),(app.settings.stickDeadzone-.05f)/.3f,v=>app.settings.stickDeadzone=.05f+v*.3f,v=>Mathf.RoundToInt(5+v*30)+"%");
            Setting(card,L("交互震动强度","Interaction vibration"),app.settings.hapticIntensity,v=>{app.settings.hapticIntensity=v;app.controller.Stop();});
            Setting(card,L("自适应扳机强度","Adaptive trigger strength"),app.settings.triggerStrength,v=>{app.settings.triggerStrength=v;app.controller.Stop();});
            Button(card,L("镜头起伏：","Head bob: ")+(app.settings.headBob?"ON":"OFF"),()=>{app.settings.headBob=!app.settings.headBob;app.SaveSettings();Show("settings");},"secondary");
            Button(card,L("交互震动：","Interaction vibration: ")+(app.settings.haptics?"ON":"OFF"),()=>{app.settings.haptics=!app.settings.haptics;app.controller.Stop();app.SaveSettings();Show("settings");},"secondary");
            Button(card,L("自适应扳机：","Adaptive triggers: ")+(app.settings.adaptive?"ON":"OFF"),()=>{app.settings.adaptive=!app.settings.adaptive;app.controller.Stop();app.SaveSettings();Show("settings");},"secondary");
            Text(card,app.controller.connected?L("已连接手柄。普通行走不震动。","Controller connected. Ordinary footsteps never vibrate."):L("未检测到手柄；接入后自动识别。","No controller detected. Connect one for automatic discovery."),"fine");
            if(backPage=="menu"){
                if(app.HasSave)Button(card,L("继续上次行动","Continue saved night"),()=>app.Continue(),"secondary");
                Button(card,L("练习五个基础动作","Practice the five actions"),()=>app.ContinueTutorial(),"secondary");
                Button(card,L("操作说明","Controls"),()=>OpenHelp("settings"),"secondary");
            }
            Text(card,L("声音设置只调整听到的音量，不改变父母听觉。断线或失焦会暂停。","Volume changes what you hear, not parent hearing. Disconnecting or leaving the window pauses play."),"fine");
            Button(panel,L("返回","Back"),()=>app.ShowPage(backPage),"primary");
        }
        static readonly string[] Skins={"golden-frog","golden-bull","nailong","naiwa","naishu","scarf","crab","chef","box","pillow","nightcap"};
        static readonly string[] SkinNames={"金蛙","金牛","奶龙","奶蛙","奶鼠","围巾","螃蟹","厨师","纸箱","枕头","睡帽"};
        void BuildWardrobe(VisualElement shell)
        {
            var card=new ScrollView(ScrollViewMode.Vertical);card.AddToClassList("wardrobe-card");card.AddToClassList("menu-scroll-card");shell.Add(card);Text(card,L("潜行也要有睡衣品味。","SNEAKING OUT, DRESSED IN."),"eyebrow");Text(card,L("衣柜","Wardrobe"),"card-title");
            Text(card,L("你的造型","YOUR LOOK"),"section-label");var row=Box(card,"row wrap");for(int i=0;i<Skins.Length;i++){int n=i;Button(row,L(SkinNames[i],Skins[i]),()=>{previewPlayer=Skins[n];app.world.SetSkins(previewPlayer,previewParent);Show("wardrobe");},previewPlayer==Skins[i]?"selected-button":"night-button");}
            Text(card,L("家长的造型","YOUR PARENT'S LOOK"),"section-label");row=Box(card,"row wrap");for(int i=0;i<Skins.Length;i++){int n=i;Button(row,L(SkinNames[i],Skins[i]),()=>{previewParent=Skins[n];app.world.SetSkins(previewPlayer,previewParent);Show("wardrobe");},previewParent==Skins[i]?"selected-button":"night-button");}
            Text(card,L("拖动左侧人物或用右摇杆旋转预览。上下选择，× 确认，○ 返回；确认穿上后才保存，外观没有能力加成。","Drag or use the right stick to rotate. Up/down select, A confirm, B back. Confirm to wear. Outfits never change abilities."),"fine");
            Button(card,L("确认穿上 · 回去行动","WEAR THESE · BACK TO BUSINESS"),()=>{app.settings.playerSkin=previewPlayer;app.settings.parentSkin=previewParent;app.SaveSettings();app.ShowPage("menu");},"primary");
            Button(card,L("保持原样","Keep current outfits"),()=>app.ShowPage("menu"),"secondary");
            var orbit=Box(shell,"wardrobe-orbit");Vector2 previous=Vector2.zero;bool dragging=false;
            orbit.RegisterCallback<PointerDownEvent>(e=>{dragging=true;previous=e.position;orbit.CapturePointer(e.pointerId);e.StopPropagation();});
            orbit.RegisterCallback<PointerMoveEvent>(e=>{if(!dragging)return;Vector2 point=e.position;app.world.ShowcaseOrbit(point-previous);previous=point;e.StopPropagation();});
            orbit.RegisterCallback<PointerUpEvent>(e=>{dragging=false;orbit.ReleasePointer(e.pointerId);e.StopPropagation();});
        }
        void BuildHelp(VisualElement shell)
        {
            var card=new ScrollView(ScrollViewMode.Vertical);card.AddToClassList("wardrobe-card");card.AddToClassList("menu-scroll-card");shell.Add(card);Text(card,L("五个动作，一屋子变化。","FIVE ACTIONS. ONE UNPREDICTABLE HOUSE."),"eyebrow");Text(card,L("操作说明","Controls"),"card-title");
            Text(card,L("WASD / 左摇杆：移动　　鼠标 / 右摇杆：看四周\nC / R1：蹲行　　V / R3：切换近景　　M / △：局部环境总览\nE / □：抓握、搜索、互动　　按住 E 渐进施力 / R2 调整压力\nQ、右鼠标或 Shift / L2：左手托稳　　R / L1：反向或卸力\n空格 / ×：落脚、确认　　Q / Options：丢猫玩具\nEsc / ○：松手退出；无交互时暂停　　P / Menu：暂停", "WASD / left stick: move    Mouse / right stick: look\nC / R1: crouch    V / R3: camera    M / Y: overview\nE / X: grasp, search, interact    Hold E: build force / R2: pressure\nQ, right mouse or Shift / L2: support with the left hand\nR / L1: reverse / release tension    Space / A: confirm\nQ / Options: cat toy    Esc / B: release or pause    P / Menu: pause"),"help-copy");
            Text(card,L("门：先抓握，再听门轴声调力；到头前收力。\n锁：选弹子、抬到接缝、松手；腰形弹子需要卸力。\n救场：WASD 移手，双手托住，再对准落点轻放。\n返程：拿到手机还没赢。停下静音，安全回卧室。\n时间：洗衣声能遮住部分动作，却遮不住视线。", "Doors: grasp, listen, and ease off before the stop.\nLocks: select, lift to the seam, release. Spool pins need tension relief.\nRescues: move your hands, support, align, and lower gently.\nReturn: finding the phone is only half the job. Silence it, then get home.\nTime: laundry can cover a noise. It cannot hide you from sight."),"body");
            Button(card,L("明白了","Got it"),()=>app.ShowPage(helpBackPage),"primary");
        }
        void BuildHUD()
        {
            var hud=Box(root,"hud");hud.pickingMode=PickingMode.Ignore;
            var top=Box(hud,"hud-top");var time=Box(top,"clock-card");clock=Text(time,"22:30","clock");phase=Text(time,"","fine");
            var goal=Box(top,"objective-card");Text(goal,app.game.IsTutorial?L("练习","PRACTICE"):"9/"+(app.game.levelIndex+1),"eyebrow");objective=Text(goal,"","objective");
            Button(top,L("暂停  P","PAUSE  P"),()=>app.Pause(),"small-button");
            map=new NightMap();map.AddToClassList("mini-map");map.app=app;hud.Add(map);map.RegisterCallback<PointerDownEvent>(e=>{app.overview=!app.overview;app.world.SetOverview(app.overview);app.SetCursor();e.StopPropagation();});
            var bottom=Box(hud,"hud-bottom"+(app.game.IsTutorial?"":" hidden"));var information=Box(bottom,"hint-card");tutorialTitle=Text(information,"","eyebrow");hint=Text(information,"","hint");toast=Text(information,"","toast");
            Text(information,L("WASD 移动   C 蹲行   E 互动   V 视角   M 总览","WASD MOVE   C CROUCH   E INTERACT   V CAMERA   M OVERVIEW"),"fine");
            interaction=Box(hud,"interaction-card");modeTitle=Text(interaction,"","mode-title");modeHint=Text(interaction,"","body");
            mechanism=new NightMechanism();mechanism.AddToClassList("mechanism");mechanism.app=app;interaction.Add(mechanism);
            modeReadout=Text(interaction,"","statline");modeControl=Text(interaction,"","fine");
            soundCaption=Text(hud,"","sound-caption");soundCaption.pickingMode=PickingMode.Ignore;
            Text(hud,"+","reticle");
            if(app.game.levelIndex==3){BuildControlDemo(hud);finishPractice=Button(information,L("完成基础练习","COMPLETE PRACTICE"),()=>app.CompletePractice(),"small-button");}
        }
        void BuildControlDemo(VisualElement hud)
        {
            controlDemo=NightControlDemonstration.Ensure(gameObject);controlDemo.Sync(app.game,demoScheme);controlDemo.SetVisible(true);
            var card=Box(hud,"control-demo");var row=Box(card,"row");
            Button(row,L("键鼠示范","KEYBOARD"),()=>{demoScheme="keyboard";controlDemo.SetScheme(demoScheme);},"small-button");
            Button(row,L("手柄示范","GAMEPAD"),()=>{demoScheme="controller";controlDemo.SetScheme(demoScheme);},"small-button");
            var preview=new Image{image=controlDemo.Texture,scaleMode=ScaleMode.ScaleToFit};preview.AddToClassList("control-preview");card.Add(preview);
            Vector2 previous=Vector2.zero;bool dragging=false;
            preview.RegisterCallback<PointerDownEvent>(e=>{dragging=true;previous=e.position;preview.CapturePointer(e.pointerId);e.StopPropagation();});
            preview.RegisterCallback<PointerMoveEvent>(e=>{if(!dragging)return;Vector2 current=e.position;controlDemo.DragOrbit(current-previous);previous=current;e.StopPropagation();});
            preview.RegisterCallback<PointerUpEvent>(e=>{dragging=false;preview.ReleasePointer(e.pointerId);e.StopPropagation();});
            demoCaption=Text(card,controlDemo.Caption,"demo-caption");
            Text(card,L("M 显示鼠标 · 拖动示范可旋转","M TO FREE CURSOR · DRAG TO ROTATE"),"fine");
        }
        public bool PointerOverControls()
        {
            if(root==null||root.panel==null||UnityEngine.Cursor.lockState==CursorLockMode.Locked)return false;
            var mouse=Input.mousePosition;var position=RuntimePanelUtils.ScreenToPanel(root.panel,new Vector2(mouse.x,Screen.height-mouse.y));
            var picked=root.panel.Pick(position);
            for(var e=picked;e!=null;e=e.parent)if(e is Button||e.ClassListContains("control-demo")||e.ClassListContains("mini-map"))return true;
            return false;
        }
        public void Refresh()
        {
            if(clock==null||app.game==null)return;var g=app.game;
            if(demoCaption!=null&&controlDemo){controlDemo.Sync(g,demoScheme);demoCaption.text=controlDemo.Caption;}
            if(soundCaption!=null){soundCaption.text=g.IsTutorial&&app.audioSystem?app.audioSystem.CurrentCaption:"";soundCaption.EnableInClassList("hidden",string.IsNullOrEmpty(soundCaption.text));}
            if(finishPractice!=null){finishPractice.EnableInClassList("hidden",g.tutorialStep!=10);finishPractice.SetEnabled(g.mode==null);}
            clock.text=g.clock.displayTime;phase.text=g.levelIndex==3?L("基础动作 · 不限时","BASIC ACTIONS · NO TIME LIMIT"):NightText.Translate(g.clock.phaseName);
            objective.text=g.levelIndex==3?L("基础动作练习 · ","BASIC ACTION PRACTICE · ")+(g.tutorialStep+1)+" / "+NightGame.TutorialStepCount:(g.hasDevice?L("把手机带回卧室","BRING THE PHONE BACK TO BED"):L("找到手机，再回卧室","FIND YOUR PHONE. THEN GET HOME."));
            if(tutorialTitle!=null){tutorialTitle.EnableInClassList("hidden",g.levelIndex!=3);tutorialTitle.text=g.levelIndex==3?NightText.Translate(g.TutorialTitle()):"";}
            hint.text=NightText.Translate(g.levelIndex==3?g.TutorialHint():g.InteractionHint());toast.text=g.toastLeft>0?NightText.Translate(g.toast):"";
            interaction.EnableInClassList("hidden",g.mode==null||(!g.IsTutorial&&(g.mode.type=="catch"||g.mode.type=="reaction"||g.mode.type=="search")));
            if(g.mode!=null){var m=g.mode;
                bool door=m.type=="door";
                interaction.EnableInClassList("door-feedback",door);interaction.EnableInClassList("quiet-interaction",!g.IsTutorial);
                modeTitle.EnableInClassList("hidden",door);modeHint.EnableInClassList("hidden",door||!g.IsTutorial);modeReadout.EnableInClassList("hidden",door||!g.IsTutorial);
                modeTitle.text=ModeName(m.type);modeHint.text=NightText.Translate(m.hint??"");
                modeReadout.text=m.type=="door"?L("听声音，感受阻力。","Listen. Feel the resistance."):m.type=="step"?L("在亮区内落脚","LAND IN THE LIT ZONE"):m.type=="lockpick"?L("顶到接缝，再松力。","LIFT TO THE SEAM, THEN RELEASE."):m.type=="catch"?L("对准物件，保持支撑。","ALIGN AND KEEP SUPPORTING."):"";
                switch(m.type){
                    case "step":modeControl.text=L("空格 / × 轻轻落脚 · Esc / ○ 退开","SPACE / A: LAND · ESC / B: STEP BACK");break;
                    case "step-transit":modeControl.text=L("正在落脚，片刻后可继续移动。","Finishing the step. Movement resumes in a moment.");break;
                    case "door":modeControl.text="E / R2     R / L1     Esc / ○";break;
                    case "lockpick":modeControl.text=L("A D / 左摇杆选弹子 · E / R2 顶起 · 接缝齐平时松手 · Q / L2 卸力","A D / LEFT STICK: SELECT · E / R2: LIFT · RELEASE AT THE SEAM · Q / L2: EASE TENSION");break;
                    case "search":modeControl.text=L("正在自动翻找 · Esc / ○ 立即停下，听屋里的动静","SEARCHING · ESC / B: STOP AND LISTEN");break;
                    case "tickle":modeControl.text=L("按住 E 或鼠标轻挠 · 手柄 R2 配合右摇杆左右 · 松手停止 · Esc / ○ 收手","HOLD E OR MOUSE: TICKLE · PAD: R2 + RIGHT STICK LEFT/RIGHT · RELEASE: STOP · ESC / B: WITHDRAW");break;
                    case "reaction":modeControl.text=g.resumeSearchIndex>=0?L("等物件落稳后继续翻找 · Esc / ○ 可取消继续翻找","WAIT FOR THE OBJECT TO SETTLE · ESC / B CANCELS THE RESUMED SEARCH"):L("等物件落稳，随后恢复移动。","Wait for the object to settle. Movement will resume.");break;
                    case "catch":
                        modeControl.text=m.kind=="pencils"?L("A D / 左摇杆左右移手 · 按住 E / R2 分别拦住三枝铅笔","A D / LEFT STICK: MOVE HAND · HOLD E / R2: CATCH EACH PENCIL"):m.kind=="fork"?L("A D / 左摇杆对准 · E / R2 托住 · Q / L2 裹住止响","A D / LEFT STICK: ALIGN · E / R2: SUPPORT · Q / L2: WRAP AND MUFFLE"):L("保持 Q + E / L2 + R2 支撑 · WASD / 左摇杆移物 · 向上送回，再向下轻放","KEEP Q + E / L2 + R2 HELD · WASD / LEFT STICK: MOVE · UP TO RETURN, DOWN TO PLACE");break;
                    default:modeControl.text="";break;
                }
                mechanism.MarkDirtyRepaint();}
            map.MarkDirtyRepaint();
        }
        string ModeName(string type)
        {
            switch(type){case "door":return L("抓握 · 推拉 · 松力","GRASP · PUSH · RELEASE");case "step":case "step-transit":return L("轻轻落脚","EASY DOES IT");case "lockpick":return L("锁里的小机关","A LITTLE MECHANICAL PERSUASION");case "search":return L("轻轻翻找","A QUIET LITTLE SEARCH");case "reaction":return app.game.mode!=null&&app.game.mode.caught?L("稳稳归位","GENTLY BACK IN PLACE"):L("物件落地 · 听听动静","AN OBJECT FALLS · LISTEN");case "catch":return L("别。掉。下。来。","PLEASE. DON'T. DROP.");case "tickle":return L("挠痒 · 留意呼吸与缩脚","TICKLE · WATCH THE RESPONSE");default:return NightText.Translate(type);}
        }
        void OpenHelp(string from){helpBackPage=from;app.ShowPage("help");}
        public void GoBack(){if(app.page=="settings")app.ShowPage(backPage);else if(app.page=="help")app.ShowPage(helpBackPage);else if(app.page=="pause")app.Resume();else app.Menu();}
        public void ShowError(string error){if(root!=null)Text(root,error,"error");}
        void BindInputEvents()
        {
            if(eventRoot==root)return;eventRoot=root;
            root.RegisterCallback<NavigationSubmitEvent>(e=>{e.PreventDefault();e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationMoveEvent>(e=>{e.PreventDefault();e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            root.RegisterCallback<NavigationCancelEvent>(e=>{e.PreventDefault();e.StopImmediatePropagation();},TrickleDown.TrickleDown);
            root.RegisterCallback<KeyDownEvent>(e=>{
                if(e.keyCode==KeyCode.Space||e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter||e.keyCode==KeyCode.Tab||e.keyCode==KeyCode.Escape||e.keyCode==KeyCode.LeftArrow||e.keyCode==KeyCode.RightArrow||e.keyCode==KeyCode.UpArrow||e.keyCode==KeyCode.DownArrow){e.PreventDefault();e.StopImmediatePropagation();}
            },TrickleDown.TrickleDown);
            root.RegisterCallback<KeyUpEvent>(e=>{if(e.keyCode==KeyCode.Space||e.keyCode==KeyCode.Return||e.keyCode==KeyCode.KeypadEnter){e.PreventDefault();e.StopImmediatePropagation();}},TrickleDown.TrickleDown);
        }
        void RegisterControl(VisualElement element,Action action)
        {
            element.focusable=shownPage!="playing";
            controls.Add(element);controlActions.Add(action);
            element.RegisterCallback<FocusInEvent>(_=>SelectControl(element,false));
            element.RegisterCallback<PointerDownEvent>(_=>SelectControl(element,false),TrickleDown.TrickleDown);
        }
        bool Available(VisualElement element)
        {
            if(element==null||!element.enabledInHierarchy||!element.visible)return false;
            for(var e=element;e!=null;e=e.parent)if(e.resolvedStyle.display==DisplayStyle.None||e.ClassListContains("hidden"))return false;
            return true;
        }
        void RestoreFocus()
        {
            int index;if(!pageFocus.TryGetValue(shownPage,out index)){
                index=shownPage=="settings"?controls.FindIndex(c=>c is Slider):controls.FindIndex(c=>c.ClassListContains("primary"));
            }
            focused=Mathf.Clamp(index,0,Mathf.Max(0,controls.Count-1));
            if(Available(FocusedControl))SelectControl(FocusedControl,true);else MoveFocus(1);
        }
        void SelectControl(VisualElement element,bool giveFocus)
        {
            int index=controls.IndexOf(element);if(index<0||!Available(element)||shownPage=="playing")return;
            focused=index;
            foreach(var c in controls){c.EnableInClassList("menu-focused",c==element);if(c is Slider)c.parent.EnableInClassList("setting-focused",c==element);}
            if(giveFocus)element.Focus();
            element.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(element);
            element.schedule.Execute(()=>{if(FocusedControl==element&&element.panel!=null)element.GetFirstAncestorOfType<ScrollView>()?.ScrollTo(element);}).StartingIn(1);
        }
        public void MoveFocus(int delta)
        {
            if(controls.Count==0||shownPage=="playing")return;
            for(int i=0;i<controls.Count;i++){focused=(focused+delta+controls.Count)%controls.Count;if(Available(FocusedControl)){SelectControl(FocusedControl,true);return;}}
        }
        public void AdjustFocused(int direction)
        {
            if(shownPage=="playing"||!Available(FocusedControl))return;
            var slider=FocusedControl as Slider;
            if(slider!=null)slider.value=Mathf.Clamp01(Mathf.Round((slider.value+direction*.05f)*100)/100);else MoveFocus(direction);
        }
        public void ScrollMenu(float direction)
        {
            var scroll=FocusedControl?.GetFirstAncestorOfType<ScrollView>()??root?.Q<ScrollView>();
            if(scroll!=null)scroll.scrollOffset+=new Vector2(0,direction*360*Time.unscaledDeltaTime);
        }
        public void ActivateFocused(){if(shownPage!="playing"&&Available(FocusedControl))controlActions[focused]?.Invoke();}
    }
    public abstract class NightDrawing : VisualElement
    {
        protected MeshGenerationContext context;
        protected void Quad(float x,float y,float w,float h,Color c)
        {
            if(w<=0||h<=0)return;var mesh=context.Allocate(4,6);
            mesh.SetNextVertex(new Vertex{position=new Vector3(x,y,Vertex.nearZ),tint=c});mesh.SetNextVertex(new Vertex{position=new Vector3(x+w,y,Vertex.nearZ),tint=c});mesh.SetNextVertex(new Vertex{position=new Vector3(x+w,y+h,Vertex.nearZ),tint=c});mesh.SetNextVertex(new Vertex{position=new Vector3(x,y+h,Vertex.nearZ),tint=c});
            mesh.SetNextIndex(0);mesh.SetNextIndex(1);mesh.SetNextIndex(2);mesh.SetNextIndex(2);mesh.SetNextIndex(3);mesh.SetNextIndex(0);
        }
    }
    public sealed class NightMechanism : NightDrawing
    {
        public NightApp app;public NightMechanism(){generateVisualContent+=Draw;}
        void Draw(MeshGenerationContext c){if(app==null||app.game==null||app.game.mode==null)return;context=c;var m=app.game.mode;float w=contentRect.width,h=contentRect.height;var gold=new Color(1,.77f,.37f);var mint=new Color(.55f,.87f,.75f);var blue=new Color(.22f,.31f,.40f);
            if(m.type=="step"){Quad(8,h*.45f,w-16,18,blue);float width=app.game.level.stepWidth;Quad(w*(.5f-width*.5f),h*.4f,w*width,34,mint);Quad(Mathf.Clamp01(m.pointer)*(w-8),h*.32f,5,58,gold);}
            else if(m.type=="lockpick"){var state=app.game.lockState;if(state!=null){int n=state.pins.Length;for(int i=0;i<n;i++){var pin=state.pins[i];float x=28+i*(w-45)/n;Quad(x,h*.1f,24,h*.78f,blue);Quad(x-5,h*.72f-pin.target*h*.48f,34,3,mint);Quad(x+3,h*.72f-pin.lift*h*.48f,18,30,pin.seated?mint:(i==state.selected?gold:new Color(.55f,.61f,.64f)));if(pin.spool&&!pin.cleared)Quad(x+7,h*.5f,10,5,new Color(.9f,.42f,.25f));}}}
            else if(m.type=="reaction"||m.type=="catch"){var r=app.game.rescue;Quad(w*.1f,h*.78f,w*.8f,10,blue);Quad(w*.5f-27,h*.71f,54,6,mint);if(r!=null){float ox=w*(.5f+-r.objectX*.38f),hx=w*(.5f+-r.handX*.38f),oy=h*(.7f-Mathf.Clamp01(r.height/1.3f)*.48f);Quad(ox-13,oy-15,26,32,gold);Quad(hx-35,h*.6f,25,10+15*r.left,mint);Quad(hx+10,h*.6f,25,10+15*r.right,mint);if(r.pencils!=null)foreach(var p in r.pencils)Quad(w*(.5f+-p.x*.38f),h*.32f,4,30,p.state=="caught"?mint:gold);}}
            else if(m.type=="door"){
                float center=w*.5f,span=w*.40f,direction=m.phase=="close"?-1:1,end=center+span*Mathf.Clamp01(m.force)*direction;
                Quad(center-span,h*.45f,span*2,2,blue);Quad(Mathf.Min(center,end),h*.45f,Mathf.Abs(end-center),2,mint);
                Quad(center-1,h*.45f-5,2,12,new Color(.7f,.77f,.78f));Quad(end-2,h*.45f-3,4,8,gold);
                Quad(direction>0?center+span-5:center-span,h*.45f-2,5,6,new Color(.7f,.77f,.78f));
            }
            else if(m.type=="tickle"){Quad(8,h*.47f,w-16,12,blue);Quad(8,h*.47f,(w-16)*Mathf.Clamp01(app.game.tickleHeat),12,new Color(.97f,.43f,.30f));}
            else{Quad(8,h*.47f,w-16,12,blue);Quad(8,h*.47f,(w-16)*Mathf.Clamp01(m.progress),12,mint);}
        }
    }
}
