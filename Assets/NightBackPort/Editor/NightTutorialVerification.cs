using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace NightBackPort.Editor
{
    // 仅提交与玩家相同的 NightInput。关卡初始化以后不写任何 NightGame 状态。
    public sealed class NightTutorialPilot
    {
        readonly List<Vector3> path = new List<Vector3>();
        Vector3 destination = new Vector3(-99, 0, -99);
        string previousMode;
        float pathAge;
        readonly bool analog;
        public string decision = "observe";
        public int inputs, pathPlans;

        public NightTutorialPilot(bool analogInput = false) { analog = analogInput; }

        public NightInput Next(NightGame game, float dt)
        {
            inputs++;
            var input = new NightInput();
            if (!game.active || game.status != "playing" || !game.IsTutorial) return input;
            string kind = game.mode == null ? "walk" : game.mode.type;
            if (previousMode != kind) { path.Clear(); pathAge = 0; previousMode = kind; }
            if (game.mode != null)
            {
                decision = "practice " + game.mode.type;
                if (kind == "door")
                {
                    var door = game.doors[game.mode.index];
                    if (game.tutorialStep == 3) { decision = "release all pressure; keep contact"; return input; }
                    bool close = game.tutorialStep == 4;
                    if (door.direction != (close ? -1 : 1)) { input.reverse = true; decision = "reverse push/pull direction"; return input; }
                    decision = close ? "gently pull door back" : "push door; ease pressure near end";
                    if (analog) { input.analog = true; input.force = input.rightForce = door.progress < .88f ? .4f : .22f; }
                    else input.actionHeld = true;
                    return input;
                }
                if (kind == "catch") return RescueInput(game);
                if (kind == "step")
                {
                    float pointer = .5f + .48f * Mathf.Sin((game.time + dt) * 3.8f);
                    input.confirm = Mathf.Abs(pointer - .5f) < game.level.stepWidth * .38f;
                    return input;
                }
                // 回放、过渡等状态交还游戏自然推进；不跳过反应时长。
                return input;
            }
            if (game.tutorialStep <= 4)
            {
                if (game.tutorialStep > 0 && game.DoorNear() != null) { decision = "grasp door handle"; input.actionPressed = true; return input; }
                return Walk(game, new Vector3(3, 0, 13.05f), dt, "approach handle");
            }
            if (game.tutorialStep == 5)
            {
                if (game.doors[0].progress < .999f && game.DoorNear() != null) { decision = "grasp door to leave enough room"; input.actionPressed = true; return input; }
                return Walk(game, new Vector3(3, 0, 10.5f), dt, "walk through open doorway");
            }
            if (game.tutorialStep == 6 || game.tutorialStep == 7)
            {
                var target = new Vector3(2.25f, 0, 8.3f);
                if (NightGeometry.Distance(game.player, target) < .95f) { decision = "reach for vase at practice station"; input.actionPressed = true; return input; }
                return Walk(game, target, dt, "walk to vase station");
            }
            if (game.tutorialStep == 8)
            {
                if (game.CatNear()) { decision = "pet cat once"; input.actionPressed = true; return input; }
                return Walk(game, new Vector3(3, 0, 6.5f), dt, "walk to cat");
            }
            if (game.tutorialStep == 9)
            {
                var target = new Vector3(3, 0, 2.2f);
                if (NightGeometry.Distance(game.player, target) < .95f) { decision = "reach for tin at practice station"; input.actionPressed = true; return input; }
                return Walk(game, target, dt, "walk to tin station");
            }
            decision = "press normal confirm to finish free practice";
            input.confirm = true;
            return input;
        }

        NightInput Walk(NightGame game, Vector3 goal, float dt, string description)
        {
            decision = description;
            var input = new NightInput();
            pathAge += dt;
            if (NightGeometry.Distance(goal, destination) > .01f || path.Count == 0 || pathAge > 2.5f)
            {
                path.Clear(); path.AddRange(game.FindPath(game.player, goal, true, .2f));
                destination = goal; pathAge = 0; pathPlans++;
            }
            while (path.Count > 0 && NightGeometry.Distance(game.player, path[0]) < .12f) path.RemoveAt(0);
            if (path.Count > 0)
            {
                Vector3 delta = path[0] - game.player;
                input.move = Vector2.ClampMagnitude(new Vector2(delta.x, delta.z) * 5, 1);
            }
            return input;
        }

        NightInput RescueInput(NightGame game)
        {
            var input = new NightInput(); var rescue = game.rescue;
            if (rescue == null) return input;
            input.leftForce = .55f;
            if (analog) { input.analog = true; input.force = input.rightForce = .55f; }
            else input.actionHeld = true;
            decision = "support " + rescue.kind + " / " + rescue.stage + " / " + rescue.returnPhase;
            if (rescue.stage == "reach") input.move.x = Mathf.Clamp((rescue.objectX - rescue.handX) * 12, -1, 1);
            else if (rescue.stage == "lower")
            {
                input.move.x = Mathf.Clamp(-rescue.returnPosition.x * 12, -1, 1);
                input.move.y = rescue.returnPhase == "place" ? Mathf.Abs(rescue.returnPosition.x) < .04f ? -1 : 0 : 1;
            }
            return input;
        }
    }

    public static class NightTutorialVerification
    {
        [Serializable] public class Sample
        {
            public int tutorialStep; public float realTime, worldTime, doorProgress;
            public string mode, phase, decision, title, hint, eventText;
            public Vector3 player; public Vector2 move; public float force, leftForce;
            public bool actionPressed, actionHeld, reverse, confirm;
        }
        [Serializable] public class RunResult
        {
            public string inputMode, status, reason, finalMode;
            public bool passed, won; public uint seed;
            public int finalStep, inputs, pathPlans, rescued, failedRescues, doorBumps;
            public float realTime, worldTime; public Vector3 finalPlayer;
            public List<int> visitedSteps = new List<int>();
            public List<Sample> samples = new List<Sample>();
        }
        [Serializable] public class Report
        {
            public string timestamp, scope; public int passed, failed;
            public List<RunResult> runs = new List<RunResult>();
        }
        [MenuItem("Night Back Port/Verify Full Tutorial Input Playthrough")]
        public static void RunMenu() { Debug.Log(Run()); }
        public static string Run()
        {
            var report = new Report {
                timestamp = DateTime.Now.ToString("o"),
                scope = "11 步教学从出生点到完成页的正常 NightGame.Tick / NightInput 全程验证。键鼠与模拟标准手柄各一遍；固定种子仅开局设置。只以正常移动、交互、反向、松力、双手支撑、归位、摸猫、确认完成，不写位置、门、进度或难度，不重置站点，不跳过回放。导航和动作策略知道关卡数据，因此不代表盲玩、真人教学理解、渲染或实体硬件实测。360 秒仅为验证器防卡上限。"
            };
            foreach (bool analog in new[] { false, true })
            {
                var result = new RunResult { inputMode = analog ? "simulated-standard-controller" : "keyboard-mouse", seed = 2 };
                report.runs.Add(result);
                try
                {
                    var game = new NightGame(3); game.seed = result.seed; game.Start();
                    var pilot = new NightTutorialPilot(analog);
                    result.visitedSteps.Add(game.tutorialStep);
                    int oldStep = -1; string oldMode = ""; float nextSample = 0;
                    for (int frame = 0; frame < 18000 && game.status == "playing"; frame++)
                    {
                        NightInput input = pilot.Next(game, .02f); game.Tick(.02f, input);
                        if (!result.visitedSteps.Contains(game.tutorialStep)) result.visitedSteps.Add(game.tutorialStep);
                        string mode = game.mode == null ? "walk" : game.mode.type;
                        if (game.realTime >= nextSample || oldStep != game.tutorialStep || oldMode != mode || game.status != "playing")
                        {
                            result.samples.Add(new Sample {
                                tutorialStep = game.tutorialStep, realTime = game.realTime, worldTime = game.time,
                                doorProgress = game.doors[0].progress, mode = mode, phase = game.mode == null ? "" : game.mode.phase,
                                decision = pilot.decision, title = game.TutorialTitle(), hint = game.TutorialHint(), eventText = game.toast,
                                player = game.player, move = input.move, force = input.force, leftForce = input.leftForce,
                                actionPressed = input.actionPressed, actionHeld = input.actionHeld, reverse = input.reverse, confirm = input.confirm
                            });
                            nextSample = game.realTime + 1;
                        }
                        oldStep = game.tutorialStep; oldMode = mode;
                    }
                    result.won = game.status == "won"; result.status = game.status; result.finalStep = game.tutorialStep;
                    result.passed = result.won && result.visitedSteps.Count == NightGame.TutorialStepCount && game.metrics.rescued >= 2 && game.catMemory.pets > 0;
                    result.realTime = game.realTime; result.worldTime = game.time; result.inputs = pilot.inputs; result.pathPlans = pilot.pathPlans;
                    result.finalMode = game.mode == null ? "walk" : game.mode.type; result.finalPlayer = game.player;
                    result.rescued = game.metrics.rescued; result.failedRescues = game.metrics.failedRescues; result.doorBumps = game.metrics.doorBumps;
                    result.reason = game.status == "playing" ? "到达验证器 360 秒上限；游戏本身没有超时判负。最后动作：" + pilot.decision : game.toast;
                }
                catch (Exception exception) { result.reason = exception.ToString(); result.status = "exception"; }
                if (result.passed) report.passed++; else report.failed++;
            }
            string directory = Path.Combine(Application.dataPath, "../NightBackEvidence"); Directory.CreateDirectory(directory);
            string json = JsonUtility.ToJson(report, true);
            File.WriteAllText(Path.Combine(directory, "tutorial-playthrough-checks.json"), json);
            return json;
        }
    }
}
