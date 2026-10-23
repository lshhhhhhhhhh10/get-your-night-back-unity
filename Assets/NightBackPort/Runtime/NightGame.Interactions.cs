// @role Resolves doors, locks, searches, rescue, cats and phone
// @calls NightGame_Core NightAcoustics NightData

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    [Serializable] public class NightPin
    {
        public float target, lift;
        public bool seated, spool, cleared;
    }

    [Serializable] public class NightLock
    {
        public string id, feedback = "lift";
        public int level, selected, navigation;
        public NightPin[] pins;
        public float pressure, velocity, stress, cooldown, turn;
        public bool held, armed;
    }

    [Serializable] public class NightPencil
    {
        public float x, at, resolvedAt;
        public string state = "rolling";
    }

    [Serializable] public class NightRescue
    {
        public string kind, stage, returnPhase = "lift";
        public float handX, objectX, height = 1.3f, tilt, left, right, steady, release;
        public float grace = .65f, lidNoise, contactAt = -1, returnProgress, returnHeight, returnTilt;
        public float sourceElapsed;
        public bool clink, success;
        public Vector3 returnFrom, returnPosition;
        public NightPencil[] pencils;
    }

    [Serializable] public class NightLure
    {
        public string id, name, kind, furniture;
        public Vector3 position;
        public float timer;
        public int pulses, minLevel;
        public bool armed, done;
    }

    [Serializable] public class NightIncidentResult
    {
        public string id, kind;
        public bool success;
        public float sourceElapsed;
        public NightRescue rescue;
    }

    [Serializable] public class NightCatMemory
    {
        public List<Vector3> route = new List<Vector3>();
        public float repath, still, cooldown, toyCooldown, toyAge, lastVoice = -20;
        public int pets, distractions;
        public bool hasToy;
        public Vector3 toy, toyFrom;
    }

    // 交互状态独立序列化。暂停只是释放输入，不会把一根弹子误判为已经卡住。
    public partial class NightGame
    {
        public NightCat cat;
        public NightCatMemory catMemory;
        public NightPhone phone;
        public NightRescue rescue;
        public NightLock lockState;
        public List<NightLock> locks = new List<NightLock>();
        public List<NightLure> lures = new List<NightLure>();
        public List<NightIncidentResult> incidentResults = new List<NightIncidentResult>();
        public string vaseState = "stable", lastIncidentId, lastIncidentResult;
        public bool vasePracticed, tinPracticed;
        public float tickleHeat, rescueAspect = 1.77778f;
        public float doorNoiseTime, doorNoiseEnergy, interactionSoundAt;
        public int floorX, floorZ, resumeSearchIndex = -1, phoneCue = -1;
        public float resumeSearchElapsed, reactionImpact = -1, reactionNoise;
        public string reactionSound, reactionText;
        public Vector3 transitFrom;
        public bool interactionNeedsNeutral;

        static float FlatDistance(Vector3 a, Vector3 b)
        {
            float x = a.x - b.x, z = a.z - b.z;
            return Mathf.Sqrt(x * x + z * z);
        }

        public void ResetInteractions()
        {
            cat = new NightCat { position = levelIndex == 3 ? new Vector3(3, 0, 5.3f) : new Vector3(3, 0, 5.5f), met = levelIndex == 3, jumpUsed = levelIndex == 3 };
            catMemory = new NightCatMemory();
            phone = new NightPhone();
            locks = new List<NightLock>();
            incidentResults = new List<NightIncidentResult>();
            lures = new List<NightLure>();
            if (levelIndex != 3)
            {
                lures.Add(new NightLure { id = "radio", name = "客厅收音机", kind = "radio", furniture = "living-tv", position = new Vector3(4.08f, 0, 2.2f) });
                lures.Add(new NightLure { id = "toy", name = "走廊玩具鸭", kind = "toy", furniture = "hall-chair", position = new Vector3(1.25f, 0, 7) });
                if (levelIndex >= 1) lures.Add(new NightLure { id = "back-radio", name = "后廊收音机", kind = "radio", furniture = "back-console", position = new Vector3(21, 0, 17.15f), minLevel = 1 });
            }
            vaseState = "stable";
            floorX = Mathf.RoundToInt(player.x); floorZ = Mathf.RoundToInt(player.z);
            resumeSearchIndex = -1; reactionImpact = -1; phoneCue = -1;
            ResetTutorial();
        }

        public void ReleaseInteractionInputs()
        {
            interactionNeedsNeutral = true;
            if (phone != null) { phone.armed = false; phone.hold = 0; }
            if (lockState != null)
            {
                NightLock saved = locks.Find(item => item.id == lockState.id);
                if (saved != null) lockState = saved;
                ReleaseLock(lockState);
            }
            if (rescue != null) { rescue.left = 0; rescue.right = 0; rescue.release = 0; rescue.grace = .65f; }
            if (doors != null) foreach (var door in doors) { door.force = 0; door.speed = 0; }
            if (mode != null) mode.force = 0;
            doorNoiseTime = 0; doorNoiseEnergy = 0;
        }

        static bool NeutralInput(NightInput input)
        {
            return !input.actionHeld && !input.muteHeld && input.force <= .035f && input.leftForce <= .035f && input.rightForce <= .035f;
        }

        static float RightPressure(NightInput input, float keyboardPressure)
        {
            float analog = Mathf.Clamp01(Mathf.Max(input.force, input.rightForce));
            return analog > 0 ? analog : input.actionHeld ? keyboardPressure : 0;
        }

        public void TickInteraction(float realDt, NightInput input)
        {
            if (!active || status != "playing") return;
            float dt = Mathf.Clamp(realDt, 0, .06f);
            float worldDt = dt * (mode != null && (mode.type == "catch" || mode.type == "reaction") ? .12f : 1);
            if (interactionNeedsNeutral)
            {
                if (NeutralInput(input)) interactionNeedsNeutral = false;
                input.force = input.leftForce = input.rightForce = 0;
                input.actionHeld = input.muteHeld = false;
            }
            if (input.petPressed && mode == null) PetCat();
            if (input.toyPressed && mode == null) TossCatToy();
            TickNightTools(worldDt, input);
            TickCat(worldDt);
            if (mode == null && levelIndex == 2 && vaseState == "stable" && FlatDistance(player, VasePosition()) < 1.35f) BeginIncident("vase");
            NightMode current = mode;
            if (current != null)
            {
                current.elapsed += dt;
                switch (current.type)
                {
                    case "door": TickDoor(dt, input, current); break;
                    case "step": TickFloor(input, current); break;
                    case "step-transit": TickFloorTransit(current); break;
                    case "lockpick": TickLock(dt, input, current); break;
                    case "search": TickSearch(current); break;
                    case "catch": TickRescue(dt, input, current); break;
                    case "reaction": TickReaction(current); break;
                    case "tickle": TickTickle(dt, input, current); break;
                }
            }
            else tickleHeat = Mathf.Max(0, tickleHeat - worldDt * .025f);
            TickTutorial(dt, input);
        }

        public void TryInteract()
        {
            if (!active || status != "playing" || mode != null) return;
            if (PhonePending()) { phone.armed = true; return; }
            string station = TutorialStationNear();
            if (station != null) { BeginIncident(station); return; }
            if (TickleNear())
            {
                mode = new NightMode { type = "tickle", target = TicklePosition(), lift = -9, hint = "按住 E 轻挠，松手停止；Esc 收手。留意停鼾和缩脚。" };
                interactionSoundAt = 0; Say(mode.hint, "hint"); return;
            }
            NightDoor door = DoorNear(); NightSpot spot = SpotNear(); NightLure tool = ToolNear();
            if (tool != null && (door == null || FlatDistance(player, tool.position) < FlatDistance(player, door.Position)) && (spot == null || FlatDistance(player, tool.position) < FlatDistance(player, spot.Position)))
            {
                tool.armed = true; tool.timer = 2.5f;
                Say(tool.name + " 已定时，2.5 秒后响起。先离开这里。", "hint"); Emit("switch", 5, tool.position); return;
            }
            if (door != null)
            {
                door.direction = door.progress <= 0 ? 1 : door.progress >= 1 ? -1 : door.direction;
                door.force = 0; door.speed = 0; door.impacted = false;
                doorNoiseTime = doorNoiseEnergy = 0;
                mode = new NightMode { type = "door", index = doors.IndexOf(door), id = door.name, phase = door.direction == 1 ? "open" : "close", target = door.Position, progress = door.progress, hint = "手搭上门把。按住 E 施力，松手即停；R 换开合方向，Esc 收手。" };
                Emit("doorHandle", 5, door.Position); Say(mode.hint, "hint"); return;
            }
            if (spot != null && levelIndex != 3)
            {
                if (SpotLocked(spot)) BeginLock(spot); else BeginSearch(spot);
                return;
            }
            if (CatNear() && cat.state != "calm" && cat.state != "play") { PetCat(); return; }
            Say(levelIndex == 3 ? TutorialHint() : hasDevice ? "设备拿到了，返回发暖光的卧室。" : "靠近门、柜子、家猫或声源道具，再按 E。", "hint");
        }

        public void CancelInteraction()
        {
            if (phone != null) { phone.armed = false; phone.hold = 0; }
            if (mode == null) return;
            if (mode.type == "catch") { ResolveIncident(false, "手松得太早，物件滑了下去。", 1); return; }
            if (mode.type == "reaction") { resumeSearchIndex = -1; return; }
            if (mode.type == "step-transit") return;
            if (mode.type == "lockpick" && lockState != null) ReleaseLock(lockState);
            foreach (var door in doors) { door.force = 0; door.speed = 0; }
            doorNoiseTime = doorNoiseEnergy = 0;
            mode = null;
        }

        public string InteractionHint()
        {
            if (mode != null) return mode.hint;
            if (PhonePending()) return "停下，按住 E / □ 1.2 秒静音  " + Mathf.RoundToInt(phone.hold / 1.2f * 100) + "%";
            string station = TutorialStationNear();
            if (station != null) return station == "vase" ? "E 练习托放花瓶" : "E 练习托底、压盖和归位";
            if (TickleNear()) return "E 轻挠熟睡家长的脚底（会刺激家长）";
            var door = DoorNear(); if (door != null) return "E 握住" + door.name + " · 再按住 E 施力";
            var spot = SpotNear(); if (spot != null && levelIndex != 3) return "E " + (SpotLocked(spot) ? "查看机械锁 · " : "翻找 · ") + spot.name;
            var tool = ToolNear(); if (tool != null) return "E 定时启动" + tool.name;
            if (CatNear()) return "E 轻抚家猫 · Q 丢玩具球";
            return levelIndex == 3 ? TutorialHint() : hasDevice ? "带设备返回卧室 · 来电前可找掩护" : "观察环境与声音，寻找设备并返回卧室";
        }

        public NightDoor DoorNear()
        {
            NightDoor best = null; float nearest = 1.2f;
            foreach (var door in doors) { float d = FlatDistance(player, door.Position); if (d < nearest) { nearest = d; best = door; } }
            return best;
        }

        public NightSpot SpotNear()
        {
            NightSpot best = null; float nearest = 1.2f;
            foreach (var spot in spots)
            {
                float d = FlatDistance(player, spot.Position);
                if (!spot.searched && d < nearest && !Occluded(player, spot.Position, true, spot.id)) { nearest = d; best = spot; }
            }
            return best;
        }

        NightLure ToolNear()
        {
            NightLure best = null; float nearest = 1.25f;
            foreach (var tool in lures)
            {
                float d = FlatDistance(player, tool.position);
                if (!tool.armed && !tool.done && d < nearest && !Occluded(player, tool.position, true, tool.furniture)) { nearest = d; best = tool; }
            }
            return best;
        }

        static float HingeRoughness(float speed, float resistance, float wear)
        {
            float[] x = { 0, .2f, .3f, .45f, .6f, .8f, 1 };
            float[] y = { .035f, .09f, .32f, .82f, .76f, .16f, .055f };
            float s = Mathf.Clamp01(speed);
            for (int i = 1; i < x.Length; i++)
                if (s <= x[i]) return Mathf.Clamp01(Mathf.Lerp(y[i - 1], y[i], Mathf.SmoothStep(0, 1, (s - x[i - 1]) / (x[i] - x[i - 1]))) * (.72f + resistance * .40f + wear));
            return .035f;
        }

        void TickDoor(float dt, NightInput input, NightMode current)
        {
            if (current.index < 0 || current.index >= doors.Count) { mode = null; return; }
            NightDoor door = doors[current.index];
            if (input.reverse) { door.direction *= -1; door.force = 0; doorNoiseTime = doorNoiseEnergy = 0; }
            bool held = input.force > .035f || input.rightForce > .035f || input.actionHeld;
            float analog = Mathf.Max(input.force, input.rightForce);
            door.force = held ? analog > .035f ? Mathf.Clamp01(analog) : Mathf.Clamp01(door.force + dt * .56f) : Mathf.Max(0, door.force - dt * 3.8f);
            current.force = door.force; current.progress = door.progress; current.phase = door.direction == 1 ? "open" : "close";
            if (!held)
            {
                door.speed = 0; current.balance = 0; current.hint = "手扶着门，屋里又安静下来。E 继续施力 · R 反向 · Esc 收手";
                // 源版累计的是实际运动时长。松力停止声源，但不能反复轻点来抹掉已产生的声能。
                return;
            }
            float a = (door.progress - (Mathf.Abs(door.x - 3) < .01f ? .36f : .27f)) / .09f;
            float b = (door.progress - (Mathf.Abs(door.x - 3) < .01f ? .77f : .68f)) / .075f;
            float resistance = Mathf.Max(Mathf.Exp(-a * a), Mathf.Exp(-b * b));
            float rate = .075f + door.force * .25f * (1 - resistance * .08f);
            float speed = Mathf.Clamp01((rate - .075f) / .25f);
            float roughness = HingeRoughness(speed, resistance, (levelIndex == 3 ? 0 : levelIndex) * .025f + (Mathf.Abs(door.x - 3) < .01f ? 0 : .055f));
            float next = Mathf.Clamp01(door.progress + door.direction * rate * dt);
            bool blocked = DoorSweepHits(door, door.progress, next, player, .20f) || ((parent.state == "checking" || parent.state == "returning") && DoorSweepHits(door, door.progress, next, parent.position, .24f));
            if (blocked)
            {
                door.speed = 0; current.balance = 0; current.hint = "门边有人，先松开把手挪一挪。Esc 收手";
                return;
            }
            door.progress = next; door.speed = speed; current.progress = next; current.balance = roughness; current.torque = resistance;
            doorSeconds += dt; quietDoorSeconds += dt * (1 - Mathf.Clamp01((roughness - .14f) / .86f));
            current.hint = speed > .76f ? "门轴声轻下来了，快到头时收住力。" : roughness > .65f ? "咯吱咯吱——涩点带着把手轻颤。" : roughness > .22f ? "吱……门轴有些发涩。" : door.direction == -1 ? "轻轻咯吱……门缝缓缓合拢。" : "轻轻咯吱……门缝缓缓张开。";
            current.hint += "  松手即停 · R 反向 · Esc 收手";
            doorNoiseTime += dt; doorNoiseEnergy += (3 + roughness * 25) * dt;
            if (doorNoiseTime >= .75f)
            {
                MakeNoise(doorNoiseEnergy / doorNoiseTime, "门轴发出一阵咯吱声。", "hingeMotion", door.Position);
                doorNoiseTime = doorNoiseEnergy = 0;
            }
            if (door.direction == 1 ? next >= 1 : next <= 0)
            {
                float impact = Mathf.Pow(Mathf.Clamp01((rate - .235f) / .09f), 2);
                metrics.doors++; door.speed = 0; door.force = 0; mode = null;
                if (impact > 0)
                {
                    door.impacted = true; metrics.doorBumps++;
                    MakeNoise(24 + impact * 66, impact > .55f ? "砰——！门猛地撞上门框。" : "咚，门在尽头碰了一下。", "doorBump", door.Position);
                }
                else { Say(next >= 1 ? "门开了。停一拍，听听有没有回应。" : "门轻轻合上了。听听门外的动静。", "good"); Emit("latch", 12, door.Position); }
            }
        }

        public bool TryFloorStep(Vector3 next)
        {
            int x = Mathf.RoundToInt(next.x), z = Mathf.RoundToInt(next.z);
            if (x == floorX && z == floorZ) return false;
            bool creak = false;
            if (level.creaks != null) foreach (var tile in level.creaks) if (Mathf.RoundToInt(tile.x) == x && Mathf.RoundToInt(tile.y) == z) { creak = true; break; }
            if (!creak) { floorX = x; floorZ = z; return false; }
            if (MaskAt(player))
            {
                floorX = x; floorZ = z; metrics.steps++; metrics.goodSteps++; metrics.maskedSteps++;
                MakeNoise(25, "借着环境声走过木板。", "floorSoft", player);
                Say("趁着掩护声走过了松动木板。视线仍然危险。", "good"); return false;
            }
            Vector3 direction = next - player; direction.y = 0;
            Vector3 target = next + direction.normalized * .34f;
            if (!CanOccupy(target)) target = next;
            mode = new NightMode { type = "step", target = target, returnX = x, returnY = z, pointer = .5f + .48f * Mathf.Sin(time * 3.8f), hint = "松动木板：指针进入中央亮区时按 Space / ×，轻轻落脚；Esc 退开。" };
            velocity = Vector3.zero;
            Emit("floorPressure", 3, player); Say(mode.hint, "hint"); return true;
        }

        void TickFloor(NightInput input, NightMode current)
        {
            current.pointer = .5f + .48f * Mathf.Sin(time * 3.8f);
            if (!input.confirm) return;
            bool good = Mathf.Abs(current.pointer - .5f) <= level.stepWidth * .5f;
            metrics.steps++; if (good) metrics.goodSteps++;
            floorX = Mathf.RoundToInt(current.returnX); floorZ = Mathf.RoundToInt(current.returnY);
            transitFrom = player;
            mode = new NightMode { type = "step-transit", target = current.target, hint = "轻轻落脚……" };
            MakeNoise(good ? 5 : 50, good ? "稳稳落下。再听听卧室里有没有变化。" : "吱呀——这块木板响了。", good ? "floorSoft" : "floorCreak", player);
        }

        void TickFloorTransit(NightMode current)
        {
            Vector3 next = Vector3.Lerp(transitFrom, current.target, Mathf.SmoothStep(0, 1, current.elapsed / .25f));
            if (CanOccupy(next)) player = next;
            if (current.elapsed >= .25f) mode = null;
        }

        public bool SpotLocked(NightSpot spot)
        {
            if (spot.unlocked) return false;
            if (levelIndex == 0 || levelIndex == 3) return spot.id == "study-search";
            if (levelIndex == 1) return spot.id == "storage-search" || spot.id == "kitchen-search";
            return spot.id == "laundry-search" || spot.id == "storage-search";
        }

        void BeginLock(NightSpot spot)
        {
            lockState = locks.Find(value => value.id == spot.id);
            if (lockState == null)
            {
                int difficulty = levelIndex == 3 ? 0 : levelIndex;
                uint hash = seed;
                unchecked { foreach (char c in spot.id) hash = hash * 31 + c; }
                lockState = new NightLock { id = spot.id, level = difficulty, pins = new NightPin[difficulty == 0 ? 3 : 4] };
                for (int i = 0; i < lockState.pins.Length; i++)
                {
                    unchecked { hash = hash * 1664525 + 1013904223; }
                    lockState.pins[i] = new NightPin { target = .35f + (float)(hash / 4294967296.0) * .39f, spool = difficulty > 0 && i % 2 == 1 };
                }
                locks.Add(lockState);
            }
            ReleaseLock(lockState);
            mode = new NightMode { type = "lockpick", id = spot.id, index = spots.IndexOf(spot), target = spot.Position, hint = "A / D 选弹子；按住 E 顶起，接缝齐平时松手。腰形弹子卡住时保持 E 并按 Q 卸力。" };
            Say("看锁芯里的接缝。顶到边缘齐平时松手；Esc 随时收手听动静。", "hint");
            SyncLockMode();
        }

        static void ReleaseLock(NightLock mechanism)
        {
            mechanism.pressure = mechanism.velocity = mechanism.stress = 0;
            mechanism.held = mechanism.armed = false; mechanism.navigation = 0;
        }

        void TickLock(float dt, NightInput input, NightMode current)
        {
            NightLock mechanism = lockState;
            if (mechanism == null || current.index < 0 || current.index >= spots.Count) { mode = null; return; }
            mechanism.cooldown = Mathf.Max(0, mechanism.cooldown - dt);
            bool all = true; foreach (var value in mechanism.pins) if (!value.seated) { all = false; break; }
            if (all)
            {
                mechanism.turn = Mathf.Min(1, mechanism.turn + dt / 1.05f); mechanism.feedback = "open";
                if (mechanism.turn >= 1)
                {
                    var spot = spots[current.index]; spot.unlocked = true;
                    Emit("latch", 10, spot.Position); Say("锁芯转开了。轻轻翻找……", "good"); BeginSearch(spot); return;
                }
                SyncLockMode(); return;
            }
            float pressure = input.analog ? RightPressure(input, .52f) : (input.actionHeld || input.force > 0 || input.rightForce > 0 ? .52f : 0);
            bool held = pressure > .06f;
            if (!mechanism.armed) { if (!held) mechanism.armed = true; mechanism.pressure = 0; SyncLockMode(); return; }
            int nav = Mathf.Abs(input.move.x) > .45f ? input.move.x > 0 ? 1 : -1 : 0;
            if (!held && !mechanism.held && nav != 0 && nav != mechanism.navigation)
            {
                NightPin old = mechanism.pins[mechanism.selected]; old.lift = old.seated ? old.target : 0;
                mechanism.selected = (mechanism.selected + nav + mechanism.pins.Length) % mechanism.pins.Length;
                mechanism.velocity = 0; mechanism.feedback = "lift";
            }
            mechanism.navigation = nav;
            NightPin pin = mechanism.pins[mechanism.selected];
            if (!pin.seated)
            {
                if (held)
                {
                    mechanism.velocity = Mathf.Lerp(mechanism.velocity, .12f + pressure * .55f, Mathf.Min(1, dt * 18));
                    float lift = Mathf.Clamp01(pin.lift + mechanism.velocity * dt), shoulder = pin.target * .58f;
                    if (pin.spool && !pin.cleared && lift >= shoulder)
                    {
                        if (input.leftForce > .12f) { pin.cleared = true; mechanism.feedback = "freed"; Emit("lockPin", 6, current.target); }
                        else { lift = shoulder; mechanism.feedback = "binding"; }
                    }
                    pin.lift = lift;
                    if (pin.lift > pin.target + .075f)
                    {
                        mechanism.stress += dt; mechanism.feedback = "over";
                        if (mechanism.cooldown <= 0 && mechanism.stress > .18f)
                        {
                            MakeNoise(19, "锁里的金属刮响了。先收力听听屋里。", "lockScrape", current.target); mechanism.cooldown = .85f;
                            if (mechanism.level == 2)
                            {
                                NightPin previous = mechanism.pins[(mechanism.selected + mechanism.pins.Length - 1) % mechanism.pins.Length];
                                if (previous.seated) { previous.seated = previous.cleared = false; previous.lift = 0; mechanism.feedback = "slipped"; }
                            }
                        }
                    }
                    else
                    {
                        mechanism.stress = 0;
                        if (!pin.spool || pin.cleared || pin.lift < shoulder) mechanism.feedback = Mathf.Abs(pin.lift - pin.target) < .05f ? "aligned" : "lift";
                    }
                }
                else if (mechanism.held)
                {
                    if (Mathf.Abs(pin.lift - pin.target) <= .05f && (!pin.spool || pin.cleared))
                    {
                        pin.seated = true; pin.lift = pin.target; mechanism.feedback = "set"; Emit("lockPin", 6, current.target);
                    }
                    else { mechanism.feedback = "miss"; pin.cleared = false; }
                    mechanism.velocity = mechanism.stress = 0;
                }
                else pin.lift = Mathf.Max(0, pin.lift - dt * 1.3f);
            }
            else mechanism.feedback = "set";
            mechanism.pressure = held ? pressure : 0; mechanism.held = held;
            SyncLockMode();
        }

        void SyncLockMode()
        {
            if (mode == null || lockState == null || mode.type != "lockpick") return;
            var mechanism = lockState; mode.pin = mechanism.selected; mode.force = mechanism.pressure;
            mode.progress = mechanism.turn; mode.phase = mechanism.feedback; mode.stage = mechanism.level;
            mode.values = new float[mechanism.pins.Length]; mode.solved = new bool[mechanism.pins.Length];
            for (int i = 0; i < mechanism.pins.Length; i++) { mode.values[i] = mechanism.pins[i].lift; mode.solved[i] = mechanism.pins[i].seated; }
            mode.lift = mechanism.pins[mechanism.selected].lift; mode.pointer = mechanism.pins[mechanism.selected].target;
            switch (mechanism.feedback)
            {
                case "aligned": mode.hint = "接缝齐了。现在松开 E / R2。"; break;
                case "set": mode.hint = "咔哒，这根卡住了。松手，A / D 移向下一根。"; break;
                case "miss": mode.hint = "弹回来了。看着接缝，再试一次。"; break;
                case "over": mode.hint = "顶过头了，锁芯在发颤。松手卸力。"; break;
                case "binding": mode.hint = "腰形弹子卡在台肩。保持 E / R2 顶住，按 Q / L2 卸开扭力。"; break;
                case "freed": mode.hint = "台肩让开了，继续轻轻顶起。"; break;
                case "slipped": mode.hint = "共用压片被带动，旁边的弹子滑脱了。"; break;
                case "open": mode.hint = "弹子全部到位，锁芯正在转开。"; break;
                default: mode.hint = "A / D 选弹子 · E / R2 顶起 · 接缝齐平时松手 · Esc 收手"; break;
            }
        }

        void BeginSearch(NightSpot spot, float elapsed = 0)
        {
            mode = new NightMode { type = "search", index = spots.IndexOf(spot), id = spot.id, target = spot.Position, elapsed = elapsed, progress = elapsed / 6, hint = "轻轻翻找……有动静时按 Esc 立即停下。" };
            interactionSoundAt = elapsed;
        }

        public static double IncidentRoll(uint incidentSeed, string id)
        {
            uint n = incidentSeed;
            unchecked
            {
                foreach (char c in id) n = (n ^ c) * 16777619;
                n ^= n >> 16; n *= 2246822507; n ^= n >> 13;
            }
            return n / 4294967296.0;
        }

        void TickSearch(NightMode current)
        {
            if (current.index < 0 || current.index >= spots.Count) { mode = null; return; }
            NightSpot spot = spots[current.index];
            current.progress = Mathf.Clamp01(current.elapsed / 6);
            if (current.elapsed >= interactionSoundAt) { interactionSoundAt = current.elapsed + 1.2f; Emit("search", 8, current.target); }
            if (current.elapsed >= 2.2f && !spot.incidentUsed && IncidentKind(spot.id) != null)
            {
                spot.incidentUsed = true;
                if (IncidentRoll(seed, spot.id) < .7) { BeginIncident(spot.id, current.index, current.elapsed); return; }
            }
            if (current.elapsed < 6) return;
            spot.searched = true; metrics.searches++; mode = null;
            if (spot.device)
            {
                hasDevice = true; phone = new NightPhone { state = "warning", timer = 6 }; phoneCue = -1;
                Say("找到了！屏幕亮了，有来电预兆。停下，按住 E 1.2 秒静音。", "good"); Emit("found", 45, spot.Position);
            }
            else Say("这里没有设备，换个藏点看看。", "info");
        }

        static string IncidentKind(string id)
        {
            if (id == "vase") return "vase";
            if (id == "kitchen-search") return "fork";
            if (id == "study-search") return "pencils";
            if (id == "storage-search") return "tin";
            return null;
        }

        Vector3 VasePosition() { return levelIndex == 3 ? new Vector3(1.3f, 0, 8) : new Vector3(9.7f, 0, 2.8f); }

        public void BeginIncident(string id, int searchIndex = -1, float searchElapsed = 0)
        {
            string kind = IncidentKind(id); if (kind == null) return;
            var spot = spots.Find(item => item.id == id);
            if (spot != null) spot.incidentUsed = true;
            if (id == "vase") vaseState = "wobbling";
            int sign = seed % 2 == 1 ? 1 : -1;
            rescue = new NightRescue { kind = kind, stage = kind == "pencils" ? "sweep" : "reach", objectX = sign * .44f, tilt = sign * .24f,
                pencils = new[] { new NightPencil { x = -.58f, at = 1.65f }, new NightPencil { x = .48f, at = 3 }, new NightPencil { x = -.02f, at = 4.35f } } };
            resumeSearchIndex = searchIndex; resumeSearchElapsed = searchElapsed;
            Vector3 source = id == "vase" ? VasePosition() : spot != null ? spot.Position : player;
            mode = new NightMode { type = "catch", id = id, kind = kind, target = source, timeout = levelIndex == 3 ? -1 : 10, phase = rescue.stage };
            velocity = Vector3.zero; parent.recognition = 0;
            lastIncidentId = id; lastIncidentResult = null;
            Say(kind == "vase" ? "花瓶晃了！对准落点，双手托住。" : kind == "fork" ? "餐具格里的叉子滑出来了！右手接住，左手裹住止响。" : kind == "pencils" ? "碰倒了笔筒！移手拦住三路铅笔。" : "铁盒滑下来了！右手托底，左手压盖。", "warning");
            Emit("wobble", 15, source); SyncRescueMode();
        }

        static Vector3 RescueRest(string kind)
        {
            return kind == "vase" ? new Vector3(0, .319f, 0) : kind == "tin" ? new Vector3(0, .287f, -.04f) : kind == "fork" ? new Vector3(-.09f, .123f, 0) : new Vector3(-.43f, .225f, -.17f);
        }

        float RescueFurnitureDepth(string kind)
        {
            string id = kind == "vase" ? "vase-stand" : kind == "tin" ? "storage-search" : kind == "fork" ? "kitchen-search" : "study-search";
            if (level.furniture != null) foreach (var furniture in level.furniture) if (furniture.id == id) return furniture.d;
            return kind == "vase" ? .58f : kind == "tin" ? .68f : kind == "fork" ? .9f : .8f;
        }

        public Vector3 RescueObjectPosition()
        {
            if (rescue == null) return Vector3.zero;
            if (rescue.stage == "lower") return rescue.returnPosition;
            Vector3 rest = RescueRest(rescue.kind);
            float amount = rescue.stage == "reach" ? Mathf.SmoothStep(0, 1, mode == null ? 1 : mode.elapsed / .65f) : 1;
            return new Vector3(Mathf.Lerp(rest.x, rescue.objectX * .55f, amount), rest.y - Mathf.Max(0, 1.3f - rescue.height) * .5f, Mathf.Lerp(rest.z, RescueFurnitureDepth(rescue.kind) / 2 + .15f, amount));
        }

        void TickRescue(float dt, NightInput input, NightMode current)
        {
            if (rescue == null) { mode = null; return; }
            NightRescue r = rescue;
            float oldX = r.handX;
            if (r.stage != "lower") r.handX = Mathf.Clamp(r.handX + input.move.x * dt * 1.5f, -.95f, .95f);
            r.left = Mathf.Clamp01(input.leftForce);
            r.right = RightPressure(input, .55f);
            r.grace = Mathf.Max(0, r.grace - dt);
            current.timeout = levelIndex == 3 ? -1 : Mathf.Max(0, 10 - current.elapsed);
            if (r.kind == "pencils")
            {
                int caught = 0, resolved = 0;
                foreach (NightPencil pencil in r.pencils)
                {
                    if (pencil.state == "rolling")
                    {
                        if (current.elapsed >= pencil.at - .4f && current.elapsed <= pencil.at + .18f && r.right > .12f && Mathf.Abs(r.handX - pencil.x) < .27f)
                        { pencil.state = "caught"; pencil.resolvedAt = current.elapsed; Emit("catchTouch", 4, current.target); }
                        else if (current.elapsed > pencil.at + .18f) { pencil.state = "fallen"; pencil.resolvedAt = current.elapsed; }
                    }
                    if (pencil.state == "caught") caught++;
                    if (pencil.state != "rolling") resolved++;
                }
                if (resolved == 3) { ResolveIncident(caught == 3, caught == 3 ? "三枝铅笔全拦住了，轻轻收回笔筒。" : "拦住了 " + caught + " / 3 枝，其余滚到了地上。", (3 - caught) / 3f); return; }
            }
            else
            {
                bool fork = r.kind == "fork", tin = r.kind == "tin", both = r.left > .12f && r.right > .12f;
                bool supported = fork ? r.right > .12f : both;
                if (r.stage == "reach")
                {
                    r.objectX = Mathf.Clamp(r.objectX + Mathf.Sin(current.elapsed * 2.2f) * dt * .16f, -.65f, .65f);
                    r.height = 1.3f - Mathf.Max(0, current.elapsed - .65f) * .22f;
                    if (current.elapsed > .65f && r.height > .32f && Mathf.Abs(r.handX - r.objectX) < .25f && (fork || tin ? r.right > .12f : both))
                    {
                        r.stage = fork ? "damp" : "steady"; r.contactAt = current.elapsed; r.release = 0; r.grace = .4f;
                        Emit("catchTouch", 4, current.target);
                    }
                    else if (current.elapsed > 4.9f) { ResolveIncident(false); return; }
                }
                else
                {
                    r.objectX = r.handX; r.release = supported ? 0 : r.release + dt;
                    if (r.release > .20f && r.grace <= 0 && !(tin && r.right > .12f)) { ResolveIncident(false, "手松得太早，物件又滑了下去。", 1); return; }
                    if (fork)
                    {
                        r.steady = both ? r.steady + dt : Mathf.Max(0, r.steady - dt);
                        r.tilt = Mathf.Sin(current.elapsed * 35) * .12f * Mathf.Max(0, 1 - r.steady / .6f);
                        if (r.steady >= .6f) { ResolveIncident(true, "袖口裹住叉子，金属的颤动停了。", 0); return; }
                    }
                    else if (tin)
                    {
                        r.tilt *= Mathf.Exp(-dt * 4);
                        if (r.left < .12f)
                        {
                            r.lidNoise += dt;
                            if (r.lidNoise > .5f && !r.clink) { r.clink = true; MakeNoise(16, "盒盖叮了一声，左手轻轻压住它。", "metalDrop", current.target); }
                        }
                        r.steady = both ? r.steady + dt : 0;
                    }
                    else
                    {
                        r.tilt = Mathf.Clamp(r.tilt * Mathf.Exp(-dt * 1.7f) + (r.right - r.left) * dt * 2 + (r.handX - oldX) * .22f, -1.5f, 1.5f);
                        if (Mathf.Abs(r.tilt) > .85f) { ResolveIncident(false, "两只手用力差太多，花瓶歪了出去。", 1); return; }
                        r.steady = both && Mathf.Abs(r.tilt) < .20f ? r.steady + dt : 0;
                    }
                    if (!fork && r.steady > .5f && r.stage != "lower")
                    {
                        r.returnFrom = RescueObjectPosition(); r.returnPosition = r.returnFrom; r.returnHeight = r.height; r.returnTilt = r.tilt;
                        r.returnProgress = 0; r.returnPhase = "lift"; r.stage = "lower";
                    }
                    if (r.stage == "lower" && both && MoveRescueReturn(input.move, dt)) { ResolveIncident(true); return; }
                }
            }
            if (levelIndex != 3 && current.elapsed >= 10) { ResolveIncident(false, "来不及稳稳放回，物件滑了下去。", 1); return; }
            SyncRescueMode();
        }

        float RescueDepth(Vector3 position)
        {
            float y = rescue.kind == "vase" ? 1.28f : rescue.kind == "pencils" ? 1.22f : .96f;
            float z = (rescue.kind == "vase" ? 4.4f : rescue.kind == "tin" ? 3.4f : rescue.kind == "pencils" ? 2.6f : 2.25f) / Mathf.Min(1, Mathf.Max(.1f, rescueAspect) / .95f);
            float targetY = rescue.kind == "vase" ? .42f : rescue.kind == "pencils" ? .20f : .07f;
            float dy = y - targetY, dz = z - .08f;
            return ((y - position.y) * dy + (z - position.z) * dz) / Mathf.Sqrt(dy * dy + dz * dz);
        }

        bool MoveRescueReturn(Vector2 input, float dt)
        {
            NightRescue r = rescue; Vector3 p = r.returnPosition, rest = RescueRest(r.kind);
            float oldDepth = RescueDepth(p); input = Vector2.ClampMagnitude(input, 1);
            float amount = input.y * dt * .50f, clearY = Mathf.Max(rest.y + .16f, r.returnFrom.y);
            if (r.returnPhase == "place") p.y = Mathf.Clamp(p.y + amount, rest.y, clearY + .12f);
            else if (amount > 0)
            {
                float lift = Mathf.Min(amount, Mathf.Max(0, clearY - p.y)); p.y += lift;
                p.z = Mathf.Max(rest.z, p.z - (amount - lift) * 1.8f);
                if (p.z <= rest.z) r.returnPhase = "place";
            }
            else if (amount < 0)
            {
                float forward = Mathf.Min(-amount * 1.8f, Mathf.Max(0, r.returnFrom.z - p.z)); p.z += forward;
                p.y = Mathf.Max(rest.y - .30f, p.y + amount + forward / 1.8f);
            }
            float depth = RescueDepth(p);
            p.x = Mathf.Clamp(p.x * depth / Mathf.Max(.01f, oldDepth) + input.x * dt * .65f * depth / 2.5f, -.65f, .65f);
            r.handX = Mathf.Clamp(p.x / .55f, -.95f, .95f);
            r.returnProgress = r.returnPhase == "place" ? Mathf.Clamp01(1 - (p.y - rest.y) / .16f) : 0;
            r.height = .34f + (r.returnHeight - .34f) * (1 - r.returnProgress); r.returnPosition = p;
            return r.returnPhase == "place" && input.y < -.08f && p.y <= rest.y + .001f && Mathf.Abs(p.x - rest.x) < .065f;
        }

        void SyncRescueMode()
        {
            if (mode == null || rescue == null) return;
            var r = rescue; mode.phase = r.stage; mode.returnX = r.handX; mode.returnY = r.height;
            mode.pointer = r.objectX; mode.force = r.right; mode.torque = r.left; mode.balance = r.tilt; mode.progress = r.returnProgress;
            if (r.kind == "pencils")
            {
                int caught = 0; foreach (var pencil in r.pencils) if (pencil.state == "caught") caught++;
                mode.hint = "A / D 移手，按住 E / R2 拦住铅笔。已拦 " + caught + " / 3 枝。"; return;
            }
            if (r.stage == "reach") mode.hint = r.kind == "fork" ? "A / D 对准叉子，E 托住，再用 Q 裹住止响。" : r.kind == "tin" ? "A / D 对准铁盒，E 托底，Q 压住盒盖。" : "A / D 对准花瓶，Q + E 双手托住。左右用力不均会歪。";
            else if (r.stage == "damp") mode.hint = "保持 E 托住，再按住 Q，用袖口包住叉子，等它停止颤动。";
            else if (r.stage == "steady") mode.hint = r.kind == "tin" ? "保持 E 托底，Q 轻压盒盖，别让它叮当响。" : "保持 Q + E 托住，把花瓶扶正。";
            else mode.hint = r.returnPhase == "place" ? "保持 Q + E 支撑，A / D 对准原垫，S 向下轻放。" : "保持 Q + E 支撑，W 向上抬过桌沿并送回，A / D 对准原垫，再 S 轻放。";
        }

        void ResolveIncident(bool success, string result = null, float noiseFactor = 1)
        {
            if (mode == null || mode.type != "catch" || rescue == null) return;
            var previous = mode; var r = rescue; r.success = success; r.sourceElapsed = previous.elapsed;
            incidentResults.RemoveAll(item => item.id == previous.id);
            incidentResults.Add(new NightIncidentResult { id = previous.id, kind = r.kind, success = success, sourceElapsed = previous.elapsed, rescue = r });
            lastIncidentId = previous.id; lastIncidentResult = success ? "caught" : "fallen";
            if (previous.id == "vase") { vaseState = lastIncidentResult; if (success) vasePracticed = true; }
            if (previous.id == "storage-search" && success) tinPracticed = true;
            if (success) metrics.rescued++; else metrics.failedRescues++;
            string name = r.kind == "vase" ? "花瓶" : r.kind == "tin" ? "铁盒" : r.kind == "fork" ? "叉子" : "铅笔";
            reactionText = result ?? (success ? "稳稳接住" + name + "，轻轻放回原处。" : name + "掉在了地上。先听听父母的动静。");
            reactionNoise = (r.kind == "vase" ? 90 : r.kind == "tin" ? 65 : r.kind == "fork" ? 55 : 40) * noiseFactor;
            reactionSound = r.kind == "vase" ? "crash" : r.kind == "pencils" ? "pencilDrop" : "metalDrop";
            reactionImpact = success ? -1 : r.kind == "pencils" ? .34f : .48f;
            mode = new NightMode { type = "reaction", id = previous.id, kind = previous.kind, target = previous.target, caught = success, phase = success ? "return" : "fall", hint = reactionText };
            Say(reactionText, success ? "good" : "warning");
            if (success) Emit("cloth", 8, previous.target);
        }

        void TickReaction(NightMode current)
        {
            current.progress = Mathf.Clamp01(current.elapsed / 1.25f);
            if (reactionImpact >= 0 && current.elapsed >= reactionImpact)
            {
                reactionImpact = -1; MakeNoise(reactionNoise, reactionText, reactionSound, current.target);
            }
            if (current.elapsed < 1.25f) return;
            if (resumeSearchIndex >= 0 && resumeSearchIndex < spots.Count)
            {
                int index = resumeSearchIndex; resumeSearchIndex = -1;
                BeginSearch(spots[index], resumeSearchElapsed); Say("继续轻轻翻找……Esc 可以停下。", "hint");
            }
            else mode = null;
        }

        public bool PhonePending() { return phone != null && (phone.state == "warning" || phone.state == "ringing"); }

        void TickNightTools(float dt, NightInput input)
        {
            foreach (NightLure tool in lures)
            {
                if (!tool.armed || tool.done) continue;
                tool.timer -= dt;
                if (tool.timer > 0) continue;
                tool.pulses++; tool.timer = 1.6f;
                Emit(tool.kind, 50, tool.position); HearNoise(65, tool.kind, tool.position, true);
                if (tool.pulses == 1) Say(tool.name + " 响了。留意父母是否听见。", "info");
                if (tool.pulses >= 4) tool.done = true;
            }
            if (!PhonePending() || (mode != null && (mode.type == "catch" || mode.type == "reaction"))) return;
            phone.timer -= dt;
            bool mute = input.muteHeld || input.actionHeld;
            if (mute && phone.armed && mode == null && velocity.magnitude < .12f)
            {
                phone.hold += dt;
                if (phone.hold >= 1.2f)
                {
                    phone.state = "silenced"; phone.armed = false;
                    Say("来电已静音。带手机回到卧室。", "good"); Emit("phoneMute", 5, player); return;
                }
            }
            else { phone.hold = 0; if (!mute) phone.armed = false; }
            if (phone.state == "warning")
            {
                int second = Mathf.CeilToInt(phone.timer);
                if (second != phoneCue) { phoneCue = second; Emit("phoneBuzz", 4, player); }
                if (phone.timer <= 0) { phone.state = "ringing"; phone.timer = 0; Say("来电响了！停下，按住 E 静音。", "warning"); }
            }
            if (phone.state == "ringing" && phone.timer <= 0)
            {
                phone.rings++; MakeNoise(38, "手机铃声传进了走廊。", "phoneRing", player); phone.timer = 2.4f;
                if (phone.rings >= 3) { phone.state = "missed"; phone.armed = false; Say("来电挂断了。留意父母的脚步。", "warning"); }
            }
        }

        public bool CatNear(float radius = 1.55f)
        {
            return cat != null && (levelIndex != 3 || tutorialStep >= 8) && cat.state != "jump" && FlatDistance(cat.position, player) < radius && !Occluded(cat.position, player);
        }

        public bool PetCat()
        {
            if (!active || status != "playing" || mode != null || !CatNear()) return false;
            cat.state = "calm"; cat.timer = 18; catMemory.route.Clear(); catMemory.still = 0; catMemory.cooldown = 24; catMemory.pets++;
            cat.heading = Mathf.Atan2(player.x - cat.position.x, player.z - cat.position.z);
            Say("呼噜噜……猫被摸舒服了，会安静待一会儿。", "good"); Emit("catPurr", 5, cat.position); return true;
        }

        public bool TossCatToy()
        {
            if (!active || status != "playing" || mode != null || !CatNear(6) || catMemory.toyCooldown > 0) return false;
            float[] offsets = { 0, .65f, -.65f, 1.3f, -1.3f, Mathf.PI };
            foreach (float offset in offsets)
            {
                Vector3 target = player + new Vector3(Mathf.Sin(heading + offset), 0, Mathf.Cos(heading + offset)) * 2.5f;
                if (!CanOccupy(target) || Occluded(player, target)) continue;
                var route = FindPath(cat.position, target, true, .2f);
                if (route.Count == 0 || FlatDistance(route[route.Count - 1], target) >= .45f) continue;
                catMemory.route = route; catMemory.hasToy = true; catMemory.toy = target; catMemory.toyFrom = player; catMemory.toyAge = 0;
                catMemory.toyCooldown = 14; catMemory.distractions++; catMemory.cooldown = 24; cat.state = "toy"; cat.timer = 16; cat.target = target;
                Say("玩具球滚过去了，猫的注意力被引开了。", "good"); Emit("catToy", 6, target); return true;
            }
            Say("前面没有安全落点，转向空地再丢玩具球。", "hint"); return false;
        }

        void CatVoice(string kind, float strength = 4)
        {
            if (time - catMemory.lastVoice <= 5) return;
            Emit(kind, strength, cat.position); catMemory.lastVoice = time;
        }

        void TickCat(float dt)
        {
            if (cat == null || (mode != null && (mode.type == "catch" || mode.type == "reaction"))) return;
            var memory = catMemory;
            memory.repath = Mathf.Max(0, memory.repath - dt); memory.cooldown = Mathf.Max(0, memory.cooldown - dt); memory.toyCooldown = Mathf.Max(0, memory.toyCooldown - dt);
            if (memory.hasToy) { memory.toyAge += dt; if (memory.toyAge > 18) memory.hasToy = false; }
            if (levelIndex == 3)
            {
                if (cat.state == "calm") { cat.timer -= dt; if (cat.timer <= 0) cat.state = "idle"; }
                return;
            }
            float distance = FlatDistance(cat.position, player); bool visible = !Occluded(cat.position, player), moving = velocity.magnitude > .12f;
            if (cat.state == "calm" || cat.state == "play") { cat.timer -= dt; if (cat.timer <= 0) { cat.state = "idle"; memory.still = 0; } return; }
            if (cat.state == "jump")
            {
                if (mode != null) return;
                cat.timer += dt;
                if (cat.timer >= .55f)
                {
                    cat.state = "calm"; cat.timer = 12; memory.cooldown = 24; memory.route.Clear();
                    if (vaseState == "stable") { BeginIncident("vase"); Say("猫扑上桌沿，尾巴扫到了花瓶！对准落点，双手托住。", "warning"); }
                }
                return;
            }
            if (cat.state == "approach" || cat.state == "prepare")
            {
                if (vaseState != "stable" || FlatDistance(player, VasePosition()) > 5.5f) { cat.state = "idle"; memory.route.Clear(); memory.cooldown = 15; return; }
                if (cat.state == "prepare")
                {
                    if (mode != null) return;
                    cat.timer -= dt; CatVoice("catChirp");
                    if (cat.timer <= 0) { cat.state = "jump"; cat.timer = 0; cat.jumpUsed = true; Emit("catHop", 8, cat.position); }
                    return;
                }
            }
            if (cat.state == "toy")
            {
                cat.timer -= dt; if (cat.timer <= 0) { cat.state = "idle"; memory.route.Clear(); memory.cooldown = 10; return; }
            }
            else if (cat.state != "approach")
            {
                if (!cat.jumpUsed && memory.cooldown <= 0 && vaseState == "stable" && mode == null && visible && FlatDistance(player, VasePosition()) < 3.8f && FlatDistance(cat.position, VasePosition()) < 3.5f)
                {
                    cat.state = "approach"; cat.target = new Vector3(10.5f, 0, 3); memory.route = FindPath(cat.position, cat.target, true, .2f);
                    Say("猫盯上了花瓶，正在找起跳的位置。靠近 E 安抚，或 Q 丢玩具。", "warning"); CatVoice("catChirp");
                }
                else if (distance < 4.5f && visible)
                {
                    if (!cat.met) { cat.met = true; Say("猫发现你了。它会跟随；靠近 E 安抚，Q 丢玩具球引开。", "hint"); CatVoice("catMeow"); }
                    if (distance < 1 && !moving && mode == null) { memory.still += dt; if (memory.still > .7f) { cat.state = "rub"; memory.route.Clear(); CatVoice("catPurr"); return; } }
                    else memory.still = 0;
                    cat.state = "follow";
                    if (distance < .85f) { memory.route.Clear(); return; }
                    if (memory.repath <= 0 && (FlatDistance(cat.target, player) > .7f || memory.route.Count == 0))
                    { cat.target = player; memory.route = FindPath(cat.position, cat.target, true, .2f); memory.repath = 1.2f; }
                }
                else { cat.state = "idle"; memory.route.Clear(); memory.still = 0; return; }
            }
            if (memory.route.Count > 0)
            {
                Vector3 target = memory.route[0]; float distanceToNext = FlatDistance(cat.position, target);
                float step = Mathf.Min(distanceToNext, (cat.state == "toy" ? 2.1f : cat.state == "approach" ? 1.1f : 1.45f) * dt);
                if (distanceToNext > .001f)
                {
                    Vector3 next = Vector3.MoveTowards(cat.position, target, step);
                    if (!CanOccupy(next)) { memory.route.Clear(); memory.repath = 0; return; }
                    cat.heading = Mathf.Atan2(target.x - cat.position.x, target.z - cat.position.z); cat.position = next;
                }
                if (distanceToNext <= step + .001f) memory.route.RemoveAt(0);
            }
            if (memory.route.Count == 0)
            {
                if (cat.state == "toy" && memory.hasToy && FlatDistance(cat.position, memory.toy) < .55f) { cat.state = "play"; cat.timer = 12; CatVoice("catPurr"); }
                else if (cat.state == "approach" && FlatDistance(cat.position, new Vector3(10.5f, 0, 3)) < .35f)
                {
                    cat.state = "prepare"; cat.timer = 3.5f; cat.heading = Mathf.Atan2(VasePosition().x - cat.position.x, VasePosition().z - cat.position.z);
                    Say("猫压低了身子——要扑花瓶了！E 安抚，Q 丢玩具，或退开。", "warning");
                }
            }
        }

        Vector3 TicklePosition() { return level.parentBed + new Vector3(0, 0, .72f); }

        public bool TickleNear()
        {
            return (levelIndex != 3 || tutorialStep >= 10) && parent.state == "sleep" && FlatDistance(player, TicklePosition()) < 1.15f && !Occluded(player, level.parentBed + new Vector3(0, 0, 1.16f));
        }

        void TickTickle(float dt, NightInput input, NightMode current)
        {
            if (parent.state != "sleep" && parent.state != "alert") { mode = null; return; }
            float pressure = RightPressure(input, .28f);
            float stroke = input.analog ? Mathf.Clamp01(Mathf.Abs(input.look.x)) : input.actionHeld ? .6f : 0;
            current.force = pressure; current.progress = Mathf.Clamp01(tickleHeat / 1.65f);
            bool moving = pressure > .035f && stroke > .08f;
            current.phase = moving ? "stroke" : "rest";
            current.hint = parent.state == "alert" ? "鼾声停了，脚缩了回去……现在可以松手或 Esc 收手。" : moving ? "脚趾蜷了一下，嘴角忍不住上扬。" : "手停在被子边，先听听呼吸。按住 E 轻挠 · Esc 收手";
            if (!moving) return;
            tickleHeat += pressure * (.35f + stroke * .75f) * dt; current.hold += stroke * dt;
            if (current.elapsed - interactionSoundAt > .7f) { interactionSoundAt = current.elapsed; Emit("cloth", 2, TicklePosition()); }
            if (tickleHeat > .3f && current.elapsed - current.lift > 1.4f) { current.lift = current.elapsed; Emit("parentGiggle", 8 + pressure * 16, level.parentBed); quiet = 0; }
            if (tickleHeat >= .85f && parent.state == "sleep")
            {
                parent.alert = Mathf.Max(parent.alert, 32); parent.state = "alert";
                Say("鼾声突然停了，父母缩起脚。再挠可能会醒，松手或 Esc 收手。", "warning"); Emit("bed", 25, level.parentBed);
            }
            if (tickleHeat >= 1.65f) { parent.alert = Mathf.Max(parent.alert, 60); parent.target = player; BeginWarning(); mode = null; }
        }
    }
}
