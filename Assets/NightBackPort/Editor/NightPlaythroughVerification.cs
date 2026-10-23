using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightBackPort.Editor
{
    public static class NightPlaythroughVerification
    {
        [Serializable] public class Sample
        {
            public float realTime, worldTime, parentAlert, recognition;
            public string clock, mode, parentState, parentIntent, decision, eventText;
            public Vector3 player, parent; public bool hasDevice; public float force; public Vector2 move;
        }
        [Serializable] public class RunResult
        {
            public int night, inputs, pathPlans, goodSteps, totalSteps, searches, rescued, failedRescues, score, parentWarnings;
            public uint seed; public float departure, realTime, worldTime, maximumAlert, waitedSeconds; public bool won, inspectionReturned, timePhaseChanged;
            public string status, grade, finalMode, reason; public Vector3 finalPlayer;
            public List<Sample> samples = new List<Sample>();
        }
        [Serializable] public class Report
        {
            public string scope, timestamp; public int passed, failed; public List<RunResult> runs = new List<RunResult>();
        }
        [MenuItem("Night Back Port/Verify Three Night Input Playthroughs")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            return RunSet(90, false, 11, "playthrough-checks.json");
        }
        [MenuItem("Night Back Port/Verify 2300 Wait And Dark Return Playthroughs")]
        public static void RunTimedMenu() { Debug.Log(RunTimed()); }
        public static string RunTimed()
        {
            return RunSet(180, true, 4, "timed-playthrough-checks.json");
        }
        static string RunSet(float departure, bool waitForInspection, uint seedValue, string filename)
        {
            var report = new Report
            {
                timestamp = DateTime.Now.ToString("o"),
                scope = "真实 NightGame / NightInput 程序输入通关。正常菜单偏移=" + departure + "，固定seed=" + seedValue + (waitForInspection ? "；23:00起步，在出生卧室真实等候完整巡查及熄灯，随后出发。种子4让第一夜笔筒、第二夜铁盒均有真实救场。" : "；22:45洗衣时段出发。") + "按当前固定藏点的已知攻略路线测试，不声称盲玩、真人体验或实体手柄测试。只在开局赋seed，随后不改位置、门状态、设备、家长、锁或难度。"
            };
            for (int index = 0; index < 3; index++)
            {
                var result = new RunResult { night = index + 1, seed = seedValue, departure = departure }; report.runs.Add(result);
                try
                {
                    var game = new NightGame(index, result.departure); game.seed = result.seed; game.Start();
                    var pilot = new NightPlaythroughPilot { waitForFirstInspection = waitForInspection }; string oldMode = "", oldParent = ""; float nextSample = 0;
                    int initialPhase = game.clock.phaseIndex;
                    for (int frame = 0; frame < 18000 && game.status == "playing"; frame++)
                    {
                        var input = pilot.Next(game, .02f); game.Tick(.02f, input);
                        if (game.clock.phaseIndex != initialPhase) result.timePhaseChanged = true;
                        result.maximumAlert = Mathf.Max(result.maximumAlert, game.parent.alert);
                        string mode = game.mode == null ? "walk" : game.mode.type;
                        if (game.parent.state == "warning" && oldParent != "warning") result.parentWarnings++;
                        if (game.realTime >= nextSample || oldMode != mode || oldParent != game.parent.state || game.status != "playing")
                        {
                            result.samples.Add(new Sample { realTime = game.realTime, worldTime = game.time, parentAlert = game.parent.alert, recognition = game.parent.recognition, clock = game.clock.displayTime, mode = mode, parentState = game.parent.state, parentIntent = game.parent.intent, decision = pilot.decision, player = game.player, parent = game.parent.position, hasDevice = game.hasDevice, force = input.force, move = input.move, eventText = game.toast }); nextSample = game.realTime + 1;
                        }
                        oldMode = mode; oldParent = game.parent.state; game.events.Clear();
                    }
                    result.won = game.status == "won"; result.status = game.status; result.grade = game.metrics.grade; result.score = game.metrics.score;
                    result.realTime = game.realTime; result.worldTime = game.time; result.inputs = pilot.inputs; result.pathPlans = pilot.pathPlans;
                    result.waitedSeconds = pilot.waitedSeconds; result.inspectionReturned = game.visits > 0;
                    result.finalMode = game.mode == null ? "walk" : game.mode.type; result.reason = game.status == "playing" ? "到达验证器 360 秒上限；游戏本身没有超时判负。" : game.toast;
                    result.finalPlayer = game.player; result.goodSteps = game.metrics.goodSteps; result.totalSteps = game.metrics.steps; result.searches = game.metrics.searches; result.rescued = game.metrics.rescued; result.failedRescues = game.metrics.failedRescues;
                }
                catch (Exception exception) { result.reason = exception.ToString(); result.status = "exception"; }
                if (result.won && (!waitForInspection || result.inspectionReturned && result.timePhaseChanged && result.parentWarnings > 0)) report.passed++; else report.failed++;
            }
            string directory = Path.Combine(Application.dataPath, "../NightBackEvidence"); Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(report, true); File.WriteAllText(Path.Combine(directory, filename), json); return json;
        }
    }
}
