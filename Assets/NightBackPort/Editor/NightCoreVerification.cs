using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightBackPort.Editor
{
    public static class NightCoreVerification
    {
        [Serializable] public class Check { public string name, detail; public bool passed; }
        [Serializable] public class Report { public string scope, timestamp; public int passed, failed; public List<Check> checks = new List<Check>(); }
        static void CheckThat(Report report, string name, bool passed, string detail = "")
        { report.checks.Add(new Check { name = name, passed = passed, detail = detail }); if (passed) report.passed++; else report.failed++; }
        static void Simulate(NightGame game, float duration)
        {
            int frames = Mathf.CeilToInt(duration / .02f); for (int frame = 0; frame < frames && game.status == "playing"; frame++) game.Tick(.02f, new NightInput());
        }
        [MenuItem("Night Back Port/Verify Core Simulation")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            var report = new Report { scope = "真实 Unity 编辑器内的代码模拟；不是人工整局通关、实体手柄或耳机主观试听。", timestamp = DateTime.Now.ToString("o") };
            try
            {
                for (int index = 0; index < 4; index++)
                {
                    var game = new NightGame(index);
                    CheckThat(report, "关卡" + index + " 出生点无碰撞", game.CanOccupy(game.player));
                    CheckThat(report, "关卡" + index + " 父母起点无碰撞", game.CanOccupy(game.parent.position, .24f));
                    CheckThat(report, "关卡" + index + " 墙体阻挡角色", !game.CanOccupy(new Vector3(0, 0, 3)));
                    if (index == 3) continue;
                    foreach (var door in game.doors) door.progress = 1;
                    game.clock.elapsedSeconds = 180;
                    var origin = game.parent.position;
                    foreach (var target in game.ScheduledRoute())
                    {
                        var route = game.FindPath(origin, target, false, .24f); var last = route.Count > 0 ? route[route.Count - 1] : origin;
                        bool clear = true; foreach (var point in route) if (!game.CanOccupy(point, .24f)) { clear = false; break; }
                        CheckThat(report, "关卡" + index + " 作息路线 " + target, NightGeometry.Distance(last, target) < .15f && clear, "nodes=" + route.Count + ", end=" + last);
                        origin = last;
                    }
                    var homeRoute = game.FindPath(origin, game.level.parentHome, false, .24f);
                    CheckThat(report, "关卡" + index + " 作息可实际走回床边", homeRoute.Count > 0 && NightGeometry.Distance(homeRoute[homeRoute.Count - 1], game.level.parentHome) < .15f);
                    foreach (var spot in game.spots)
                    {
                        var route = game.FindPath(game.player, spot.Position, true, .2f); var last = route.Count > 0 ? route[route.Count - 1] : game.player;
                        CheckThat(report, "关卡" + index + " 搜索点可抵达 " + spot.id, NightGeometry.Distance(last, spot.Position) < 1.2f && !game.Occluded(last, spot.Position, true, spot.id), "approach=" + last);
                    }
                }
                var doorGame = new NightGame();
                CheckThat(report, "门完整扫掠挡住扫掠区内角色", doorGame.DoorSweepHits(doorGame.doors[0], 0, 1, new Vector3(3, 0, 9.7f), .2f));
                CheckThat(report, "门扫掠不挡远处角色", !doorGame.DoorSweepHits(doorGame.doors[0], 0, 1, new Vector3(3, 0, 11.5f), .2f));
                CheckThat(report, "关闭门实物阻挡", !doorGame.CanOccupy(new Vector3(3, 0, 10)));
                doorGame.doors[0].progress = 1;
                CheckThat(report, "门打开后门洞可通行", doorGame.CanOccupy(new Vector3(3, 0, 10)));
                CheckThat(report, "蹲下受沙发遮挡", doorGame.Occluded(new Vector3(1.4f, 0, 1.1f), new Vector3(1.4f, 0, 4.3f), true));
                CheckThat(report, "站起不能靠低沙发隐身", !doorGame.Occluded(new Vector3(1.4f, 0, 1.1f), new Vector3(1.4f, 0, 4.3f), false));
                var sight = new NightGame(); sight.Start(); sight.parent.position = new Vector3(11, 0, 9); sight.player = new Vector3(11, 0, 8);
                sight.parent.state = "checking"; sight.parent.intent = "investigate"; sight.parent.timer = 999; sight.parent.route.Clear(); sight.parent.heading = Mathf.PI;
                for (int i = 0; i < 12; i++) sight.Tick(.02f, new NightInput());
                CheckThat(report, "无遮挡 0.24 秒尚未识别", sight.status == "playing", sight.parent.recognition.ToString("F4"));
                sight.Tick(.02f, new NightInput()); CheckThat(report, "无遮挡超过 0.25 秒判负", sight.status == "lost", sight.parent.recognition.ToString("F4"));
                var noise = new NightGame(); noise.Start(); noise.MakeNoise(1000, "验证噪声", "floorCreak", noise.level.parentBed);
                CheckThat(report, "巨大声音不直接判负", noise.status == "playing"); noise.Tick(.02f, new NightInput());
                CheckThat(report, "巨大声音先给起身预警", noise.parent.state == "warning" && noise.parent.timer > 4.9f, noise.parent.state);
                var win = new NightGame(); win.Start(); win.Tick(.02f, new NightInput()); CheckThat(report, "空手回卧室不会胜利", win.status == "playing");
                win.hasDevice = true; win.Tick(.02f, new NightInput()); CheckThat(report, "取物返程获胜", win.status == "won" && win.metrics.score >= 40);
                var clock = new NightClock(0); CheckThat(report, "22:30 出发", clock.displayTime == "22:30" && clock.phaseIndex == 0);
                clock = new NightClock(90); CheckThat(report, "22:45 洗衣出发", clock.displayTime == "22:45" && clock.washerActive);
                clock = new NightClock(180); CheckThat(report, "23:00 喝水出发", clock.displayTime == "23:00" && clock.phaseIndex == 2);
                clock = new NightClock(359.99f); clock.Tick(.02f); CheckThat(report, "跨小时重新循环", clock.phaseIndex == 0 && clock.cycle == 1 && clock.displayTime == "23:30");
                var paused = new NightGame(); paused.Start(); paused.active = false; paused.Tick(10, new NightInput()); CheckThat(report, "暂停不推进世界或时钟", paused.time == 0 && paused.clock.elapsedSeconds == 0);
                var safe = new NightGame(0, 90); safe.Start(); Simulate(safe, 96);
                CheckThat(report, "安全卧室等候跨洗衣结束仍可游玩", safe.status == "playing", "clock=" + safe.clock.displayTime + ", parent=" + safe.parent.state);
                var early = new NightGame(2, 0); early.Start(); Simulate(early, 31);
                CheckThat(report, "第三夜首次短巡查有明确时点", early.parent.state == "sleep"); Simulate(early, 1.1f);
                CheckThat(report, "第三夜时点先给预警", early.parent.state == "warning" && early.parent.timer > 4.5f);
                var later = new NightGame(2, 90); later.Start(); Simulate(later, 33);
                CheckThat(report, "22:45 出发有完整洗衣窗口不叠加旧32秒检查", later.parent.state == "sleep" && later.clock.washerActive);
                var immediate = new NightGame(1, 180); immediate.Start(); immediate.Tick(.02f, new NightInput());
                CheckThat(report, "23:00 出发先预警而非立即抓到", immediate.parent.state == "warning" && immediate.status == "playing" && immediate.parent.timer > 4.9f);
                var patrol = new NightGame(1); patrol.Start(); patrol.player = new Vector3(17, 0, 16); patrol.parent.position = patrol.level.home; patrol.parent.state = "checking"; patrol.parent.intent = "investigate"; patrol.SetDestination(new Vector3(3, 0, 9));
                bool collided = false, crossed = false;
                for (int i = 0; i < 600 && patrol.status == "playing"; i++) { patrol.Tick(.02f, new NightInput()); if (!patrol.CanOccupy(patrol.parent.position, .24f)) collided = true; if (patrol.parent.position.z < 9.4f) crossed = true; }
                CheckThat(report, "父母实际开门通行且无门/墙穿模", crossed && !collided && patrol.doors[0].progress >= .999f, "door=" + patrol.doors[0].progress + ", parent=" + patrol.parent.position);
                var restore = new NightGame(2, 90); restore.Start(); restore.doors[0].progress = .6f; restore.hasDevice = true; restore.spots[0].incidentUsed = true; restore.metrics.loudSounds = 3;
                uint seed = restore.seed; string saved = JsonUtility.ToJson(restore); var loaded = JsonUtility.FromJson<NightGame>(saved); bool valid = loaded.RepairAfterLoad();
                CheckThat(report, "续玩保留种子/半开门/取物/已判定事件", valid && loaded.seed == seed && loaded.hasDevice && loaded.spots[0].incidentUsed && loaded.metrics.loudSounds == 3 && Mathf.Abs(loaded.doors[0].progress - .6f) < .0001f);
                CheckThat(report, "续玩释放持续施力并等待恢复", !loaded.active && loaded.velocity == Vector3.zero && loaded.interactionNeedsNeutral);
                var walkingSave = new NightGame(0, 90); walkingSave.Start();
                var walkingLoad = JsonUtility.FromJson<NightGame>(JsonUtility.ToJson(walkingSave)); bool walkingValid = walkingLoad.RepairAfterLoad();
                CheckThat(report, "存档空占位对象恢复为无交互", walkingValid && walkingLoad.mode == null && walkingLoad.lockState == null && walkingLoad.rescue == null && walkingLoad.locks.Count == 0);
                walkingLoad.Start(); var walkingStart = walkingLoad.player;
                for (int i = 0; i < 20; i++) walkingLoad.Tick(.02f, new NightInput { move = new Vector2(0, -1) });
                CheckThat(report, "自由移动存档续玩后正常输入可移动", walkingLoad.status == "playing" && walkingLoad.mode == null && NightGeometry.Distance(walkingStart, walkingLoad.player) > .15f, "distance=" + NightGeometry.Distance(walkingStart, walkingLoad.player));
                var liveLock = new NightGame(); liveLock.Start(); liveLock.player = new Vector3(16, 0, 2);
                liveLock.Tick(.02f, new NightInput { actionPressed = true }); var lockPilot = new NightPlaythroughPilot();
                for (int i = 0; i < 500 && liveLock.mode != null && liveLock.mode.type == "lockpick" && CountPins(liveLock) < 1; i++) liveLock.Tick(.02f, lockPilot.Next(liveLock, .02f));
                var resumedLock = JsonUtility.FromJson<NightGame>(JsonUtility.ToJson(liveLock)); bool validLock = resumedLock.RepairAfterLoad(); resumedLock.Start();
                resumedLock.Tick(.02f, new NightInput()); lockPilot = new NightPlaythroughPilot();
                for (int i = 0; i < 500 && resumedLock.mode != null && resumedLock.mode.type == "lockpick" && CountPins(resumedLock) < 2; i++) resumedLock.Tick(.02f, lockPilot.Next(resumedLock, .02f));
                int savedPins = CountPins(resumedLock); resumedLock.CancelInteraction(); resumedLock.TryInteract();
                CheckThat(report, "续玩新卡弹子在收手重抓后保留", validLock && savedPins >= 2 && CountPins(resumedLock) == savedPins, "pins=" + savedPins + " -> " + CountPins(resumedLock));
                var acoustic = new NightGame(0); acoustic.time = 2; acoustic.lastSnore = -9;
                float[] progress = { 0, .5f, 1 }, expectedGain = { .07482145f, .16485450f, .43210402f };
                for (int i = 0; i < progress.Length; i++)
                {
                    acoustic.doors[0].progress = progress[i]; var path = acoustic.SoundPath("reference", new Vector3(3, 1.1f, 9), new Vector3(3, 1.35f, 11.3f));
                    CheckThat(report, "三频段声场对照网页 门角度" + progress[i], Mathf.Abs(path.gain - expectedGain[i]) < .00015f, "native=" + path.gain.ToString("F7") + ", web=" + expectedGain[i].ToString("F7") + ", route=" + path.route);
                }
                float heard = acoustic.HearingAt(50, "floorCreak", new Vector3(3, 0, 9)); CheckThat(report, "父母听觉对照网页", Mathf.Abs(heard - 7.5916726f) < .003f, heard.ToString("F6"));
                var masked = new NightGame(1); masked.time = 9; masked.lastSnore = -9; masked.clock.elapsedSeconds = 92;
                Vector3[] positions = { new Vector3(11, 0, 16), new Vector3(11, 0, 13), new Vector3(3, 0, 9), new Vector3(16, 0, 3) };
                bool[] expectedMask = { true, true, false, false }; float[] expectedHearing = { .48220186f, .69952586f, 3.06928496f, .23030074f };
                for (int i = 0; i < positions.Length; i++)
                {
                    masked.player = positions[i]; float received = masked.HearingAt(25, "floorSoft", masked.player);
                    CheckThat(report, "空间掩护对照网页 " + positions[i], masked.MaskAt(masked.player) == expectedMask[i] && Mathf.Abs(received - expectedHearing[i]) < .003f, "heard=" + received.ToString("F6") + ", mask=" + masked.MaskAt(masked.player));
                }
            }
            catch (Exception exception) { CheckThat(report, "验证执行无异常", false, exception.ToString()); }
            string directory = Path.Combine(Application.dataPath, "../NightBackEvidence"); Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(report, true); File.WriteAllText(Path.Combine(directory, "core-checks.json"), json); return json;
        }
        static int CountPins(NightGame game)
        {
            int count = 0; if (game.lockState != null && game.lockState.pins != null) foreach (var pin in game.lockState.pins) if (pin.seated) count++; return count;
        }
    }
}
