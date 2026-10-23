#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort.Editor
{
    // 验证驾驶器只返回正常输入；不会写位置、门角度、设备、锁针、父母状态或分数。
    public sealed class NightPlaythroughPilot
    {
        readonly List<Vector3> path = new List<Vector3>();
        Vector3 destination = new Vector3(-99, 0, -99);
        string previousMode;
        float pathAge;
        bool navPulse;
        public bool waitForFirstInspection;
        bool departureReleased;
        public float waitedSeconds;
        public string decision = "observe";
        public int inputs, pathPlans;
        public NightInput Next(NightGame game, float dt)
        {
            inputs++; var input = new NightInput();
            if (game.status != "playing" || !game.active) return input;
            if (waitForFirstInspection && !departureReleased)
            {
                // 玩家保持在原出生卧室听完整趟检查，等熄灯再出发；世界照常运行。
                // 这不是暂停、跳时或修改 AI，只提交零输入。
                if (game.visits > 0 && game.clock.phaseIndex == 3) { departureReleased = true; path.Clear(); }
                else { waitedSeconds += dt; decision = "listen in bedroom; leave after footsteps return and corridor light switches off"; return input; }
            }
            var mode = game.mode;
            string kind = mode == null ? "walk" : mode.type;
            if (previousMode != kind) { path.Clear(); pathAge = 0; previousMode = kind; navPulse = false; }
            if (mode != null)
            {
                decision = mode.type;
                if (mode.type == "door")
                {
                    var door = game.doors[mode.index];
                    if (door.direction < 0) { input.reverse = true; return input; }
                    // 较快越过涩点，在终点之前真实收力，避免撞框。
                    input.force = input.rightForce = door.progress < .90f ? .92f : .30f;
                    return input;
                }
                if (mode.type == "step")
                {
                    float pointer = .5f + .48f * Mathf.Sin((game.time + dt) * 3.8f);
                    input.confirm = Mathf.Abs(pointer - .5f) < game.level.stepWidth * .38f; return input;
                }
                if (mode.type == "lockpick") return LockInput(game);
                if (mode.type == "catch") return RescueInput(game);
                return input;
            }
            if (game.PhonePending())
            {
                decision = "stop and silence phone"; input.actionPressed = !game.phone.armed; input.muteHeld = input.actionHeld = true; return input;
            }
            if (game.cat != null && game.cat.state == "prepare") { input.toyPressed = true; decision = "distract cat with toy"; return input; }
            var wanted = game.spots[Mathf.Clamp(game.level.device, 0, game.spots.Count - 1)];
            if (!game.hasDevice && game.SpotNear() == wanted)
            {
                decision = "inspect " + wanted.id; input.actionPressed = true; return input;
            }
            Vector3 goal = game.hasDevice ? game.level.home : wanted.Position;
            decision = game.hasDevice ? "return to bedroom" : "approach " + wanted.id;
            // 靠近睡房时自然切换蹲行；这是输入策略，不修改噪声阈值。
            bool crouch = NightGeometry.Distance(game.player, game.level.parentHome) < 4.2f;
            input.crouchPressed = crouch != game.crouched;
            bool changed = NightGeometry.Distance(goal, destination) > .01f;
            pathAge += dt;
            if (changed || path.Count == 0 || pathAge > 2.5f)
            {
                path.Clear(); path.AddRange(game.FindPath(game.player, goal, false, .2f)); destination = goal; pathAge = 0; pathPlans++;
            }
            while (path.Count > 0 && NightGeometry.Distance(game.player, path[0]) < .16f) path.RemoveAt(0);
            if (path.Count == 0) return input;
            Vector3 target = path[0];
            foreach (var door in game.doors)
            {
                if (door.progress >= .999f || NightGeometry.Distance(game.player, door.Position) >= 1.16f) continue;
                bool crosses = (game.player.z - door.z) * (target.z - door.z) <= 0 || NightGeometry.Distance(target, door.Position) < .56f;
                if (crosses && game.DoorNear() == door) { decision = "grasp " + door.name; input.actionPressed = true; input.move = Vector2.zero; return input; }
            }
            Vector3 delta = target - game.player;
            input.move = Vector2.ClampMagnitude(new Vector2(delta.x, delta.z) * 5, 1);
            return input;
        }
        NightInput LockInput(NightGame game)
        {
            var input = new NightInput(); var mechanism = game.lockState;
            if (mechanism == null || !mechanism.armed) return input;
            var pin = mechanism.pins[mechanism.selected];
            if (pin.seated)
            {
                bool all = true; foreach (var other in mechanism.pins) if (!other.seated) { all = false; break; }
                if (all) return input;
                navPulse = !navPulse; if (navPulse) input.move.x = 1; return input;
            }
            navPulse = false;
            if (pin.lift < pin.target - .01f)
            {
                input.force = input.rightForce = .55f;
                if (pin.spool && !pin.cleared && pin.lift >= pin.target * .58f - .015f) input.leftForce = .55f;
            }
            return input;
        }
        NightInput RescueInput(NightGame game)
        {
            var input = new NightInput(); var r = game.rescue; if (r == null) return input;
            input.force = input.rightForce = .55f; input.leftForce = .55f;
            if (r.kind == "pencils")
            {
                foreach (var pencil in r.pencils) if (pencil.state == "rolling") { input.move.x = Mathf.Clamp((pencil.x - r.handX) * 12, -1, 1); break; }
            }
            else if (r.stage == "reach") input.move.x = Mathf.Clamp((r.objectX - r.handX) * 12, -1, 1);
            else if (r.stage == "lower")
            {
                input.move.x = Mathf.Clamp(-r.returnPosition.x * 12, -1, 1);
                input.move.y = r.returnPhase == "place" ? Mathf.Abs(r.returnPosition.x) < .05f ? -1 : 0 : 1;
            }
            return input;
        }
    }

}
#endif
