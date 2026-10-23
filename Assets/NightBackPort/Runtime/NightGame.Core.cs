// @role Runs the night, parent awareness and return-home goal
// @calls NightClock NightAcoustics NightNavigation NightData
// @tune RecognitionTime:float:0.25-0.25:Baseline seconds of sustained recognition before being caught; not changed.
// @tune ParentWarningSeconds:float:5-5:Baseline warning before a parent leaves bed; not changed.

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    [Serializable] public partial class NightGame
    {
        public const float RecognitionTime = .25f;
        public const float ParentWarningSeconds = 5f;
        public int levelIndex;
        public NightLevel level;
        public Vector3 player, velocity, inspectionTarget;
        public float heading = Mathf.PI;
        public bool crouched, hasDevice, active;
        public float[] firstNightHintProgress; // Optional first-night writing progress, serialized with the run.
        public string status = "ready";
        public float time, realTime, noise, noiseAge = 100, quiet, worldDelta;
        public NightParent parent;
        public List<NightDoor> doors = new List<NightDoor>();
        public List<NightSpot> spots = new List<NightSpot>();
        public NightMode mode;
        public NightMetrics metrics = new NightMetrics();
        public NightClock clock;
        public List<NightEvent> events = new List<NightEvent>();
        public List<string> history = new List<string>();
        public string toast = "";
        public float toastLeft, lastSnore = -9, lastWasher = -10, doorSeconds, quietDoorSeconds;
        public uint seed;
        public float lastHeard, lastMaskFactor = 1, walked, lastParentFoot, scanHeading;
        public int visits, exposures, openingDoor = -1, retreatDoor = -1, lastQuietPatrolCycle = -1;
        public List<Vector3> retreatRoute = new List<Vector3>();
        public bool IsTutorial { get { return levelIndex == 3; } }
        public bool IsCinematic { get { return mode != null && (mode.type == "catch" || mode.type == "reaction"); } }

        public NightGame(int index = 0, float departure = 0)
        {
            levelIndex = Mathf.Clamp(index, 0, 3); level = NightLevel.Load(levelIndex); player = level.home; player.y = 0;
            clock = new NightClock(departure); seed = unchecked((uint)DateTime.UtcNow.Ticks);
            inspectionTarget = new Vector3(3, 0, 9);
            parent = new NightParent { position = level.parentHome, target = inspectionTarget, lastRetarget = -10 };
            if (IsTutorial) doors.Add(new NightDoor { name = "练习门", x = 3, z = 12 });
            else { doors.Add(new NightDoor { name = "卧室门", x = 3, z = 10 }); doors.Add(new NightDoor { name = "旧木门", x = 11, z = 6 }); }
            if (level.spots != null) for (int i = 0; i < level.spots.Length; i++)
            {
                var s = level.spots[i]; spots.Add(new NightSpot { id = s.id, name = s.name, x = s.x, z = s.z, device = i == level.device, unlocked = s.unlocked });
            }
            ResetInteractions();
        }

        public void Start()
        {
            if (status == "won" || status == "lost") return;
            active = true; status = "playing"; velocity = Vector3.zero;
            Say(IsTutorial ? "走到暖光里的门把前。先学基础动作，再自己运用。" : "先听屋里的动静，再去房间找设备。时间改变作息，等一等不会扣分。", "hint");
        }
        public void Say(string message, string kind = "info")
        {
            toast = message; toastLeft = 5;
            events.Add(new NightEvent { kind = kind, text = message, time = time, position = player });
            if (kind != "snore" && kind != "hint") { history.Add((clock == null ? "" : clock.displayTime + "  ") + message); while (history.Count > 12) history.RemoveAt(0); }
            TrimEvents();
        }
        public void Emit(string kind, float strength, Vector3 source)
        {
            events.Add(new NightEvent { kind = kind, strength = strength, position = source, time = time }); TrimEvents();
        }
        void TrimEvents() { if (events.Count > 256) events.RemoveRange(0, events.Count - 256); }
        public void MakeNoise(float amount, string label, string kind, Vector3 source)
        {
            kind = string.IsNullOrEmpty(kind) ? amount > 15 ? "creak" : "step" : kind;
            var hearing = HearingProfile(amount, kind, source); float masked = amount * hearing.factor;
            if (masked > 10) { metrics.loudSounds++; metrics.noiseBurden += masked - 10; }
            noise = masked; noiseAge = 0; quiet = 0; ApplyHearing(hearing, source, false);
            if (masked > 10 && !string.IsNullOrEmpty(label)) Say(label, "noise"); Emit(kind, amount, source);
        }
        public void HearNoise(float amount, string kind, Vector3 source, bool force = false)
        {
            if (force) quiet = 0; ApplyHearing(HearingProfile(amount, kind, source), source, force);
        }
        void ApplyHearing(NightHearing hearing, Vector3 source, bool force)
        {
            lastHeard = hearing.heard; lastMaskFactor = hearing.factor; parent.alert = Mathf.Clamp(parent.alert + hearing.heard, 0, 100);
            if (hearing.heard >= 6)
            {
                inspectionTarget = new Vector3(source.x, 0, source.z);
                if ((parent.state == "checking" || parent.state == "returning") && time - parent.lastRetarget > 1.2f)
                {
                    parent.state = "checking"; parent.intent = "investigate"; parent.itinerary.Clear(); parent.lastRetarget = time; SetDestination(inspectionTarget);
                }
                else if (parent.state == "warning") { parent.intent = "investigate"; parent.itinerary.Clear(); parent.itinerary.Add(inspectionTarget); }
                else if (parent.state == "sleep" || parent.state == "alert") parent.target = inspectionTarget;
            }
            if (force && hearing.heard >= 6 && (parent.state == "sleep" || parent.state == "alert")) parent.alert = Mathf.Max(60, parent.alert);
        }

        public void Tick(float dt, NightInput input)
        {
            if (!active || status != "playing" || dt <= 0 || float.IsNaN(dt) || float.IsInfinity(dt)) return;
            float realDelta = Mathf.Min(dt, .06f); bool cinematic = IsCinematic; realTime += realDelta;
            worldDelta = realDelta * (cinematic ? .12f : 1f); time += worldDelta;
            quiet += worldDelta; noiseAge += worldDelta; toastLeft = Mathf.Max(0, toastLeft - realDelta);
            clock.Tick(worldDelta);
            if (input.cancel) CancelInteraction();
            if (input.crouchPressed && mode == null)
            {
                crouched = !crouched; Say(crouched ? "蹲低慢行。要靠墙和家具挡住身体，空地上仍然会被看见。" : "站起来了。脚步更快，也更响。", "hint"); Emit("cloth", 8, player);
            }
            if (input.actionPressed && mode == null) TryInteract();
            if (mode == null) MovePlayer(input.move, worldDelta); else velocity = Vector3.zero;
            TickClock();
            TickParent(worldDelta, cinematic);
            if (status != "playing") return;
            TickInteraction(realDelta, input);
            CheckWin();
        }
        void MovePlayer(Vector2 input, float dt)
        {
            float magnitude = input.magnitude, speed = crouched ? 1.05f : 2.05f;
            var desired = new Vector3(input.x, 0, input.y) / Mathf.Max(1, magnitude) * speed;
            velocity = Vector3.Lerp(velocity, desired, 1 - Mathf.Exp(-(magnitude > .001f ? 24 : 36) * dt));
            if (velocity.magnitude < .005f) { velocity = Vector3.zero; return; }
            Vector3 next = player, step = velocity * dt;
            var candidate = new Vector3(next.x + step.x, 0, next.z); if (CanOccupy(candidate)) next = candidate; else velocity.x = 0;
            candidate = new Vector3(next.x, 0, next.z + step.z); if (CanOccupy(candidate)) next = candidate; else velocity.z = 0;
            float traveled = NightGeometry.Distance(player, next);
            if (traveled <= .00001f) return;
            if (TryFloorStep(next)) { velocity = Vector3.zero; return; }
            player = next; walked += traveled;
            if (velocity.sqrMagnitude > .00001f) heading = Mathf.Atan2(velocity.x, velocity.z);
            if (walked >= .72f) { walked %= .72f; MakeNoise(crouched ? 1 : 3, "轻轻落脚。", crouched ? "crouchStep" : "step", player); }
        }
        void CheckWin()
        {
            if (!IsTutorial && hasDevice && player.z >= 11 && player.x <= 6 && status == "playing") Finish(true, "安全带着设备回到卧室。今晚的时间，拿回来了。");
        }
        public void Finish(bool won, string reason)
        {
            if (status == "won" || status == "lost") return;
            status = won ? "won" : "lost"; active = false; mode = null; velocity = Vector3.zero;
            Say(reason, won ? "good" : "danger"); Emit(won ? "win" : "lose", 50, player); EvaluatePerformance();
        }
        public void EvaluatePerformance()
        {
            float incidents = metrics.rescued + metrics.failedRescues;
            float actions = metrics.steps + incidents + (doorSeconds > 0 ? 1 : 0);
            float good = metrics.goodSteps + metrics.rescued + (doorSeconds > 0 ? Mathf.Clamp01(quietDoorSeconds / doorSeconds) : 0);
            int mission = status == "won" ? 40 : 0, sound = Mathf.RoundToInt(25 * Mathf.Max(0, 1 - metrics.noiseBurden / 160));
            int action = Mathf.RoundToInt(25 * (actions > 0 ? Mathf.Clamp01(good / actions) : 1)), sight = Mathf.Max(0, 10 - exposures * 5);
            metrics.score = mission + sound + action + sight; metrics.grade = status != "won" ? "—" : metrics.score >= 90 ? "S" : metrics.score >= 75 ? "A" : metrics.score >= 60 ? "B" : "C";
        }

        void TickClock()
        {
            if (IsTutorial) return;
            int absolutePhase = clock.cycle * 4 + clock.phaseIndex;
            if (clock.announcedPhase != absolutePhase)
            {
                bool first = clock.announcedPhase < 0; clock.announcedPhase = absolutePhase;
                if (clock.phaseIndex == 1) Say("洗衣机开始脱水。靠近同一声场时，可以借它掩住落脚；隔墙是否有效，要亲耳判断。", "time");
                else if (clock.phaseIndex == 2)
                {
                    Say("洗衣机停了，走廊灯亮起。父母到了喝水的时段，先看清路线再动。", "time");
                    if (parent.state == "sleep" || parent.state == "alert") BeginWarning(true);
                }
                else if (clock.phaseIndex == 3)
                {
                    Say("走廊熄灯，远处更难看清。近距离和无遮掩的位置仍然危险。", "time");
                    if (parent.state == "checking" && parent.intent == "schedule") { parent.state = "returning"; parent.intent = "return"; parent.itinerary.Clear(); SetDestination(level.parentHome); }
                }
                else if (!first) Say("新一轮作息开始。安静等待不扣分，先听呼吸是否平稳。", "time");
            }
            if (clock.inspectionSoon && clock.warnedCycle != clock.cycle)
            {
                clock.warnedCycle = clock.cycle; Say("洗衣声快结束了。父母稍后会开灯喝水，可以先选好遮挡和返程路线。", "warning"); Emit("clock", 8, WasherPosition);
            }
            // 第三夜在入睡时段多一次短检查；与整点喝水共用调度，不在返床后重复堆叠。
            if (levelIndex == 2 && clock.phaseIndex == 0 && clock.phaseAge >= 32 && lastQuietPatrolCycle != clock.cycle)
            {
                lastQuietPatrolCycle = clock.cycle;
                if (parent.state == "sleep" || parent.state == "alert") BeginWarning(true);
            }
            if (clock.washerActive && time - lastWasher >= 1.6f) { lastWasher = time; Emit("washer", 30, WasherPosition); }
        }
        public List<Vector3> ScheduledRoute()
        {
            var route = new List<Vector3>();
            // 三夜的空间约束不同：客厅短检查、厨房往返、洗衣后廊环线。
            if (clock.phaseIndex == 0) { route.Add(new Vector3(3, 0, 9)); route.Add(new Vector3(11, 0, 9)); return route; }
            if (levelIndex == 0) { route.Add(new Vector3(3, 0, 9)); route.Add(new Vector3(3, 0, 4)); route.Add(new Vector3(11, 0, 3)); }
            else if (levelIndex == 1) { route.Add(new Vector3(11, 0, 9)); route.Add(new Vector3(21, 0, 3)); route.Add(new Vector3(16, 0, 3)); }
            else if (clock.cycle % 2 == 0) { route.Add(new Vector3(11, 0, 9)); route.Add(new Vector3(11, 0, 16)); route.Add(new Vector3(21, 0, 12)); route.Add(new Vector3(21, 0, 3)); }
            else { route.Add(new Vector3(3, 0, 9)); route.Add(new Vector3(16, 0, 3)); route.Add(new Vector3(21, 0, 12)); route.Add(new Vector3(11, 0, 16)); }
            return route;
        }
        public void BeginWarning(bool scheduled = false)
        {
            if (IsTutorial || parent.state != "sleep" && parent.state != "alert") return;
            parent.intent = scheduled && parent.alert < 60 ? "schedule" : "investigate";
            parent.itinerary = parent.intent == "schedule" ? ScheduledRoute() : new List<Vector3> { parent.target };
            if (parent.itinerary.Count == 0) parent.itinerary.Add(inspectionTarget);
            parent.state = "warning"; parent.phase = "rest"; parent.timer = ParentWarningSeconds;
            Say("床板响了……父母翻身，准备起床。还有时间停下或躲藏。", "warning"); Emit("bed", 55, level.parentBed);
        }
        public void SetDestination(Vector3 target)
        {
            parent.target = target; parent.route = FindPath(parent.position, target, false, .24f); parent.phase = parent.route.Count > 0 ? "walk" : "scan";
            parent.timer = parent.intent == "investigate" ? 3.5f : 2.6f; scanHeading = parent.heading;
        }
        void TickParent(float dt, bool cinematic)
        {
            if (quiet > 3) parent.alert = Mathf.Max(0, parent.alert - dt * 2.1f);
            if (parent.state == "sleep" && parent.alert >= 30) { parent.state = "alert"; Say("鼾声停了。父母翻了个身，先别急。", "warning"); Emit("bed", 35, level.parentBed); }
            if (parent.state == "alert" && parent.alert < 20) { parent.state = "sleep"; Say("鼾声重新响起，房间慢慢安静下来。", "good"); }
            if ((parent.state == "sleep" || parent.state == "alert") && parent.alert >= 60) BeginWarning();
            if (parent.state == "sleep" && time - lastSnore > 6) { lastSnore = time; Emit("snore", 25, level.parentBed); }
            if (IsTutorial) return;
            if (parent.state == "warning")
            {
                parent.timer -= dt;
                if (parent.timer <= 0)
                {
                    parent.state = "checking"; var target = parent.itinerary.Count > 0 ? parent.itinerary[0] : inspectionTarget;
                    if (parent.itinerary.Count > 0) parent.itinerary.RemoveAt(0); SetDestination(target); Say("咔哒。脚步从父母房间出来了。", "warning");
                }
            }
            if (parent.state == "checking" || parent.state == "returning")
            {
                Patrol(dt);
                if (!cinematic && Visible())
                {
                    if (parent.recognition <= 0) { exposures++; Say("灯光停在你身上——赶快离开视线！", "danger"); Emit("notice", 60, parent.position); }
                    parent.recognition += dt; metrics.exposure += dt;
                    if (parent.recognition >= RecognitionTime) Finish(false, "父母看清了你。留意刚才的声音、照明和遮挡，再试一次。");
                }
                else if (!cinematic) parent.recognition = 0;
            }
        }
        public bool Visible()
        {
            float d = NightGeometry.Distance(player, parent.position);
            float range = clock.phaseIndex == 3 ? 3.8f : clock.phaseIndex == 2 ? 5.6f : 5.2f;
            if (d > range || Occluded(player, parent.position, crouched)) return false;
            float bearing = Mathf.Atan2(player.x - parent.position.x, player.z - parent.position.z);
            float diff = Mathf.DeltaAngle(parent.heading * Mathf.Rad2Deg, bearing * Mathf.Rad2Deg) * Mathf.Deg2Rad;
            return d < 1.2f || Mathf.Abs(diff) < .85f;
        }
        void Footstep()
        {
            if (time - lastParentFoot > .7f) { lastParentFoot = time; Emit("parentStep", 40, parent.position); }
        }
        bool ClearDoorSwing(NightDoor door, float dt)
        {
            if (!DoorSweepHits(door, 0, 1, parent.position, .28f))
            {
                if (retreatDoor >= 0) { retreatDoor = -1; retreatRoute.Clear(); SetDestination(parent.target); } return true;
            }
            int id = doors.IndexOf(door);
            if (retreatDoor != id || retreatRoute.Count == 0)
            {
                retreatDoor = id; float side = parent.position.z < door.z ? -1 : 1, best = float.PositiveInfinity; retreatRoute.Clear();
                foreach (float sign in new[] { side, -side }) foreach (float x in new[] { door.x, door.x + .5f, door.x - .5f })
                {
                    Vector3 target = new Vector3(x, 0, door.z + sign * 1.5f); var route = FindPath(parent.position, target, true, .24f);
                    if (route.Count == 0 || NightGeometry.Distance(route[route.Count - 1], target) >= .1f || DoorSweepHits(door, 0, 1, target, .28f)) continue;
                    float length = sign == side ? 0 : 1; var previous = parent.position; foreach (var p in route) { length += NightGeometry.Distance(previous, p); previous = p; }
                    if (length < best) { best = length; retreatRoute = route; }
                }
            }
            parent.phase = "doorYield";
            if (retreatRoute.Count == 0) return false;
            Vector3 destination = retreatRoute[0]; float distance = NightGeometry.Distance(parent.position, destination), step = Mathf.Min(distance, .95f * dt);
            var next = distance > 0 ? Vector3.MoveTowards(parent.position, destination, step) : parent.position;
            if (!CanOccupy(next, .24f)) { retreatRoute.Clear(); return false; }
            if (distance > .001f) parent.heading = Mathf.Atan2(next.x - parent.position.x, next.z - parent.position.z);
            parent.position = next; if (step >= distance - .001f) retreatRoute.RemoveAt(0); Footstep(); return false;
        }
        void Patrol(float dt)
        {
            NightDoor heldDoor = null;
            if (mode != null && mode.type == "door")
            {
                if (mode.index >= 0 && mode.index < doors.Count) heldDoor = doors[mode.index];
                if (!string.IsNullOrEmpty(mode.id)) foreach (var d in doors) if (d.name == mode.id) { heldDoor = d; break; }
            }
            if (heldDoor != null && !ClearDoorSwing(heldDoor, dt)) return;
            if (retreatDoor >= 0 && heldDoor == null && openingDoor < 0) { retreatDoor = -1; retreatRoute.Clear(); SetDestination(parent.target); }
            if (openingDoor >= 0 && openingDoor < doors.Count)
            {
                var door = doors[openingDoor]; parent.phase = "opening";
                if (door == heldDoor) { parent.phase = "doorWait"; return; }
                if (!ClearDoorSwing(door, dt)) return;
                door.direction = 1; float next = Mathf.Clamp01(door.progress + dt * .72f);
                if (!DoorSweepHits(door, door.progress, next, player, .20f) && !DoorSweepHits(door, door.progress, next, parent.position, .24f)) door.progress = next;
                else { parent.phase = "doorWait"; return; }
                if (door.progress >= 1) { openingDoor = -1; Emit("latch", 12, door.Position); SetDestination(parent.target); } return;
            }
            if (parent.route.Count > 0)
            {
                Vector3 destination = parent.route[0], delta = destination - parent.position; float distance = NightGeometry.Distance(parent.position, destination), desiredHeading = Mathf.Atan2(delta.x, delta.z);
                parent.heading = Mathf.MoveTowardsAngle(parent.heading * Mathf.Rad2Deg, desiredHeading * Mathf.Rad2Deg, dt * 3 * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                float step = Mathf.Min(distance, (parent.state == "returning" ? 1.15f : .95f) * dt); Vector3 next = Vector3.MoveTowards(parent.position, destination, step);
                if (heldDoor != null && DoorSweepHits(heldDoor, 0, 1, next, .29f)) { parent.phase = "doorWait"; return; }
                Vector3 ahead = parent.position + (distance > .0001f ? delta / distance * .38f : Vector3.zero);
                for (int i = 0; i < doors.Count; i++)
                {
                    var door = doors[i];
                    if (door.progress < .999f && NightGeometry.Distance(door.Position, parent.position) < 1.15f && DoorHits(door, ahead, .24f))
                    { openingDoor = i; parent.phase = "opening"; Emit("doorSoft", 15, door.Position); return; }
                }
                if (!CanOccupy(next, .24f)) { parent.phase = "scan"; SetDestination(parent.target); return; }
                parent.phase = "walk"; parent.position = next; if (distance <= step + .001f) parent.route.RemoveAt(0); Footstep();
                if (parent.route.Count == 0) { parent.phase = "scan"; parent.timer = parent.intent == "investigate" ? 3.5f : 2.6f; scanHeading = parent.heading; }
            }
            else if (parent.state == "checking")
            {
                parent.phase = "scan"; parent.timer -= dt; parent.heading = scanHeading + Mathf.Sin((parent.intent == "investigate" ? 3.5f : 2.6f) - parent.timer) * .8f;
                if (parent.timer <= 0)
                {
                    if (parent.itinerary.Count > 0) { var target = parent.itinerary[0]; parent.itinerary.RemoveAt(0); SetDestination(target); }
                    else { parent.state = "returning"; parent.intent = "return"; SetDestination(level.parentHome); Say("脚步开始远去。等它真正离开，再继续。", "good"); }
                }
            }
            else if (NightGeometry.Distance(parent.position, level.parentHome) < .12f)
            {
                parent.state = "sleep"; parent.phase = "rest"; parent.heading = 0; parent.alert = 12; parent.recognition = 0; visits++; Say("卧室重新传来鼾声。", "good");
            }
            else { parent.phase = "scan"; parent.timer -= dt; if (parent.timer <= 0) SetDestination(level.parentHome); }
        }

        // JsonUtility 不运行构造函数；续玩只重建派生缓存，不重新随机事件或重置成绩。
        public bool RepairAfterLoad()
        {
            if (levelIndex < 0 || levelIndex > 3 || parent == null || doors == null || spots == null || time < 0 || float.IsNaN(time) || float.IsInfinity(time)) return false;
            level = NightLevel.Load(levelIndex); navigation = null; acoustics = null;
            if (doors.Count != (IsTutorial ? 1 : 2) || spots.Count != level.spots.Length) return false;
            if (cat == null || catMemory == null || phone == null || locks == null || lures == null) return false;
            foreach (var door in doors) if (door == null || float.IsNaN(door.progress) || float.IsInfinity(door.progress) || door.progress < 0 || door.progress > 1) return false;
            for (int i = 0; i < spots.Count; i++) if (spots[i] == null || spots[i].id != level.spots[i].id) return false;
            if (!CanOccupy(player) || !Nav.CanStand(parent.position, .24f)) return false;
            if (clock == null) clock = new NightClock { elapsedSeconds = time };
            if (clock.departureSeconds < 0 || clock.elapsedSeconds < 0 || float.IsNaN(clock.totalSeconds) || float.IsInfinity(clock.totalSeconds)) return false;
            // Unity's inline serializer writes null custom classes as empty value objects.
            // An empty mode must mean free movement; otherwise Tick treats it as an
            // interaction forever and the loaded player can never move.
            if (mode != null && string.IsNullOrEmpty(mode.type)) mode = null;
            if (lockState != null && string.IsNullOrEmpty(lockState.id)) lockState = null;
            if (rescue != null && string.IsNullOrEmpty(rescue.kind)) rescue = null;
            locks.RemoveAll(value => value == null || string.IsNullOrEmpty(value.id));
            if (events == null) events = new List<NightEvent>(); else events.Clear();
            if (history == null) history = new List<string>(); if (metrics == null) metrics = new NightMetrics();
            if (parent.route == null) parent.route = new List<Vector3>(); if (parent.itinerary == null) parent.itinerary = new List<Vector3>(); if (retreatRoute == null) retreatRoute = new List<Vector3>();
            // JsonUtility 将同一锁对象的两条引用分别反序列化；续玩重新接回锁列表，避免收手后丢失新卡好的弹子。
            if (lockState != null)
            {
                int index = locks.FindIndex(value => value != null && value.id == lockState.id);
                if (index >= 0) locks[index] = lockState; else locks.Add(lockState);
            }
            active = false; velocity = Vector3.zero; worldDelta = 0; ReleaseInteractionInputs();
            return true;
        }
    }
}
