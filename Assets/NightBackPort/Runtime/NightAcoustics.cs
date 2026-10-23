// @role Traces sound through rooms, doors and masking sources
// @calls NightNavigation NightData

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    public sealed class NightSoundPath
    {
        public Vector3 bands, arrival, directBands, routedBands, reflectionBands;
        public float gain, distance, delay;
        public bool blocked;
        public int walls, doors;
        public string room, route;
    }
    public struct NightHearing { public float heard, factor; public NightSoundPath path; public Vector3 signal; }
    public struct NightEnvironmentSound
    {
        public string kind, name; public Vector3 position; public float age, duration, gain; public bool active, cover;
    }

    // 网页 v0.14.6 的三频段游戏声学近似。不是物理声压标定或完整声波求解器。
    public sealed class NightAcoustics
    {
        public static readonly Vector3 WallTransmission = new Vector3(.13f, .045f, .012f);
        public static readonly Vector3 DoorTransmission = new Vector3(.30f, .12f, .035f);
        static readonly Vector3 WoodTransmission = new Vector3(.65f, .38f, .18f), SoftTransmission = new Vector3(.40f, .20f, .075f);
        readonly NightGame game;
        readonly List<NightBox> walls = new List<NightBox>(), furniture = new List<NightBox>();
        readonly List<Portal> portals = new List<Portal>();
        readonly List<List<int>> links = new List<List<int>>();
        readonly Dictionary<string, NightSoundPath> cache = new Dictionary<string, NightSoundPath>();
        string doorVersion;
        struct Portal { public Vector3 position; public int opening; }
        struct TraceResult { public Vector3 bands; public int walls, doors, objects; }
        struct Path { public Vector3 bands, arrival; public float distance; }
        public NightAcoustics(NightGame owner)
        {
            game = owner;
            if (game.level.walls != null) foreach (var p in game.level.walls) walls.Add(new NightBox(p.x, p.y, 1, 1));
            if (game.level.furniture != null) foreach (var f in game.level.furniture)
                if (f.type != "lamp" && f.type != "plant" && f.type != "table" && f.type != "desk" && f.type != "chair") furniture.Add(new NightBox(f));
            if (game.level.openings != null) for (int i = 0; i < game.level.openings.Length; i++)
            {
                var o = game.level.openings[i]; if (o.x >= game.level.width - 1) continue;
                var offset = o.axis == "z" ? new Vector3(.56f, 0, 0) : new Vector3(0, 0, .56f);
                var center = new Vector3(o.x, 1.35f, o.z);
                portals.Add(new Portal { position = center - offset, opening = i }); portals.Add(new Portal { position = center + offset, opening = i });
            }
            for (int i = 0; i < portals.Count; i++) links.Add(new List<int>());
            for (int i = 0; i < portals.Count; i++) for (int j = i + 1; j < portals.Count; j++)
                if (WallHits(portals[i].position, portals[j].position) == 0) { links[i].Add(j); links[j].Add(i); }
        }
        public static Vector3 Multiply(Vector3 a, Vector3 b) { return new Vector3(a.x * b.x, a.y * b.y, a.z * b.z); }
        public static Vector3 Square(Vector3 a) { return Multiply(a, a); }
        public static Vector3 Root(Vector3 a) { return new Vector3(Mathf.Sqrt(Mathf.Max(0, a.x)), Mathf.Sqrt(Mathf.Max(0, a.y)), Mathf.Sqrt(Mathf.Max(0, a.z))); }
        public static float BandLevel(Vector3 bands) { return Mathf.Sqrt(bands.x * bands.x * .25f + bands.y * bands.y * .5f + bands.z * bands.z * .25f); }
        static Vector3 FreeField(float distance) { float spread = 1 / Mathf.Max(1, distance); return new Vector3(spread, spread * Mathf.Exp(-distance * .001f), spread * Mathf.Exp(-distance * .006f)); }
        static bool Intersects(Vector3 a, Vector3 b, NightBox box) { Vector2 interval; return NightGeometry.Interval(a, b, box, out interval); }
        int WallHits(Vector3 a, Vector3 b)
        {
            var hits = new List<Vector2>(); Vector2 interval;
            foreach (var box in walls) if (NightGeometry.Interval(a, b, box, out interval)) hits.Add(interval);
            hits.Sort((x, y) => x.x.CompareTo(y.x)); int count = 0; float end = -1;
            foreach (var hit in hits) { if (hit.x > end + .00001f) count++; end = Mathf.Max(end, hit.y); } return count;
        }
        TraceResult Trace(Vector3 a, Vector3 b, NightDoor ignoreDoor = null, bool includeFurniture = true)
        {
            int count = WallHits(a, b); var result = new TraceResult { walls = count, bands = new Vector3(Mathf.Pow(WallTransmission.x, count), Mathf.Pow(WallTransmission.y, count), Mathf.Pow(WallTransmission.z, count)) };
            foreach (var d in game.doors) if (d != ignoreDoor && Intersects(a, b, NightGeometry.DoorShape(d))) { result.bands = Multiply(result.bands, DoorTransmission); result.doors++; }
            if (includeFurniture) foreach (var box in furniture)
            {
                var p = NightGeometry.LocalPoint(a, box); var q = NightGeometry.LocalPoint(b, box);
                if (Mathf.Abs(p.x) <= box.w / 2 + .04f && Mathf.Abs(p.z) <= box.d / 2 + .04f || Mathf.Abs(q.x) <= box.w / 2 + .04f && Mathf.Abs(q.z) <= box.d / 2 + .04f) continue;
                if (Intersects(a, b, box)) { result.bands = Multiply(result.bands, box.type == "bed" || box.type == "sofa" ? SoftTransmission : WoodTransmission); result.objects++; }
            }
            return result;
        }
        Path Edge(Vector3 a, Vector3 b, int firstPortal = -1, int secondPortal = -1, NightDoor ignoreDoor = null)
        {
            var bands = Vector3.one;
            bool bridge = firstPortal >= 0 && secondPortal >= 0 && portals[firstPortal].opening == portals[secondPortal].opening;
            if (bridge)
            {
                var o = game.level.openings[portals[firstPortal].opening];
                foreach (var d in game.doors) if (Mathf.Abs(d.x - o.x) < .001f && Mathf.Abs(d.z - o.z) < .001f)
                {
                    float gap = 1 - Mathf.Cos(d.progress * Mathf.PI * .49f); bands = Vector3.Lerp(DoorTransmission, Vector3.one, gap); break;
                }
            }
            else foreach (var d in game.doors) if (d != ignoreDoor && Intersects(a, b, NightGeometry.DoorShape(d))) bands = Multiply(bands, DoorTransmission);
            return new Path { bands = bands, distance = Vector3.Distance(a, b), arrival = a };
        }
        static float Cost(Path edge) { return edge.distance - 3 * Mathf.Log(Mathf.Max(.00001f, edge.bands.y)); }
        Path Around(Vector3 a, Vector3 b, NightDoor ignoreDoor)
        {
            int n = portals.Count; float[] dist = new float[n]; int[] previous = new int[n]; bool[] used = new bool[n];
            for (int i = 0; i < n; i++) { previous[i] = -1; dist[i] = WallHits(a, portals[i].position) == 0 ? Cost(Edge(a, portals[i].position, -1, i, ignoreDoor)) : float.PositiveInfinity; }
            for (int k = 0; k < n; k++)
            {
                int i = -1; for (int j = 0; j < n; j++) if (!used[j] && (i < 0 || dist[j] < dist[i])) i = j;
                if (i < 0 || float.IsInfinity(dist[i])) break; used[i] = true;
                foreach (int j in links[i]) { float cost = dist[i] + Cost(Edge(portals[i].position, portals[j].position, i, j)); if (cost < dist[j]) { dist[j] = cost; previous[j] = i; } }
            }
            int best = -1; float bestCost = float.PositiveInfinity;
            for (int i = 0; i < n; i++) if (WallHits(portals[i].position, b) == 0) { float cost = dist[i] + Cost(Edge(portals[i].position, b, i, -1)); if (cost < bestCost) { best = i; bestCost = cost; } }
            if (best < 0) return new Path { arrival = a };
            var points = new List<Vector3> { b }; var ids = new List<int> { -1 };
            for (int i = best; i >= 0; i = previous[i]) { points.Insert(0, portals[i].position); ids.Insert(0, i); }
            points.Insert(0, a); ids.Insert(0, -1);
            for (int i = 1; i < points.Count - 1;)
            {
                var trace = Trace(points[i - 1], points[i + 1], i == 1 ? ignoreDoor : null, false);
                if (trace.walls == 0 && trace.doors == 0) { points.RemoveAt(i); ids.RemoveAt(i); } else i++;
            }
            Vector3 bands = Vector3.one; float distance = 0, turn = 0;
            for (int i = 1; i < points.Count; i++)
            {
                var edge = Edge(points[i - 1], points[i], ids[i - 1], ids[i], i == 1 ? ignoreDoor : null); bands = Multiply(bands, edge.bands); distance += edge.distance;
                if (i < points.Count - 1)
                {
                    Vector2 u = new Vector2(points[i].x - points[i - 1].x, points[i].z - points[i - 1].z), v = new Vector2(points[i + 1].x - points[i].x, points[i + 1].z - points[i].z);
                    if (u.magnitude * v.magnitude > .001f) turn += Mathf.Acos(Mathf.Clamp(Vector2.Dot(u, v) / (u.magnitude * v.magnitude), -1, 1));
                }
            }
            bands = Multiply(Multiply(bands, FreeField(distance)), new Vector3(Mathf.Exp(-turn * .16f), Mathf.Exp(-turn * .45f), Mathf.Exp(-turn * .88f)));
            return new Path { bands = bands, distance = distance, arrival = points[points.Count - 2] };
        }
        string Room(Vector3 p)
        {
            string floor = game.FloorAt(p); bool bedroom = game.levelIndex != 3 && (p.x < 7 && p.z > 10 || p.x > 5 && p.x < 9 && p.z > 4 && p.z < 8);
            return floor == "carpet" || bedroom ? "soft" : floor == "tile" ? "tile" : "wood";
        }
        Path Reflection(Vector3 a, Vector3 b, TraceResult direct)
        {
            if (direct.walls > 0 || direct.doors > 0) return new Path { arrival = a };
            string room = Room(b); var coefficient = room == "soft" ? new Vector3(.20f, .12f, .06f) : room == "tile" ? new Vector3(.56f, .48f, .38f) : new Vector3(.42f, .30f, .20f);
            Vector3 mirror = a; mirror.y = -a.y; float distance = Vector3.Distance(mirror, b), fraction = b.y / Mathf.Max(.0001f, a.y + b.y);
            Vector3 bounce = Vector3.Lerp(b, a, fraction); bounce.y = .025f;
            if (WallHits(a, bounce) > 0 || WallHits(bounce, b) > 0) return new Path { arrival = a };
            return new Path { bands = Multiply(Multiply(FreeField(distance), coefficient), direct.bands), distance = distance, arrival = mirror };
        }
        public NightSoundPath Profile(Vector3 a, Vector3 b, NightDoor ignoreDoor = null)
        {
            string version = ""; foreach (var door in game.doors) version += Mathf.RoundToInt(door.progress * 100) + ",";
            if (version != doorVersion) { cache.Clear(); doorVersion = version; }
            string key = Mathf.RoundToInt(a.x * 25) + "," + Mathf.RoundToInt(a.y * 25) + "," + Mathf.RoundToInt(a.z * 25) + ";" + Mathf.RoundToInt(b.x * 25) + "," + Mathf.RoundToInt(b.y * 25) + "," + Mathf.RoundToInt(b.z * 25) + (ignoreDoor == null ? "" : ":d" + game.doors.IndexOf(ignoreDoor));
            NightSoundPath existing; if (cache.TryGetValue(key, out existing)) return existing;
            float directDistance = Vector3.Distance(a, b); var trace = Trace(a, b, ignoreDoor);
            var direct = new Path { bands = Multiply(trace.bands, FreeField(directDistance)), distance = directDistance, arrival = a };
            var routed = trace.walls > 0 || trace.doors > 0 ? Around(a, b, ignoreDoor) : new Path { arrival = a };
            var reflection = Reflection(a, b, trace);
            if (routed.distance > 0 && routed.distance - directDistance < .12f) routed.bands = Root(Square(routed.bands) - Square(direct.bands));
            Vector3 bands = Root(Square(direct.bands) + Square(routed.bands) + Square(reflection.bands));
            bool doorway = BandLevel(routed.bands) > BandLevel(direct.bands); var dominant = doorway ? routed : direct;
            var result = new NightSoundPath { bands = bands, directBands = direct.bands, routedBands = routed.bands, reflectionBands = reflection.bands, gain = BandLevel(bands), distance = dominant.distance, delay = dominant.distance / 343f, arrival = dominant.arrival, blocked = trace.walls > 0 || trace.doors > 0 || trace.objects > 0, walls = trace.walls, doors = trace.doors, route = doorway ? "doorway" : "direct", room = Room(b) };
            if (cache.Count > 256) cache.Clear(); cache[key] = result; return result;
        }
        static readonly Vector3 FloorBands = Mean(new[] { new Vector3(.030118f, .07754f, .047503f), new Vector3(.016043f, .057084f, .045428f), new Vector3(.036378f, .047186f, .036154f) });
        static readonly Vector3 WoodStep = Mean(new[] { new Vector3(.044916f, .00263f, .000102f), new Vector3(.136941f, .003049f, .000131f), new Vector3(.123089f, .011157f, .000185f), new Vector3(.027481f, .002545f, .000073f) });
        static readonly Vector3 CarpetStep = Mean(new[] { new Vector3(.018163f, .005187f, .002238f), new Vector3(.01177f, .004715f, .002113f), new Vector3(.005883f, .003998f, .001643f), new Vector3(.011432f, .006574f, .002462f) });
        static readonly Vector3 TileStep = Mean(new[] { new Vector3(.002956f, .003837f, .002337f), new Vector3(.011324f, .008196f, .002609f), new Vector3(.006522f, .006505f, .003775f), new Vector3(.00308f, .007703f, .003241f) });
        static Vector3 Mean(Vector3[] values) { Vector3 energy = Vector3.zero; foreach (var v in values) energy += Square(v); return Root(energy / values.Length); }
        public static Vector3 EmittedBands(string kind, float strength = 25, string surface = "wood")
        {
            if (kind == "snore") return new Vector3(.227131f, .010468f, .007028f) * .90f;
            if (kind == "washer") return new Vector3(.222719f, .050667f, .019872f) * .65f;
            if (kind.StartsWith("floor")) return FloorBands * (kind == "floorCreak" ? .82f : kind == "floorPressure" ? .12f : .27f * Mathf.Clamp(strength / 25, .02f, 1));
            if (kind == "step" || kind == "crouchStep" || kind == "tileStep" || kind == "parentStep")
                return (surface == "carpet" ? CarpetStep : surface == "tile" ? TileStep : WoodStep) * (kind == "crouchStep" ? .07f : kind == "parentStep" ? .92f : .27f) * (surface == "carpet" ? .58f : 1);
            Vector3 bands = kind == "doorBump" ? new Vector3(.027256f, .013997f, .011264f) : kind == "metalDrop" ? new Vector3(.002505f, .007716f, .043339f) : kind == "doorCreak" ? new Vector3(.011269f, .01434f, .002359f) : kind == "hingeMotion" ? new Vector3(.029079f, .041599f, .028847f) : new Vector3(.06f, .08f, .035f);
            return bands * (kind == "doorBump" ? 3 * (.18f + Mathf.Clamp01((strength - 24) / 66) * .82f) : Mathf.Max(.03f, strength / 50));
        }
    }

    public partial class NightGame
    {
        [NonSerialized] NightAcoustics acoustics;
        NightAcoustics Acoustics { get { if (acoustics == null) acoustics = new NightAcoustics(this); return acoustics; } }
        public Vector3 ListenerPosition { get { var p = player; p.y = crouched ? .86f : 1.35f; return p; } }
        public Vector3 ParentEar
        {
            get { float wake = parent.state == "warning" ? Mathf.Clamp01((5 - parent.timer) / 2) : parent.state == "sleep" || parent.state == "alert" ? 0 : 1; var p = Vector3.Lerp(level.parentBed, parent.position, wake); p.y = 1 + .55f * wake; return p; }
        }
        public Vector3 WasherPosition { get { return levelIndex == 3 ? new Vector3(1.12f, 1.1f, 9) : new Vector3(11.8f, 1.1f, 15.02f); } }
        public static Vector3 SoundPosition(string kind, Vector3 p)
        {
            if (kind == "hingeMotion") { p.x -= .47f; p.y = 1.2f; }
            else if (kind.EndsWith("Step") || kind == "step" || kind.StartsWith("floor") || kind.EndsWith("Drop") || kind == "crash") p.y = .12f;
            else p.y = kind == "snore" ? 1 : kind == "radio" ? (p.x > 20 ? .88f : .6f) : kind == "toy" ? .65f : 1.1f;
            return p;
        }
        public NightSoundPath SoundPath(string kind, Vector3 source, Vector3 listener)
        {
            NightDoor ignore = null;
            if (kind.StartsWith("door") || kind.StartsWith("hinge") || kind.StartsWith("latch")) foreach (var d in doors) if (NightGeometry.Distance(d.Position, source) < .75f) { ignore = d; break; }
            return Acoustics.Profile(SoundPosition(kind, source), listener, ignore);
        }
        static float Envelope(float age, float duration, float attack, float release) { return Mathf.Clamp01(Mathf.Min(age / attack, (duration - age) / release)); }
        public List<NightEnvironmentSound> EnvironmentSources()
        {
            float age = time - lastSnore, washerAge = clock.phaseAge;
            var bed = level.parentBed; bed.y = 1;
            return new List<NightEnvironmentSound>
            {
                new NightEnvironmentSound { kind = "snore", name = "长鼾声", position = bed, active = parent.state == "sleep" && age >= 0 && age < 1.7f, cover = age >= .1f && age < 1.6f, age = age, duration = 1.7f, gain = Envelope(age,1.7f,.09f,.1f) },
                new NightEnvironmentSound { kind = "washer", name = "洗衣机脱水声", position = WasherPosition, active = levelIndex != 3 && clock.washerActive, cover = clock.washerActive, age = washerAge, duration = 90, gain = Envelope(washerAge,90,.15f,.3f) }
            };
        }
        public NightHearing HearingProfile(float amount, string kind, Vector3 source)
        {
            var path = SoundPath(kind, source, ParentEar); var emitted = NightAcoustics.EmittedBands(kind, amount, FloorAt(source)); var signal = NightAcoustics.Multiply(emitted, path.bands); var mask = Vector3.zero;
            foreach (var env in EnvironmentSources()) if (env.active) { var through = NightAcoustics.Multiply(NightAcoustics.EmittedBands(env.kind) * env.gain, Acoustics.Profile(env.position, ParentEar).bands); mask += NightAcoustics.Square(through); }
            float energy = signal.sqrMagnitude, factor = 1;
            if (energy >= 1e-12f)
            {
                float residual = 0; for (int i = 0; i < 3; i++) { float ratio2 = mask[i] / Mathf.Max(1e-10f, signal[i] * signal[i]); residual += signal[i] * signal[i] / (1 + ratio2); }
                factor = Mathf.Sqrt(residual / energy);
            }
            float sum = Mathf.Max(1e-12f, emitted.sqrMagnitude); var weighted = NightAcoustics.Multiply(NightAcoustics.Square(path.bands), NightAcoustics.Square(emitted)); float transmission = Mathf.Sqrt((weighted.x + weighted.y + weighted.z) / sum);
            return new NightHearing { heard = amount * Mathf.Min(1, transmission * 2) * factor, factor = factor, path = path, signal = signal };
        }
        public float HearingAt(float amount, string kind, Vector3 source) { return HearingProfile(amount, kind, source).heard; }
        public bool MaskAt(Vector3 point)
        {
            var listener = point; listener.y = crouched ? .86f : 1.35f;
            var step = NightAcoustics.Multiply(NightAcoustics.EmittedBands("floorSoft"), SoundPath("floorSoft", point, listener).bands);
            if (HearingProfile(25, "floorSoft", point).factor > .5f) return false;
            foreach (var env in EnvironmentSources()) if (env.active && env.cover)
            {
                var bands = NightAcoustics.Multiply(NightAcoustics.EmittedBands(env.kind) * env.gain, Acoustics.Profile(env.position, listener).bands);
                if (NightAcoustics.BandLevel(bands) / Mathf.Max(.00001f, NightAcoustics.BandLevel(step)) >= .65f) return true;
            }
            return false;
        }
    }
}
