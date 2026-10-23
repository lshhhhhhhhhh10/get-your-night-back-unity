#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    public partial class NightGame
    {
        // 在真实编辑器中运行的独立机制检查：不移动实际玩家，不写存档，不作为整局证据。
        public static List<string> ValidateInteractionMechanics()
        {
            var passed = new List<string>();
            NightGame g = InteractionFixture(0);
            g.player = g.doors[0].Position + new Vector3(0, 0, 1.05f); g.TryInteract();
            InteractionAssert(g.mode != null && g.mode.type == "door", "抓门建立交互");
            for (int i = 0; i < 80; i++) InteractionFrame(g, new NightInput { force = .5f });
            float before = g.doors[0].progress;
            InteractionFrame(g, new NightInput());
            InteractionAssert(before > .1f && Mathf.Abs(before - g.doors[0].progress) < .000001f && g.doors[0].speed == 0, "门释放即停");
            InteractionFrame(g, new NightInput { reverse = true });
            for (int i = 0; i < 5; i++) InteractionFrame(g, new NightInput { force = .5f });
            InteractionAssert(g.doors[0].progress < before, "门反向关闭"); passed.Add("门：抓握、连续施力、松手停止和反向");

            g = InteractionFixture(0); g.player = g.doors[0].Position + new Vector3(0, 0, 1.05f); g.TryInteract();
            for (int i = 0; i < 250 && g.mode != null; i++) InteractionFrame(g, new NightInput { force = 1 });
            InteractionAssert(g.doors[0].progress >= 1 && g.metrics.doorBumps == 1, "最大力到头撞框一次");
            InteractionFrame(g, new NightInput { force = 1 }); InteractionAssert(g.metrics.doorBumps == 1, "结束后不重复撞框");
            passed.Add("门：最大力终点撞击一次，未降低原参数");

            g = InteractionFixture(0);
            Vector2 tile = g.level.creaks[0];
            g.player = new Vector3(tile.x, 0, tile.y + .52f); g.floorX = Mathf.RoundToInt(g.player.x); g.floorZ = Mathf.RoundToInt(g.player.z);
            g.lastSnore = -100;
            InteractionAssert(g.TryFloorStep(new Vector3(tile.x, 0, tile.y + .48f)), "落脚截获");
            g.time = 0; InteractionFrame(g, new NightInput { confirm = true });
            InteractionAssert(g.metrics.steps == 1 && g.metrics.goodSteps == 1 && g.mode.type == "step-transit", "亮区落脚与过渡");
            passed.Add("木板：进入砖格截获、22%亮区确认与平滑落脚");

            for (int levelIndex = 0; levelIndex < 3; levelIndex++)
            {
                g = InteractionFixture(levelIndex);
                var spot = g.spots.Find(g.SpotLocked); g.BeginLock(spot);
                InteractionFrame(g, new NightInput());
                int frames = 0;
                while (!spot.unlocked && frames++ < 1800)
                {
                    NightLock mechanism = g.lockState;
                    var pin = mechanism.pins[mechanism.selected];
                    var input = new NightInput();
                    if (pin.seated)
                    {
                        bool all = true; foreach (var value in mechanism.pins) if (!value.seated) all = false;
                        if (!all) input.move = mechanism.navigation == 0 ? Vector2.right : Vector2.zero;
                    }
                    else if (pin.lift < pin.target - .007f) { input.force = .55f; input.leftForce = pin.spool ? .55f : 0; }
                    InteractionFrame(g, input);
                }
                InteractionAssert(spot.unlocked && g.mode != null && g.mode.type == "search", "锁类别 " + levelIndex + " 完成后进入翻找");
                passed.Add("锁 " + (levelIndex + 1) + "：逐针顶起、齐平松手、腰形卸力、锁芯转开");
            }
            g = InteractionFixture(2); g.BeginLock(g.spots.Find(g.SpotLocked));
            g.lockState.pins[0].seated = true; g.lockState.pins[0].lift = g.lockState.pins[0].target;
            g.lockState.selected = 1; g.lockState.pins[1].cleared = true; g.lockState.pins[1].lift = 1;
            g.lockState.armed = true;
            for (int i = 0; i < 14; i++) InteractionFrame(g, new NightInput { force = 1 });
            InteractionAssert(!g.lockState.pins[0].seated, "共用压片导致相邻已设弹子滑脱");
            g.lockState.pins[1].lift = g.lockState.pins[1].target; g.lockState.held = true;
            g.ReleaseInteractionInputs(); InteractionFrame(g, new NightInput());
            InteractionAssert(!g.lockState.pins[1].seated && g.lockState.pressure == 0, "暂停释放不会设定弹子");
            passed.Add("共用压片误顶后果与暂停不误设弹子");

            g = InteractionFixture(0); g.BeginLock(g.spots.Find(g.SpotLocked));
            InteractionFrame(g, new NightInput());
            InteractionFrame(g, new NightInput { analog = true, force = .2f, rightForce = .2f, actionHeld = true });
            InteractionAssert(Mathf.Abs(g.lockState.pressure - .2f) < .00001f, "轻压扳机不被键盘默认值钳高");
            g = InteractionFixture(0); g.BeginIncident("vase");
            InteractionFrame(g, new NightInput { analog = true, force = .2f, rightForce = .2f, leftForce = .2f, actionHeld = true });
            InteractionAssert(Mathf.Abs(g.rescue.right - .2f) < .00001f && Mathf.Abs(g.rescue.left - .2f) < .00001f, "接物双扳机保留压感");
            passed.Add("模拟输入：锁与双手接物保留轻压值，不钳高到键盘压力");

            InteractionAssert(IncidentRoll(2, "study-search") < .7f && IncidentRoll(11, "study-search") > .7f, "原网页seed固定70%边界");
            g = InteractionFixture(0); g.seed = 2; var incidentSpot = g.spots.Find(value => value.id == "study-search");
            g.BeginSearch(incidentSpot, 2.2f); InteractionFrame(g, new NightInput());
            InteractionAssert(g.mode.type == "catch" && incidentSpot.incidentUsed, "搜索触发且锁定一次概率");
            g.CancelInteraction(); for (int i = 0; i < 70; i++) InteractionFrame(g, new NightInput());
            g.CancelInteraction(); g.BeginSearch(incidentSpot, 2.2f); InteractionFrame(g, new NightInput());
            InteractionAssert(g.mode.type == "search" && incidentSpot.incidentUsed, "取消并重搜不重抽");
            passed.Add("70%源种子掷骰、意外一次性与取消不重抽");

            string[] ids = { "vase", "kitchen-search", "study-search", "storage-search" };
            foreach (string id in ids)
            {
                g = InteractionFixture(2); g.BeginIncident(id); DriveInteractionRescue(g);
                InteractionAssert(g.lastIncidentResult == "caught" && g.metrics.rescued == 1, "物件救场 " + id);
                InteractionAssert(g.incidentResults.Count == 1 && g.incidentResults[0].success, "持久物件结果 " + id);
                passed.Add("物件 " + id + "：原10秒内输入完成抓稳/三路拦截/归位");
            }
            g = InteractionFixture(0); g.BeginIncident("vase");
            while (g.mode != null && g.mode.type == "catch" && g.rescue.stage == "reach")
                InteractionFrame(g, RescueGuideInput(g));
            for (int i = 0; i < 40 && g.mode.type == "catch"; i++) InteractionFrame(g, new NightInput());
            InteractionAssert(g.lastIncidentResult == "fallen" && g.metrics.failedRescues == 1, "支撑释放导致掉落");
            float noiseBefore = g.noise;
            for (int i = 0; i < 26; i++) InteractionFrame(g, new NightInput());
            InteractionAssert(g.reactionImpact < 0 && g.noise >= noiseBefore, "落地时间触发声音");
            passed.Add("救场释放支撑、掉落结果、落地瞬间声音");

            g = InteractionFixture(0); g.phone = new NightPhone { state = "warning", timer = 6 }; g.TryInteract();
            for (int i = 0; i < 62; i++) InteractionFrame(g, new NightInput { actionHeld = true });
            InteractionAssert(g.phone.state == "silenced", "静止按住1.2秒静音");
            g.phone = new NightPhone { state = "warning", timer = 6, armed = true, hold = .6f }; g.ReleaseInteractionInputs();
            InteractionAssert(g.phone.hold == 0 && !g.phone.armed, "释放来电输入");
            passed.Add("来电：1.2秒静音与暂停释放");

            g = InteractionFixture(0); g.cat.state = "idle"; g.player = g.cat.position + Vector3.forward;
            InteractionAssert(g.PetCat() && g.cat.state == "calm" && g.catMemory.pets == 1, "一次轻抚18秒安抚");
            InteractionAssert(g.TossCatToy() && g.cat.state == "toy" && g.catMemory.hasToy, "玩具安全落点和导航");
            passed.Add("猫：近距离轻抚、玩具落点和绕障路径");

            g = InteractionFixture(0); g.mode = new NightMode { type = "tickle", target = g.TicklePosition() };
            for (int i = 0; i < 390 && g.mode != null; i++) InteractionFrame(g, new NightInput { actionHeld = true });
            InteractionAssert(g.parent.state == "warning" && g.status == "playing", "挠痒升级到起身预警但不直接判负");
            passed.Add("挠痒：缩脚停鼾到正常起身预警，不直接判负");

            g = InteractionFixture(0); g.mode = new NightMode { type = "tickle", target = g.TicklePosition() };
            InteractionFrame(g, new NightInput { analog = true, force = .2f, rightForce = .2f, actionHeld = true });
            InteractionAssert(g.tickleHeat == 0, "手柄只压扳机不自动挠痒");
            InteractionFrame(g, new NightInput { analog = true, force = .2f, rightForce = .2f, actionHeld = true, look = Vector2.right });
            InteractionAssert(g.tickleHeat > 0, "扳机配合右摇杆水平才挠痒");
            passed.Add("模拟挠痒：只压扳机不刺激，须配合右摇杆水平动作");

            g = InteractionFixture(3); g.player = new Vector3(3, 0, 13.05f);
            InteractionFrame(g, new NightInput()); g.TryInteract(); InteractionFrame(g, new NightInput());
            for (int i = 0; i < 200 && g.tutorialStep == 2; i++) InteractionFrame(g, new NightInput { force = .55f });
            InteractionAssert(g.tutorialStep == 3, "教学推门达到小段角度");
            for (int i = 0; i < 35; i++) InteractionFrame(g, new NightInput());
            InteractionFrame(g, new NightInput { reverse = true }); InteractionFrame(g, new NightInput { force = .55f });
            InteractionAssert(g.tutorialStep == 5, "教学抓握推拉松力");
            InteractionFrame(g, new NightInput { reverse = true });
            for (int i = 0; i < 250 && g.mode != null; i++) InteractionFrame(g, new NightInput { force = 1 });
            g.player = new Vector3(3, 0, 10.9f); InteractionFrame(g, new NightInput());
            g.player = new Vector3(2.25f, 0, 8.3f); g.TryInteract(); DriveInteractionRescue(g);
            for (int i = 0; i < 70; i++) InteractionFrame(g, new NightInput());
            InteractionAssert(g.tutorialStep == 8, "教学花瓶托住及轻放");
            g.cat.state = "idle"; g.player = new Vector3(3, 0, 6.3f); g.PetCat(); InteractionFrame(g, new NightInput());
            g.player = new Vector3(3, 0, 2.2f); g.TryInteract(); DriveInteractionRescue(g);
            for (int i = 0; i < 70; i++) InteractionFrame(g, new NightInput());
            InteractionAssert(g.tutorialStep == 10 && g.FinishPractice() && g.status == "won", "11小步到自由练习和结束");
            passed.Add("教程：11小步连续推进、花瓶到铁盒迁移、自由练习结束（站点间定位，非整段行走）");
            return passed;
        }

        static NightGame InteractionFixture(int index)
        {
            var result = new NightGame(index); result.seed = 2; result.Start();
            // 隔离猫主动行为以核验手部机制；猫的独立测试随后恢复真实状态。
            result.cat.state = "calm"; result.cat.timer = 10000;
            return result;
        }
        static void InteractionFrame(NightGame game, NightInput input)
        {
            game.time += .02f * (game.IsCinematic ? .12f : 1); game.realTime += .02f;
            game.TickInteraction(.02f, input);
        }
        static void InteractionAssert(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException("Interaction audit failed: " + message);
        }
        static NightInput RescueGuideInput(NightGame game)
        {
            var r = game.rescue; Vector2 move = Vector2.zero;
            if (r.kind == "pencils")
            {
                foreach (var pencil in r.pencils) if (pencil.state == "rolling") { move.x = Mathf.Clamp((pencil.x - r.handX) / .03f, -1, 1); break; }
            }
            else if (r.stage == "lower")
            {
                float goal = RescueRest(r.kind).x;
                move.x = Mathf.Clamp((goal - r.returnPosition.x) / .04f, -1, 1);
                move.y = r.returnPhase == "place" ? Mathf.Abs(goal - r.returnPosition.x) < .04f ? -1 : 0 : 1;
            }
            else move.x = Mathf.Clamp((r.objectX - r.handX) / .03f, -1, 1);
            return new NightInput { move = move, force = .55f, rightForce = .55f, leftForce = .55f };
        }
        static void DriveInteractionRescue(NightGame game)
        {
            for (int i = 0; i < 550 && game.mode != null && game.mode.type == "catch"; i++) InteractionFrame(game, RescueGuideInput(game));
        }
    }
}
#endif
