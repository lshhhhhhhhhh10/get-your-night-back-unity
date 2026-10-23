// @role Configures lights for the household
// @calls NightHomeArt

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Decorative fixtures and their light sources only; gameplay visibility remains owned by NightGame.
    public static class NightHomeLighting
    {
        public static List<Light> Build(Transform house, NightGame game)
        {
            var lights = new List<Light>();
            if (!house || game == null || game.level == null) return lights;
            var fullWalls = house.Find("FullWalls");
            var root = NightHomeArt.Group(fullWalls ? fullWalls : house, "Art/RoomWallLighting");
            if (game.IsTutorial)
            {
                // A single distant practical leaves the moving tutorial guidance light dominant.
                Add(root, game.level, lights, "Practice", new Vector3(5.44f, 1.95f, 9.8f), Vector3.left, 1.3f, 3.5f);
            }
            else
            {
                Add(root, game.level, lights, "Living", new Vector3(.56f, 1.90f, 4.65f), Vector3.right, 1.65f, 4.2f);
                Add(root, game.level, lights, "Study", new Vector3(14.56f, 1.95f, 4.25f), Vector3.right, 1.6f, 4.0f);
                // The x=18 / z=3 opening is deliberately avoided. Level 0 has no kitchen floor here.
                Add(root, game.level, lights, "Kitchen", new Vector3(18.56f, 1.95f, 4.25f), Vector3.right, 1.7f, 4.2f);
                Add(root, game.level, lights, "ParentRoom", new Vector3(8.44f, 1.85f, 6.6f), Vector3.left, 1.3f, 3.5f);
                // The laundry's west wall is x=9, not x=10. Storage z=12 is a doorway.
                Add(root, game.level, lights, "Laundry", new Vector3(9.56f, 1.95f, 16f), Vector3.right, 1.4f, 3.7f);
                Add(root, game.level, lights, "Storage", new Vector3(14.56f, 1.95f, 13f), Vector3.right, 1.45f, 3.8f);
            }
            return lights;
        }

        static void Add(Transform parent, NightLevel level, List<Light> lights, string room, Vector3 position, Vector3 inward, float intensity, float range)
        {
            if (!HasClearWall(level, position, inward)) return;
            var fixture = NightHomeArt.Group(parent, "Art/" + room + "WallSconce");
            fixture.localPosition = position;
            fixture.localRotation = Quaternion.LookRotation(inward, Vector3.up);
            NightHomeArt.Box(fixture, "Art/BrassBackplate", new Vector3(0, -.030f, .012f), new Vector3(.073f, .265f, .026f), "brass", .016f);
            NightHomeArt.Box(fixture, "Art/BrassBracket", new Vector3(0, -.115f, .095f), new Vector3(.027f, .028f, .19f), "brass", .007f);
            NightHomeArt.Cylinder(fixture, "Art/BrassStem", new Vector3(0, -.056f, .165f), .012f, .130f, "brass", 12);
            NightHomeArt.Cylinder(fixture, "Art/LinenShade", new Vector3(0, .018f, .165f), .140f, .250f, "shade", 16);
            NightHomeArt.Cylinder(fixture, "Art/ShadeLowerBinding", new Vector3(0, -.108f, .165f), .143f, .010f, "brass", 16);
            NightHomeArt.Cylinder(fixture, "Art/ShadeUpperBinding", new Vector3(0, .144f, .165f), .143f, .010f, "brass", 16);
            NightHomeArt.Finish(fixture);
            // Finish currently batches by material. Disable shade shadows on the resulting room batch too.
            Material shade = NightHomeArt.Material("shade");
            foreach (MeshRenderer renderer in fixture.GetComponentsInChildren<MeshRenderer>(true))
                { renderer.shadowCastingMode = ShadowCastingMode.Off; if(renderer.sharedMaterial == shade) renderer.receiveShadows = false; }
            var point = new GameObject("Room Practical - " + room).AddComponent<Light>();
            point.transform.SetParent(fixture, false);
            // 20 cm beyond the shade's forward face avoids self-shadowing and keeps the wall pool broad.
            point.transform.localPosition = new Vector3(0, .020f, .505f);
            point.type = LightType.Point;
            point.color = new Color(1f, .76f, .47f);
            point.intensity = intensity;
            point.range = range;
            point.shadows = LightShadows.Soft;
            point.shadowStrength = .7f;
            point.shadowBias = .035f;
            point.shadowNormalBias = .06f;
            point.renderMode = LightRenderMode.Auto;
            lights.Add(point);
        }

        static bool HasClearWall(NightLevel level, Vector3 p, Vector3 inward)
        {
            Vector3 tangent = Vector3.Cross(Vector3.up, inward);
            // Test both sides of the fixture so a seam next to an opening cannot become a false mount.
            for (int side = -1; side <= 1; side += 2)
                if (!Contains(level.walls, p - inward * .12f + tangent * (.15f * side))) return false;
            if (!Contains(level.floors, p + inward * .60f)) return false;
            if (level.openings != null)
                foreach (NightOpening door in level.openings)
                    if (Vector2.Distance(new Vector2(p.x, p.z), new Vector2(door.x, door.z)) < 1.05f) return false;
            // All windows occupy the north wall. Explicit bounds keep future candidate edits out of the openings.
            if (p.z < .85f)
                foreach (float x in new[] { 3f, 11f, 21f }) if (Mathf.Abs(p.x - x) < .95f) return false;
            if (level.furniture != null)
                foreach (NightFurniture furniture in level.furniture)
                {
                    if (furniture.h < p.y - .23f) continue;
                    Vector3 c = p + inward * .18f;
                    float dx = c.x - furniture.x, dz = c.z - furniture.z;
                    float cos = Mathf.Cos(furniture.yaw), sin = Mathf.Sin(furniture.yaw);
                    float x = dx * cos + dz * sin, z = -dx * sin + dz * cos;
                    if (Mathf.Abs(x) < furniture.w * .5f + .19f && Mathf.Abs(z) < furniture.d * .5f + .19f) return false;
                }
            return true;
        }

        static bool Contains(Vector2[] cells, Vector3 p)
        {
            if (cells == null) return false;
            foreach (Vector2 cell in cells)
                if (Mathf.Abs(cell.x - p.x) <= .501f && Mathf.Abs(cell.y - p.z) <= .501f) return true;
            return false;
        }
    }
}
