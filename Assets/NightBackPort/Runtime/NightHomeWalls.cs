// @role Builds finished walls, trim and tiles
// @calls NightHomeArt

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace NightBackPort
{
    // Wall finish only: source topology, gameplay geometry and the bedroom furniture stay intact.
    public static class NightHomeWalls
    {
        static readonly Dictionary<string, Material> materials = new Dictionary<string, Material>();
        static Texture2D plaster, plasterNormal, tile, tileNormal;
        static readonly Dictionary<Vector3, Mesh> trimMeshes = new Dictionary<Vector3, Mesh>();

        public static Texture2D PlasterTexture { get { EnsureTextures(); return plaster; } }
        public static Texture2D PlasterNormal { get { EnsureTextures(); return plasterNormal; } }

        public static void Build(Transform house, NightGame game)
        {
            if (!house || game == null) return;
            foreach (var filter in house.GetComponentsInChildren<MeshFilter>(true))
            {
                string parent = filter.transform.parent.name;
                if (filter.name != "Material 127" || (parent != "FullWalls" && parent != "OverviewWalls" && parent != "Roof")) continue;
                var mesh = Object.Instantiate(filter.sharedMesh); mesh.name = "Home Wall UV - " + parent;
                var vertices = mesh.vertices; var normals = mesh.normals; var uv = new Vector2[vertices.Length];
                for (int i = 0; i < vertices.Length; i++)
                {
                    Vector3 p = filter.transform.TransformPoint(vertices[i]);
                    Vector3 n = filter.transform.TransformDirection(normals[i]);
                    uv[i] = Mathf.Abs(n.y) > .8f ? new Vector2(p.x, p.z) :
                        Mathf.Abs(n.x) > .8f ? new Vector2(-p.z * Mathf.Sign(n.x), p.y) : new Vector2(p.x * Mathf.Sign(n.z), p.y);
                }
                mesh.uv = uv; mesh.RecalculateTangents(); filter.sharedMesh = mesh;
                var owner = house.GetComponent<NightHomeArtResources>(); if (!owner) owner = house.gameObject.AddComponent<NightHomeArtResources>(); owner.meshes.Add(mesh);
                filter.GetComponent<Renderer>().sharedMaterial = FinishMaterial("plaster");
            }
            var wall = house.Find("FullWalls"); if (!wall) return;
            var root = NightHomeArt.Group(wall, "Art/WallDetails");
            var walls = Cells(game.level.walls); var floors = Cells(game.level.floors);
            var openings = new HashSet<Vector2Int>(); foreach (var o in game.level.openings) openings.Add(new Vector2Int(Mathf.RoundToInt(o.x), Mathf.RoundToInt(o.z)));
            var directions = new[] { Vector2Int.right, Vector2Int.left, Vector2Int.up, Vector2Int.down };
            foreach (var cell in walls) foreach (var direction in directions)
            {
                if (!floors.Contains(cell + direction) || walls.Contains(cell + direction)) continue;
                Vector3 normal = new Vector3(direction.x, 0, direction.y);
                Vector3 p = new Vector3(cell.x, 0, cell.y) + normal * .507f;
                Vector3 inside = p + normal * .35f;
                bool bedroom = !game.IsTutorial && inside.x < 6.5f && inside.z > 10.5f && inside.z < 13.5f;
                var face = NightHomeArt.Group(root, "Art/WallFace"); face.localPosition = p; face.localRotation = Quaternion.LookRotation(normal);
                // The source mesh omits the inward face of the solid west mass at the laundry.
                // Restore only those three wall-grid faces; retain the existing collision and openings.
                if (!game.IsTutorial && cell.x == 9 && cell.y >= 15 && cell.y <= 17 && direction == Vector2Int.right)
                    Panel(face, "LaundryWestWall", 0, 1.30f, -.008f, 1.003f, 2.60f, .014f, "plaster");
                // The small overlap removes cracks at consecutive cell joins. All relief stays within 4 cm.
                Panel(face, "CrownShadow", 0, 2.47f, .010f, 1.003f, .07f, .016f, "trim-dark");
                Panel(face, "CrownCap", 0, 2.52f, .016f, 1.003f, .038f, .026f, "trim");
                if (bedroom || openings.Contains(cell + direction)) continue; // Keep the established bedroom panelling / furniture silhouette.
                float min = -.5015f, max = .5015f; Vector3 tangent = Vector3.Cross(Vector3.up, normal);
                foreach (var opening in game.level.openings)
                {
                    Vector3 delta = new Vector3(opening.x, 0, opening.z) - p;
                    float along = Vector3.Dot(delta, tangent), distance = Mathf.Abs(Vector3.Dot(delta, normal));
                    if (distance > .7f) continue;
                    if (along > .5f && along < 1.15f) max = Mathf.Min(max, along - .62f);
                    if (along < -.5f && along > -1.15f) min = Mathf.Max(min, along + .62f);
                }
                float width = max - min;
                var lower = NightHomeArt.Group(face, "Art/LowerWallFinish"); lower.localPosition = new Vector3((max + min) * .5f, 0, 0);
                bool wet = !game.IsTutorial && ((inside.x > 18.5f && inside.z < 6.5f) || (inside.x > 9.5f && inside.x < 13.5f && inside.z > 14.5f));
                Panel(lower, "LowerFinish", 0, .465f, .005f, width, .69f, .014f, wet ? "tile" : "panel");
                Panel(lower, "ChairRailShadow", 0, .818f, .013f, width, .025f, .024f, "trim-dark");
                Panel(lower, "ChairRail", 0, .848f, .017f, width, .035f, .032f, "trim");
                Panel(lower, "BaseBoard", 0, .085f, .014f, width, .145f, .026f, "trim-dark");
                Panel(lower, "BaseCap", 0, .166f, .020f, width, .025f, .032f, "trim");
                if (!wet)
                {
                    foreach (float x in new[] { -width * .5f + .09f, width * .5f - .09f }) Panel(lower, "PanelStile", x, .465f, .016f, .014f, .47f, .010f, "trim-dark");
                    foreach (float y in new[] { .23f, .70f }) Panel(lower, "PanelRail", 0, y, .016f, width - .166f, .014f, .010f, "trim-dark");
                }
            }
            // Continue the cornice over the existing lintels, while leaving the passage clear below.
            foreach (var opening in game.level.openings) foreach (int side in new[] { -1, 1 })
            {
                Vector3 normal = (opening.axis == "x" ? Vector3.forward : Vector3.right) * side;
                var face = NightHomeArt.Group(root, "Art/LintelCornice");
                face.localPosition = new Vector3(opening.x, 0, opening.z) + normal * .507f;
                face.localRotation = Quaternion.LookRotation(normal);
                Panel(face, "CrownShadow", 0, 2.47f, .010f, 1.003f, .07f, .016f, "trim-dark");
                Panel(face, "CrownCap", 0, 2.52f, .016f, 1.003f, .038f, .026f, "trim");
            }
            if (!game.IsTutorial)
            {
                foreach (var opening in game.level.openings)
                {
                    Vector3 tangent = opening.axis == "x" ? Vector3.right : Vector3.forward;
                    Vector3 normal = opening.axis == "x" ? Vector3.back : Vector3.left;
                    Vector3 p = new Vector3(opening.x, 1.15f, opening.z) + tangent * 1.07f + normal * .53f;
                    if (Mountable(game.level, p, normal, .14f, .18f)) Switch(root, p, normal);
                }
            }
            NightHomeArt.Finish(root);
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                var filter = renderer.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh && renderer.name.StartsWith("Art Batch")) {
                    if (renderer.sharedMaterial == FinishMaterial("plaster")) {
                        var mesh=filter.sharedMesh;var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
                        for(int i=0;i<vertices.Length;i++) {
                            var p=filter.transform.TransformPoint(vertices[i]);var n=filter.transform.TransformDirection(normals[i]);
                            uv[i]=Mathf.Abs(n.y)>.8f?new Vector2(p.x,p.z):Mathf.Abs(n.x)>.8f?new Vector2(-p.z*Mathf.Sign(n.x),p.y):new Vector2(p.x*Mathf.Sign(n.z),p.y);
                        }
                        mesh.uv=uv;
                        renderer.shadowCastingMode=ShadowCastingMode.On;
                    }
                    filter.sharedMesh.RecalculateTangents();
                }
            }
        }

        static HashSet<Vector2Int> Cells(Vector2[] cells)
        { var set = new HashSet<Vector2Int>(); foreach (var c in cells) set.Add(new Vector2Int(Mathf.RoundToInt(c.x), Mathf.RoundToInt(c.y))); return set; }

        static void Panel(Transform root, string name, float x, float y, float z, float w, float h, float d, string finish)
        {
            var t = NightHomeArt.Group(root, "Art/" + name); t.localPosition = new Vector3(x, y, z);
            Vector3 size = new Vector3(w, h, d); Mesh mesh;
            if (!trimMeshes.TryGetValue(size, out mesh) || !mesh) { mesh = TrimMesh(size); trimMeshes[size] = mesh; }
            t.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            t.gameObject.AddComponent<MeshRenderer>().sharedMaterial = FinishMaterial(finish);
        }

        // Long, shallow mouldings do not need the 108-triangle furniture bevel mesh.
        static Mesh TrimMesh(Vector3 size)
        {
            var vertices = new List<Vector3>(); var normals = new List<Vector3>(); var uv = new List<Vector2>(); var triangles = new List<int>();
            Vector3 half = size * .5f;
            foreach (var normal in new[] { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back })
            {
                Vector3 u = Mathf.Abs(normal.y) > .5f ? Vector3.right : Vector3.Cross(Vector3.up, normal), v = Vector3.Cross(normal, u);
                float a = Vector3.Dot(half, new Vector3(Mathf.Abs(u.x), Mathf.Abs(u.y), Mathf.Abs(u.z)));
                float b = Vector3.Dot(half, new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z)));
                float c = Vector3.Dot(half, new Vector3(Mathf.Abs(normal.x), Mathf.Abs(normal.y), Mathf.Abs(normal.z)));
                int start = vertices.Count;
                foreach (var q in new[] { new Vector2(-1,-1), new Vector2(1,-1), new Vector2(1,1), new Vector2(-1,1) })
                { vertices.Add(normal * c + u * (q.x * a) + v * (q.y * b)); normals.Add(normal); uv.Add(new Vector2((q.x + 1) * a, (q.y + 1) * b)); }
                triangles.AddRange(new[] { start, start+1, start+2, start, start+2, start+3 });
            }
            var mesh = new Mesh { name = "Home Wall trim template" }; mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetUVs(0, uv); mesh.SetTriangles(triangles,0); mesh.RecalculateTangents(); mesh.RecalculateBounds(); return mesh;
        }

        static Material FinishMaterial(string key)
        {
            Material m; if (materials.TryGetValue(key, out m) && m) return m;
            EnsureTextures(); m = new Material(NightHomeArt.Material("navy")); m.name = "Home Wall - " + key;
            m.SetFloat("_UseVertexColor", 0); m.SetFloat("_Metallic", 0); m.SetFloat("_Glossiness", .13f);
            m.SetFloat("_UseNormal", key == "plaster" || key == "tile" ? 1 : 0);
            m.mainTexture = plaster; m.SetTexture("_BumpMap", plasterNormal); m.SetFloat("_BumpScale", .55f);
            switch (key)
            {
                case "plaster": m.color = new Color(.13f, .19f, .27f); break;
                case "panel": m.color = new Color(.115f, .177f, .216f); m.SetFloat("_Glossiness", .22f); break;
                case "trim": m.color = new Color(.245f, .292f, .30f); m.SetFloat("_Glossiness", .25f); break;
                case "trim-dark": m.color = new Color(.145f, .208f, .24f); break;
                case "tile": m.color = new Color(.54f, .60f, .55f); m.mainTexture = tile; m.SetTexture("_BumpMap", tileNormal); m.SetFloat("_BumpScale", .36f); m.SetFloat("_Glossiness", .34f); break;
            }
            materials[key] = m; return m;
        }

        static void EnsureTextures()
        {
            if (plaster && plasterNormal && tile && tileNormal) return;
            const int n = 256; var paint = new Color[n * n]; var ceramic = new Color[n * n]; var height = new float[n * n]; var tileHeight = new float[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float u = (float)x / n, v = (float)y / n;
                float cloud = PeriodicNoise(u, v, 4), brush = PeriodicNoise(u, v, 19), grain = PeriodicNoise(u, v, 83);
                float streak = Mathf.Sin(u * Mathf.PI * 32 + Mathf.Sin(v * Mathf.PI * 4) * 1.3f) * .007f;
                float tone = .91f + (cloud - .5f) * .24f + (brush - .5f) * .080f + (grain - .5f) * .065f + streak;
                paint[y * n + x] = new Color(tone, tone * .995f, tone * .988f, 1); height[y * n + x] = brush * .30f + grain * .14f;
                // Staggered small glazed tiles, with softly recessed grout.
                float row = Mathf.Floor(v * 8), tx = Mathf.Repeat(u * 4 + (row % 2) * .5f, 1), ty = Mathf.Repeat(v * 8, 1);
                float edge = Mathf.Min(Mathf.Min(tx, 1 - tx), Mathf.Min(ty, 1 - ty) * .5f);
                float tileFace = Mathf.SmoothStep(0, 1, Mathf.Clamp01((edge - .011f) / .025f));
                float tileTone = Mathf.Lerp(.43f, .93f + (cloud - .5f) * .08f, tileFace);
                ceramic[y * n + x] = new Color(tileTone, tileTone, tileTone, 1); tileHeight[y * n + x] = tileFace * .8f + brush * .025f;
            }
            plaster = Texture("Home Wall plaster colour", paint, false); tile = Texture("Home Wall glazed tile colour", ceramic, false);
            plasterNormal = Texture("Home Wall plaster raw normal", Normals(height, n), true); tileNormal = Texture("Home Wall tile raw normal", Normals(tileHeight, n), true);
        }

        static float PeriodicNoise(float u, float v, float scale)
        {
            float a = Mathf.PerlinNoise(13 + u * scale, 29 + v * scale), b = Mathf.PerlinNoise(13 + (u - 1) * scale, 29 + v * scale);
            float c = Mathf.PerlinNoise(13 + u * scale, 29 + (v - 1) * scale), d = Mathf.PerlinNoise(13 + (u - 1) * scale, 29 + (v - 1) * scale);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }
        static Color[] Normals(float[] height, int n)
        {
            var pixels = new Color[n * n];
            for (int y = 0; y < n; y++) for (int x = 0; x < n; x++)
            {
                float dx = height[y * n + (x + 1) % n] - height[y * n + (x + n - 1) % n];
                float dy = height[((y + 1) % n) * n + x] - height[((y + n - 1) % n) * n + x];
                // NativeSurface flips green for glTF V; encode that convention here too.
                Vector3 normal = new Vector3(-dx * 2, dy * 2, 1).normalized;
                pixels[y * n + x] = new Color(normal.x * .5f + .5f, normal.y * .5f + .5f, normal.z * .5f + .5f, 1);
            }
            return pixels;
        }
        static Texture2D Texture(string name, Color[] pixels, bool linear)
        { var t = new Texture2D(256, 256, TextureFormat.RGB24, true, linear); t.name = name; t.wrapMode = TextureWrapMode.Repeat; t.filterMode = FilterMode.Trilinear; t.anisoLevel = 4; t.SetPixels(pixels); t.Apply(); return t; }

        static bool Mountable(NightLevel level, Vector3 p, Vector3 inward, float w, float h)
        {
            var walls = Cells(level.walls); var floors = Cells(level.floors); Vector3 tangent = Vector3.Cross(Vector3.up, inward);
            foreach (float offset in new[] { -w * .5f, 0f, w * .5f })
            {
                Vector3 q = p + tangent * offset; Vector3 back = q - inward * .08f, front = q + inward * .55f;
                if (!walls.Contains(new Vector2Int(Mathf.RoundToInt(back.x), Mathf.RoundToInt(back.z))) || !floors.Contains(new Vector2Int(Mathf.RoundToInt(front.x), Mathf.RoundToInt(front.z)))) return false;
            }
            if (p.z < .8f && p.y + h * .5f > .86f && p.y - h * .5f < 2.25f)
                foreach (float x in new[] { 3f, 11f, 21f }) if (Mathf.Abs(p.x - x) < 1.2f + w * .5f) return false;
            foreach (var f in level.furniture)
            {
                if (f.h < p.y - h * .5f) continue;
                Vector3 q = p + inward * .05f - f.Position; float c = Mathf.Cos(f.yaw), s = Mathf.Sin(f.yaw);
                if (Mathf.Abs(q.x * c + q.z * s) < f.w * .5f + w * .5f && Mathf.Abs(-q.x * s + q.z * c) < f.d * .5f + .08f) return false;
            }
            return true;
        }

        static void Switch(Transform parent, Vector3 p, Vector3 inward)
        {
            var root = NightHomeArt.Group(parent, "Art/WallSwitch"); root.localPosition = p; root.localRotation = Quaternion.LookRotation(inward);
            NightHomeArt.Box(root, "Art/SwitchPlate", Vector3.zero, new Vector3(.095f, .14f, .012f), "ceramic", .009f);
            NightHomeArt.Box(root, "Art/SwitchRocker", new Vector3(0, 0, .010f), new Vector3(.049f, .077f, .012f), "paper", .004f);
        }
    }
}
