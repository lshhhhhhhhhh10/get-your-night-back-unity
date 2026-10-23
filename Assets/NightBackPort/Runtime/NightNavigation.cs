// @role Tests occupancy, sight geometry and parent routes
// @calls NightData

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    public struct NightBox
    {
        public string id, type;
        public float x, z, w, d, h, yaw;
        public NightBox(float px, float pz, float width, float depth, float height = 2.6f, float angle = 0, string name = null, string material = null)
        { x = px; z = pz; w = width; d = depth; h = height; yaw = angle; id = name; type = material; }
        public NightBox(NightFurniture f) : this(f.x, f.z, f.w, f.d, f.h, f.yaw, f.id, f.type) { }
    }

    public static class NightGeometry
    {
        public static Vector3 LocalPoint(Vector3 p, NightBox box)
        {
            float c = Mathf.Cos(box.yaw), s = Mathf.Sin(box.yaw), x = p.x - box.x, z = p.z - box.z;
            return new Vector3(x * c - z * s, p.y, x * s + z * c);
        }
        public static bool CircleHits(Vector3 p, float radius, NightBox box)
        {
            var q = LocalPoint(p, box); float dx = Mathf.Max(Mathf.Abs(q.x) - box.w / 2, 0), dz = Mathf.Max(Mathf.Abs(q.z) - box.d / 2, 0);
            return dx * dx + dz * dz < radius * radius;
        }
        public static NightBox DoorShape(NightDoor door, float progress = -1)
        {
            float a = (progress < 0 ? door.progress : progress) * Mathf.PI * .49f;
            return new NightBox(door.x - .47f + .47f * Mathf.Cos(a), door.z - .47f * Mathf.Sin(a), .94f, .12f, 2.16f, a, "door", "door");
        }
        // 射线与旋转盒体的精确相交区间，声传播和遮挡使用同一尺寸。
        public static bool Interval(Vector3 a, Vector3 b, NightBox box, out Vector2 range)
        {
            Vector3 pa = LocalPoint(a, box), pb = LocalPoint(b, box); float lo = 0, hi = 1;
            bool hit = Slab(pa.x, pb.x, -box.w / 2, box.w / 2, ref lo, ref hi)
                && Slab(pa.z, pb.z, -box.d / 2, box.d / 2, ref lo, ref hi)
                && Slab(pa.y, pb.y, 0, box.h, ref lo, ref hi);
            range = new Vector2(lo, hi); return hit && hi > .002f && lo < .998f;
        }
        static bool Slab(float a, float b, float min, float max, ref float lo, ref float hi)
        {
            float d = b - a; if (Mathf.Abs(d) < 1e-8f) return a > min && a < max;
            float t0 = (min - a) / d, t1 = (max - a) / d;
            lo = Mathf.Max(lo, Mathf.Min(t0, t1)); hi = Mathf.Min(hi, Mathf.Max(t0, t1)); return hi - lo >= .000001f;
        }
        public static float Distance(Vector3 a, Vector3 b) { float x = a.x - b.x, z = a.z - b.z; return Mathf.Sqrt(x * x + z * z); }
    }

    public sealed class NightNavigation
    {
        readonly NightLevel level;
        readonly HashSet<int> walls = new HashSet<int>();
        readonly List<Vector3> nodes = new List<Vector3>();
        readonly Dictionary<int, int> nodeIds = new Dictionary<int, int>();
        readonly List<List<int>> links = new List<List<int>>();
        public NightNavigation(NightLevel data)
        {
            level = data;
            if (level.walls != null) foreach (var wall in level.walls) walls.Add(Key(Mathf.RoundToInt(wall.x), Mathf.RoundToInt(wall.y)));
            for (int iz = 2; iz < (level.depth - 1) * 2; iz++) for (int ix = 2; ix < (level.width - 1) * 2; ix++)
            {
                var p = new Vector3(ix * .5f, 0, iz * .5f); if (!CanStand(p, .24f)) continue;
                nodeIds.Add(Key(ix, iz), nodes.Count); nodes.Add(p); links.Add(new List<int>());
            }
            int[] dx = { 0, 1, -1, 0 }, dz = { 1, 0, 0, -1 };
            for (int i = 0; i < nodes.Count; i++) for (int d = 0; d < 4; d++)
            {
                int target;
                if (nodeIds.TryGetValue(Key(Mathf.RoundToInt(nodes[i].x * 2) + dx[d], Mathf.RoundToInt(nodes[i].z * 2) + dz[d]), out target)
                    && StaticSegment(nodes[i], nodes[target], .24f)) links[i].Add(target);
            }
        }
        static int Key(int x, int z) { return (x + 2048) * 4096 + z + 2048; }
        public bool Wall(int x, int z) { return x <= 0 || x >= level.width - 1 || z <= 0 || z >= level.depth - 1 || walls.Contains(Key(x, z)); }
        public bool CanStand(Vector3 p, float radius)
        {
            if (float.IsNaN(p.x) || float.IsNaN(p.z) || float.IsInfinity(p.x) || float.IsInfinity(p.z)) return false;
            if (p.x < .5f + radius || p.z < .5f + radius || p.x > level.width - 1.5f - radius || p.z > level.depth - 1.5f - radius) return false;
            for (int z = Mathf.FloorToInt(p.z - radius - .5f); z <= Mathf.CeilToInt(p.z + radius + .5f); z++)
                for (int x = Mathf.FloorToInt(p.x - radius - .5f); x <= Mathf.CeilToInt(p.x + radius + .5f); x++)
                    if (Wall(x, z) && NightGeometry.CircleHits(p, radius, new NightBox(x, z, 1, 1))) return false;
            if (level.furniture != null) foreach (var f in level.furniture) if (NightGeometry.CircleHits(p, radius, new NightBox(f))) return false;
            return true;
        }
        bool StaticSegment(Vector3 a, Vector3 b, float radius)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(NightGeometry.Distance(a, b) * 20));
            for (int i = 0; i <= n; i++) if (!CanStand(Vector3.Lerp(a, b, (float)i / n), radius)) return false;
            return true;
        }
        static bool DoorsClear(Vector3 a, Vector3 b, List<NightDoor> doors, bool blockClosed, float radius)
        {
            int n = Mathf.Max(1, Mathf.CeilToInt(NightGeometry.Distance(a, b) * 20));
            foreach (var door in doors)
            {
                if (!blockClosed && door.progress < .999f) continue;
                var box = NightGeometry.DoorShape(door);
                for (int i = 0; i <= n; i++) if (NightGeometry.CircleHits(Vector3.Lerp(a, b, (float)i / n), radius, box)) return false;
            }
            return true;
        }
        public List<Vector3> FindPath(Vector3 origin, Vector3 target, List<NightDoor> doors, bool blockClosed = false, float radius = .24f)
        {
            var result = new List<Vector3>(); int start = -1; float nearest = 1f;
            for (int i = 0; i < nodes.Count; i++)
            {
                float d = NightGeometry.Distance(origin, nodes[i]);
                if (d < nearest && StaticSegment(origin, nodes[i], radius) && DoorsClear(origin, nodes[i], doors, blockClosed, radius)) { start = i; nearest = d; }
            }
            if (start < 0) return result;
            int[] previous = new int[nodes.Count]; for (int i = 0; i < previous.Length; i++) previous[i] = -2;
            var queue = new List<int> { start }; previous[start] = -1; int found = start; float best = NightGeometry.Distance(nodes[start], target);
            for (int q = 0; q < queue.Count; q++)
            {
                int current = queue[q]; float distance = NightGeometry.Distance(nodes[current], target);
                if (distance < best) { found = current; best = distance; } if (distance < .1f) { found = current; break; }
                foreach (int next in links[current])
                    if (previous[next] == -2 && DoorsClear(nodes[current], nodes[next], doors, blockClosed, radius)) { previous[next] = current; queue.Add(next); }
            }
            for (int p = found; p >= 0; p = previous[p]) result.Add(nodes[p]); result.Reverse();
            if (result.Count > 0 && NightGeometry.Distance(result[0], origin) < .01f) result.RemoveAt(0);
            return result;
        }
    }

    public partial class NightGame
    {
        [NonSerialized] NightNavigation navigation;
        NightNavigation Nav { get { if (navigation == null) navigation = new NightNavigation(level); return navigation; } }
        public bool CanOccupy(Vector3 p, float radius = .2f)
        {
            if (!Nav.CanStand(p, radius)) return false;
            foreach (var door in doors) if (DoorHits(door, p, radius)) return false; return true;
        }
        public bool DoorHits(NightDoor door, Vector3 point, float radius) { return NightGeometry.CircleHits(point, radius, NightGeometry.DoorShape(door)); }
        public bool DoorSweepHits(NightDoor door, float from, float to, Vector3 point, float radius)
        {
            int steps = Mathf.Max(1, Mathf.CeilToInt(Mathf.Abs(to - from) / .02f));
            for (int i = 0; i <= steps; i++) if (NightGeometry.CircleHits(point, radius, NightGeometry.DoorShape(door, Mathf.Lerp(from, to, (float)i / steps)))) return true;
            return false;
        }
        public List<Vector3> FindPath(Vector3 from, Vector3 target, bool closedDoorsBlock = true, float radius = .2f) { return Nav.FindPath(from, target, doors, closedDoorsBlock, radius); }
        public bool Occluded(Vector3 a, Vector3 b, bool lowCover = true, string ignoreId = null)
        {
            int n = Mathf.CeilToInt(NightGeometry.Distance(a, b) * 30);
            for (int i = 1; i < n; i++)
            {
                var point = Vector3.Lerp(a, b, (float)i / n);
                if (Nav.Wall(Mathf.RoundToInt(point.x), Mathf.RoundToInt(point.z))) return true;
                foreach (var f in level.furniture) if (f.id != ignoreId && f.h >= (lowCover ? .74f : 1.35f) && NightGeometry.CircleHits(point, .005f, new NightBox(f))) return true;
                foreach (var door in doors) if (DoorHits(door, point, .005f)) return true;
            }
            return false;
        }
        public string FloorAt(Vector3 point)
        {
            if (levelIndex == 3) return Mathf.Abs(point.x - 2.5f) <= 1.25f && Mathf.Abs(point.z - 16) <= 1.4f ? "carpet" : "wood";
            if (Mathf.Abs(point.x - 3.65f) <= .65f && Mathf.Abs(point.z - 12.15f) <= .9f || Mathf.Abs(point.x - 2.6f) <= 1.1f && Mathf.Abs(point.z - 2.8f) <= 1.3f) return "carpet";
            int x = Mathf.RoundToInt(point.x), z = Mathf.RoundToInt(point.z); return x > 18 && z < 7 || x >= 10 && x <= 13 && z >= 14 ? "tile" : "wood";
        }
    }
}
