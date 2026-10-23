// @role Builds soft furniture and household details
// @calls NightHomeArt

using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    // Visual dressing only. Furniture placement, footprints, collision and lights remain owned by NightWorld.
    public static class NightHomeComfort
    {
        public static bool Build(Transform root, NightFurniture f)
        {
            if (!root || f == null) return false;
            if (f.type != "bed" && f.type != "sofa" && f.type != "chair" && f.type != "lamp" && f.type != "plant" && f.type != "washer") return false;
            Transform p = NightHomeArt.Group(root, "Art Comfort");
            switch (f.type)
            {
                case "bed": Bed(p, f); break;
                case "sofa": Sofa(p, f); break;
                case "chair": Chair(p, f); break;
                case "lamp": Lamp(p, f); break;
                case "plant": Plant(p, f); break;
                case "washer": Washer(p, f); break;
            }
            return true;
        }

        static Transform Box(Transform p, string name, float x, float y, float z, float w, float h, float d, string mat, float bevel = .015f)
        { return NightHomeArt.Box(p, "Art " + name, new Vector3(x, y, z), new Vector3(w, h, d), mat, bevel); }

        static Transform Cylinder(Transform p, string name, float x, float y, float z, float radius, float height, string mat, int sides = 16)
        { return NightHomeArt.Cylinder(p, "Art " + name, new Vector3(x, y, z), radius, height, mat, sides); }

        static void Feet(Transform p, float w, float d, float height, float insetX, float insetZ, float thickness)
        {
            foreach (float x in new[] { -1f, 1f }) foreach (float z in new[] { -1f, 1f })
                Box(p, "Walnut foot", x * (w * .5f - insetX), height * .5f, z * (d * .5f - insetZ), thickness, height, thickness, "walnut", .008f);
        }

        static void Bed(Transform p, NightFurniture f)
        {
            float w = f.w, d = f.d;
            // Keep the original .625 mattress surface / .76 bedding height for the fixed sleeping rig.
            Feet(p, w, d, .19f, .15f, .17f, .085f);
            Box(p, "Bed rail", 0, .285f, 0, w * .98f, .25f, d * .96f, "walnut", .025f);
            Box(p, "Bed foot inset", 0, .30f, d * .48f, w * .87f, .145f, .014f, "wood", .008f);
            Box(p, "Mattress", 0, .51f, .012f, w * .945f, .23f, d * .93f, "linen", .055f);
            // Piping is a thin continuous edge, visibly separating mattress and fabric.
            PipeRectangle(p, "Mattress edge", new Vector3(0, .555f, .012f), w * .947f, d * .932f, .009f, "paper");
            Box(p, "Headboard frame", 0, f.h * .58f, -d * .5f + .045f, w, f.h * .80f, .09f, "walnut", .025f);
            Box(p, "Headboard upholstered inset", 0, f.h * .715f, -d * .5f + .095f, w * .88f, f.h * .43f, .045f, "linen", .032f);
            int pillowCount = w > 1.45f ? 2 : 1;
            for (int i = 0; i < pillowCount; i++)
            {
                float pw = pillowCount == 2 ? w * .395f : w * .73f;
                float px = pillowCount == 2 ? (i == 0 ? -1 : 1) * w * .215f : 0;
                var pillow = Box(p, "Soft pillow", px, .686f, -d * .32f, pw, .14f, d * .205f, "paper", .06f);
                pillow.localRotation = Quaternion.Euler(0, i == 0 ? -3 : 3, 0);
                PipeRectangle(p, "Pillow seam", new Vector3(px, .686f, -d * .32f), pw * .985f, d * .203f, .006f, "linen");
            }
            Quilt(p, Mathf.Min(w * 1.02f, w + .02f), d, .722f, w * .947f * .5f + .0045f);
            Box(p, "Turned linen sheet", 0, .744f, -d * .105f, w * .93f, .035f, d * .13f, "linen", .014f);
            // Two subtle stitch channels give the blanket scale without a busy patterned texture.
            for (int i = -1; i <= 1; i += 2)
                Box(p, "Quilt stitch", i * w * .31f, .739f, d * .145f, .006f, .004f, d * .38f, "navy", .002f);
        }

        static void Quilt(Transform p, float w, float d, float y, float mattressSide)
        {
            // Explicit shoulder vertices keep triangles flat until they clear the mattress and piping.
            // A threshold alone on a uniform grid would still interpolate down through the mattress.
            float shoulderX = Mathf.Min(mattressSide + .006f, w * .49f);
            float shoulderU = .5f - shoulderX / w;
            float footStart = .012f + d * .466f + .01f, footEnd = d * .5f;
            var across = new List<float> { 0, shoulderU * .5f, shoulderU };
            for (int i = 1; i < 16; i++)
            {
                float u = i / 16f;
                if (u > shoulderU && u < 1 - shoulderU) across.Add(u);
            }
            across.Add(1 - shoulderU); across.Add(1 - shoulderU * .5f); across.Add(1);
            var along = new List<float>();
            for (int i = 0; i < 12; i++)
            {
                float z = Mathf.Lerp(-d * .17f, footEnd, i / 12f);
                if (z < footStart) along.Add(z);
            }
            along.Add(footStart); along.Add((footStart + footEnd) * .5f); along.Add(footEnd);
            int columns = across.Count - 1, rows = along.Count - 1;
            var vertices = new List<Vector3>(); var triangles = new List<int>(); var uv = new List<Vector2>();
            for (int row = 0; row <= rows; row++)
            {
                float z = along[row];
                float v = Mathf.InverseLerp(-d * .17f, footEnd, z);
                for (int col = 0; col <= columns; col++)
                {
                    float u = across[col], x = (u - .5f) * w;
                    float drop = Mathf.Pow(Mathf.InverseLerp(shoulderX, w * .5f, Mathf.Abs(x)), 1.4f) * .16f;
                    float foot = Mathf.Pow(Mathf.InverseLerp(footStart, footEnd, z), 1.4f) * .13f;
                    float wave = .013f * Mathf.Sin(u * 18 + v * 9) + .007f * Mathf.Sin(u * 31 - v * 13);
                    vertices.Add(new Vector3(x, y + wave - Mathf.Max(drop, foot), z));
                    uv.Add(new Vector2(u, v));
                    if (row < rows && col < columns)
                    {
                        int a = row * (columns + 1) + col;
                        // The blanket is double-sided so its hanging hem remains visible from a low camera.
                        Quad(triangles, a, a + columns + 1, a + columns + 2, a + 1);
                    }
                }
            }
            Mesh(p, "Art Draped navy quilt", vertices, triangles, uv, "navy", true);
        }

        static void Sofa(Transform p, NightFurniture f)
        {
            float w = f.w, d = f.d, h = f.h;
            Feet(p, w, d, .19f, .16f, .15f, .07f);
            Box(p, "Sofa walnut rail", 0, .225f, 0, w * .93f, .105f, d * .90f, "walnut", .018f);
            Box(p, "Sofa upholstered base", 0, .355f, 0, w * .99f, .235f, d * .91f, "sage", .035f);
            Box(p, "Sofa back", 0, h * .72f, -d * .39f, w * .98f, h * .54f, d * .22f, "sage", .055f);
            float arm = Mathf.Min(.16f, w * .09f);
            foreach (float x in new[] { -1f, 1f })
            {
                Box(p, "Sofa rounded arm", x * (w * .5f - arm * .5f), .63f, .006f, arm, .38f, d * .96f, "sage", .045f);
                Box(p, "Sofa arm welt", x * (w * .5f - arm * .5f), .802f, .007f, arm * .70f, .007f, d * .86f, "linen", .003f);
            }
            float inner = w - 2 * arm - .035f;
            for (int i = 0; i < 3; i++)
            {
                float cw = inner / 3 - .018f, x = (i - 1) * inner / 3;
                Box(p, "Sofa seat cushion", x, .535f, d * .072f, cw, .145f, d * .68f, "sage", .043f);
                PipeRectangle(p, "Sofa cushion welt", new Vector3(x, .535f, d * .072f), cw * .995f, d * .677f, .007f, "linen");
                var back = Box(p, "Sofa back cushion", x, .827f, -d * .245f, cw, .38f, .13f, "sage", .047f);
                back.localRotation = Quaternion.Euler(-8, 0, 0);
            }
            var scatter = Box(p, "Navy scatter cushion", -w * .275f, .734f, d * .04f, .32f, .31f, .12f, "navy", .055f);
            scatter.localRotation = Quaternion.Euler(-13, 4, -11);
            // A folded throw links the living-room cloth to the bedroom linen palette.
            Box(p, "Folded throw", w * .28f, .625f, d * .14f, w * .17f, .045f, d * .57f, "linen", .019f);
            Box(p, "Throw hanging edge", w * .28f, .533f, d * .413f, w * .17f, .17f, .021f, "linen", .008f);
            for (int i = 0; i < 5; i++)
                Box(p, "Throw fringe", w * .28f + (i - 2) * w * .028f, .435f, d * .414f, .009f, .032f, .012f, "linen", .003f);
        }

        static void Chair(Transform p, NightFurniture f)
        {
            float w = f.w, d = f.d, h = f.h;
            bool armchair = w > .8f;
            Feet(p, w, d, .43f, w * .135f, d * .16f, .045f);
            Box(p, "Chair seat frame", 0, .44f, .005f, w * .96f, .09f, d * .91f, "walnut", .018f);
            string fabric = armchair ? "navy" : "sage";
            float seatW = w * (armchair ? .73f : .94f);
            Box(p, "Chair padded seat", 0, .531f, d * .02f, seatW, .13f, d * .77f, fabric, .042f);
            PipeRectangle(p, "Chair seat welt", new Vector3(0, .531f, d * .02f), seatW, d * .768f, .006f, "linen");
            foreach (float sign in new[] { -1f, 1f })
                Box(p, "Chair back upright", sign * w * .414f, h * .68f, -d * .412f, .044f, h * .59f, .047f, "walnut", .009f);
            Box(p, "Chair back frame", 0, h * .80f, -d * .407f, w * .91f, h * .37f, .084f, "walnut", .028f);
            Box(p, "Chair padded back", 0, h * .797f, -d * .349f, w * .78f, h * .29f, .071f, fabric, .037f);
            if (armchair)
            {
                foreach (float sign in new[] { -1f, 1f })
                {
                    Box(p, "Chair arm support", sign * w * .415f, .595f, d * .26f, .045f, .27f, .047f, "walnut", .009f);
                    Box(p, "Chair padded arm", sign * w * .424f, .743f, -d * .01f, w * .14f, .09f, d * .86f, "walnut", .027f);
                }
                Box(p, "Chair lumbar cushion", 0, .69f, -d * .12f, w * .63f, .22f, .11f, "linen", .04f).localRotation = Quaternion.Euler(-9, 0, 0);
            }
        }

        static void Lamp(Transform p, NightFurniture f)
        {
            float w = Mathf.Min(f.w, f.d), h = f.h;
            Cylinder(p, "Lamp foot", 0, .027f, 0, w * .44f, .054f, "walnut", 24);
            Cylinder(p, "Lamp brass inset", 0, .056f, 0, w * .30f, .014f, "brass", 20);
            Cylinder(p, "Lamp brass stem", 0, (h - .29f) * .5f, 0, .013f, h - .29f, "brass", 12);
            Cylinder(p, "Lamp collar", 0, h - .315f, 0, .035f, .063f, "brass", 12);
            Frustum(p, "Art Linen lampshade", h - .32f, h - .015f, w * .49f, w * .29f, "paper", 20, true);
            // Thin seam rings define a fabric shade instead of an opaque cylinder.
            Ring(p, "Art Lampshade lower binding", new Vector3(0, h - .316f, 0), w * .493f, w * .471f, .012f, "linen", 20);
            Ring(p, "Art Lampshade upper binding", new Vector3(0, h - .013f, 0), w * .296f, w * .273f, .012f, "linen", 20);
            Cylinder(p, "Lamp finial", 0, h - .007f, 0, .017f, .014f, "brass", 12);
        }

        static void Plant(Transform p, NightFurniture f)
        {
            float radius = Mathf.Min(f.w, f.d) * .45f;
            float potHeight = Mathf.Min(.35f, f.h * .31f);
            Frustum(p, "Art Ceramic planter", .025f, potHeight, radius * .68f, radius, "ceramic", 16, false);
            Cylinder(p, "Planter foot", 0, .023f, 0, radius * .67f, .025f, "walnut", 16);
            Ring(p, "Art Planter rolled lip", new Vector3(0, potHeight - .009f, 0), radius * 1.015f, radius * .89f, .032f, "ceramic", 16);
            Cylinder(p, "Planter soil", 0, potHeight - .024f, 0, radius * .90f, .015f, "walnut", 16);
            // Leaves are tapered folded blades, not sphere clusters. Their tips remain inside the original footprint.
            for (int i = 0; i < 7; i++)
            {
                float angle = i * 2.399963f;
                float height = (f.h - potHeight) * (i == 0 ? .98f : .66f + .045f * i);
                Vector3 direction = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
                Vector3 bottom = direction * radius * .10f + Vector3.up * (potHeight - .025f);
                Vector3 tip = direction * radius * (i == 0 ? .20f : .77f) + Vector3.up * (potHeight + height);
                Leaf(p, bottom, tip, direction, radius * (i == 0 ? .44f : .40f));
            }
        }

        static void Washer(Transform p, NightFurniture f)
        {
            float w = f.w, d = f.d, h = f.h, front = d * .5f;
            Feet(p, w, d, h * .06f, .095f, .09f, .05f);
            Box(p, "Washer enamel cabinet", 0, h * .468f, -.01f, w * .985f, h * .84f, d - .03f, "ceramic", .03f);
            Box(p, "Washer countertop lip", 0, h * .882f, -.008f, w, h * .043f, d - .026f, "paper", .016f);
            Box(p, "Washer lower plinth", 0, h * .105f, front - .027f, w * .90f, h * .09f, .025f, "linen", .008f);
            Box(p, "Washer control panel", 0, h * .791f, front - .024f, w * .935f, h * .123f, .031f, "paper", .012f);
            Box(p, "Detergent drawer", -w * .265f, h * .79f, front - .005f, w * .285f, h * .082f, .006f, "linen", .008f);
            Box(p, "Detergent drawer pull", -w * .265f, h * .784f, front - .0005f, w * .18f, .008f, .003f, "charcoal", .002f);
            Transform dial = Cylinder(p, "Washer control dial", w * .10f, h * .79f, front - .004f, w * .05f, .008f, "brass", 20);
            dial.localRotation = Quaternion.Euler(90, 0, 0);
            Box(p, "Washer dial mark", w * .10f, h * .813f, front - .001f, .007f, .015f, .002f, "charcoal", .001f);
            Box(p, "Washer display inset", w * .295f, h * .795f, front - .004f, w * .18f, h * .042f, .006f, "charcoal", .005f);
            Box(p, "Washer display mark", w * .295f, h * .795f, front - .001f, w * .10f, .006f, .002f, "sage", .001f);
            // Front discs are kept within the collider's front face; nested layers create a recessed drum.
            Transform outer = Cylinder(p, "Washer round door rim", 0, h * .446f, front - .025f, w * .308f, .045f, "linen", 32);
            outer.localRotation = Quaternion.Euler(90, 0, 0);
            Transform gasket = Cylinder(p, "Washer door gasket", 0, h * .446f, front - .005f, w * .257f, .010f, "charcoal", 32);
            gasket.localRotation = Quaternion.Euler(90, 0, 0);
            Transform glass = Cylinder(p, "Washer dark drum glass", 0, h * .446f, front - .001f, w * .222f, .002f, "navy", 32);
            glass.localRotation = Quaternion.Euler(90, 0, 0);
            Box(p, "Washer door handle", w * .248f, h * .455f, front - .001f, .034f, .125f, .002f, "paper", .01f);
            // Folded cloth is contained in the furniture's authored height and footprint.
            Box(p, "Washer folded lower towel", -w * .115f, h * .923f, -.04f, w * .60f, h * .044f, d * .56f, "linen", .02f);
            Box(p, "Washer folded upper towel", -w * .095f, h * .967f, -.04f, w * .55f, h * .042f, d * .54f, "sage", .02f);
            for (int i = 0; i < 2; i++)
                Box(p, "Towel sewn border", -w * .095f, h * .964f, d * .23f + i * .008f, w * .50f, .004f, .004f, "linen", .001f);
        }

        static void PipeRectangle(Transform p, string name, Vector3 center, float w, float d, float thickness, string mat)
        {
            foreach (float sign in new[] { -1f, 1f })
            {
                Box(p, name, center.x, center.y, center.z + sign * d * .5f, w - thickness, thickness, thickness, mat, thickness * .3f);
                Box(p, name, center.x + sign * w * .5f, center.y, center.z, thickness, thickness, d - thickness, mat, thickness * .3f);
            }
        }

        static void Leaf(Transform p, Vector3 start, Vector3 end, Vector3 outward, float width)
        {
            Vector3 sideways = Vector3.Cross(Vector3.up, outward).normalized;
            Vector3 mid = Vector3.Lerp(start, end, .59f) + outward * width * .26f;
            Vector3 low = Vector3.Lerp(start, end, .24f);
            Vector3 high = Vector3.Lerp(start, end, .82f) + outward * width * .21f;
            var vertices = new List<Vector3> { start, low - sideways * width * .38f, mid - sideways * width * .5f, high - sideways * width * .29f, end,
                high + sideways * width * .29f, mid + sideways * width * .5f, low + sideways * width * .38f, mid + outward * .019f };
            var triangles = new List<int>();
            for (int i = 0; i < 8; i++)
            {
                triangles.Add(8); triangles.Add(i); triangles.Add((i + 1) % 8);
            }
            Mesh(p, "Art Folded sage leaf", vertices, triangles, null, "sage", true);
        }

        static void Frustum(Transform p, string name, float bottomY, float topY, float bottomR, float topR, string mat, int sides, bool doubleSided)
        {
            var v = new List<Vector3>(); var t = new List<int>(); var uv = new List<Vector2>();
            // Separate strips retain a restrained low-poly facet across the lampshade and pottery.
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                int start = v.Count;
                v.Add(new Vector3(Mathf.Sin(a) * bottomR, bottomY, Mathf.Cos(a) * bottomR));
                v.Add(new Vector3(Mathf.Sin(a) * topR, topY, Mathf.Cos(a) * topR));
                v.Add(new Vector3(Mathf.Sin(b) * topR, topY, Mathf.Cos(b) * topR));
                v.Add(new Vector3(Mathf.Sin(b) * bottomR, bottomY, Mathf.Cos(b) * bottomR));
                uv.Add(new Vector2(i / (float)sides, 0)); uv.Add(new Vector2(i / (float)sides, 1));
                uv.Add(new Vector2((i + 1f) / sides, 1)); uv.Add(new Vector2((i + 1f) / sides, 0));
                Quad(t, start, start + 3, start + 2, start + 1);
            }
            Mesh(p, name, v, t, uv, mat, doubleSided);
        }

        static void Ring(Transform p, string name, Vector3 center, float outer, float inner, float height, string material, int sides)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                float a = i * Mathf.PI * 2 / sides, b = (i + 1) * Mathf.PI * 2 / sides;
                Vector3 da = new Vector3(Mathf.Sin(a), 0, Mathf.Cos(a)), db = new Vector3(Mathf.Sin(b), 0, Mathf.Cos(b));
                int k = v.Count;
                v.Add(center + da * outer - Vector3.up * height * .5f); v.Add(center + da * outer + Vector3.up * height * .5f);
                v.Add(center + db * outer + Vector3.up * height * .5f); v.Add(center + db * outer - Vector3.up * height * .5f);
                v.Add(center + da * inner - Vector3.up * height * .5f); v.Add(center + da * inner + Vector3.up * height * .5f);
                v.Add(center + db * inner + Vector3.up * height * .5f); v.Add(center + db * inner - Vector3.up * height * .5f);
                Quad(t, k, k + 3, k + 2, k + 1); Quad(t, k + 4, k + 5, k + 6, k + 7);
                Quad(t, k + 1, k + 2, k + 6, k + 5); Quad(t, k + 4, k + 7, k + 3, k);
            }
            Mesh(p, name, v, t, null, material);
        }

        static void Quad(List<int> indices, int a, int b, int c, int d, bool doubleSided = false)
        {
            indices.Add(a); indices.Add(b); indices.Add(c); indices.Add(a); indices.Add(c); indices.Add(d);
            if (doubleSided) { indices.Add(c); indices.Add(b); indices.Add(a); indices.Add(d); indices.Add(c); indices.Add(a); }
        }

        static void Mesh(Transform p, string name, List<Vector3> vertices, List<int> triangles, List<Vector2> uv, string material, bool doubleSided = false)
        {
            Transform g = NightHomeArt.Group(p, name);
            var mesh = new UnityEngine.Mesh { name = name };
            if (uv == null)
            {
                uv = new List<Vector2>(vertices.Count);
                foreach (Vector3 vertex in vertices) uv.Add(new Vector2(vertex.x * 2, vertex.y * 2 + vertex.z));
            }
            mesh.SetVertices(vertices); mesh.SetTriangles(triangles, 0);
            if (uv != null) mesh.SetUVs(0, uv);
            mesh.RecalculateNormals();
            if (doubleSided)
            {
                // Back faces need separate vertices; shared reverse triangles would cancel the lighting normals.
                int count = vertices.Count, indexCount = triangles.Count;
                var normals = new List<Vector3>(mesh.normals);
                for (int i = 0; i < count; i++)
                {
                    vertices.Add(vertices[i]); normals.Add(-normals[i]);
                    if (uv != null) uv.Add(uv[i]);
                }
                for (int i = 0; i < indexCount; i += 3)
                { triangles.Add(triangles[i + 2] + count); triangles.Add(triangles[i + 1] + count); triangles.Add(triangles[i] + count); }
                mesh.SetVertices(vertices); mesh.SetNormals(normals); mesh.SetTriangles(triangles, 0);
                if (uv != null) mesh.SetUVs(0, uv);
            }
            var colors = new Color[vertices.Count];
            for (int i = 0; i < colors.Length; i++) colors[i] = Color.white;
            mesh.colors = colors;
            mesh.RecalculateBounds();
            g.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            g.gameObject.AddComponent<MeshRenderer>().sharedMaterial = NightHomeArt.Material(material);
            // These custom meshes are unique to this build, unlike the shared helper primitives.
            var owner = g.gameObject.AddComponent<NightHomeArtResources>();
            owner.meshes.Add(mesh);
        }
    }
}
