// @role Builds shared wooden furniture details
// @calls NightHomeArt

using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    // Authored visual replacements only. Original semantic transforms remain the animation anchors.
    public static class NightHomeWood
    {
        public static bool Build(Transform root, NightFurniture f)
        {
            if (!root || f == null) return false;
            if (f.type != "cabinet" && f.type != "shelf" && f.type != "kitchen" &&
                f.type != "desk" && f.type != "table" && f.type != "tv" && f.type != "boxes") return false;
            var art = NightHomeArt.Group(root, "Art/WoodFurniture");
            if (f.type == "shelf") Shelf(root, art, f);
            else if (f.type == "desk" || f.type == "table") Table(art, f, f.type == "desk");
            else if (f.type == "tv") Television(root, art, f);
            else if (f.type == "boxes") Boxes(art, f);
            else Cabinet(root, art, f, f.h, f.type == "kitchen");
            NightHomeArt.Finish(art);
            return true;
        }

        static Transform Box(Transform p, string n, float x, float y, float z, float w, float h, float d, string m, float b = .012f)
        { return NightHomeArt.Box(p, "Art/" + n, new Vector3(x, y, z), new Vector3(w, h, d), m, b); }

        static void Legs(Transform art, float w, float d, float h, float thickness, string material)
        {
            for (int x = -1; x <= 1; x += 2)
                for (int z = -1; z <= 1; z += 2)
                    Box(art, "Leg", x * (w * .5f - thickness), h * .5f, z * (d * .5f - thickness),
                        thickness, h, thickness, material, .008f);
        }

        static void InsetPanel(Transform p, string name, float x, float y, float z, float w, float h, string fill)
        {
            if (w < .06f || h < .04f) return;
            Box(p, name + "Frame", x, y, z, w, h, .025f, "walnut", .009f);
            Box(p, name + "Inset", x, y, z + .014f, Mathf.Max(.02f, w - .045f), Mathf.Max(.02f, h - .036f), .012f, fill, .006f);
        }

        static void Pull(Transform p, Vector3 at, float width)
        {
            float half = Mathf.Min(.075f, width * .21f);
            Box(p, "PullLeftMount", at.x - half, at.y, at.z + .012f, .015f, .020f, .028f, "brass", .004f);
            Box(p, "PullRightMount", at.x + half, at.y, at.z + .012f, .015f, .020f, .028f, "brass", .004f);
            Box(p, "BrassPull", at.x, at.y, at.z + .029f, half * 2 + .013f, .014f, .013f, "brass", .004f);
        }

        static List<Transform> SemanticChildren(Transform root, string prefix)
        {
            var list = new List<Transform>();
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith(prefix)) list.Add(t);
            return list;
        }

        static bool OriginalBounds(Transform target, bool drawerFront, out Bounds result)
        {
            result = new Bounds(); bool found = false;
            float largestFace = 0;
            foreach (MeshFilter filter in target.GetComponentsInChildren<MeshFilter>(true))
            {
                if (!filter.sharedMesh || IsArt(filter.transform, target)) continue;
                Bounds meshBounds = filter.sharedMesh.bounds;
                Matrix4x4 toLocal = target.worldToLocalMatrix * filter.transform.localToWorldMatrix;
                Bounds local = new Bounds(toLocal.MultiplyPoint3x4(meshBounds.center), Vector3.zero);
                for (int x = -1; x <= 1; x += 2)
                    for (int y = -1; y <= 1; y += 2)
                        for (int z = -1; z <= 1; z += 2)
                            local.Encapsulate(toLocal.MultiplyPoint3x4(meshBounds.center + Vector3.Scale(meshBounds.extents, new Vector3(x, y, z))));
                if (drawerFront)
                {
                    // Exported Drawer transforms are at (0,0,0): the actual front offset lives in the mesh.
                    if (local.size.z > .08f || local.size.x < .15f || local.size.y < .08f || local.center.z < 0) continue;
                    float face = local.size.x * local.size.y;
                    if (face <= largestFace) continue;
                    result = local; largestFace = face; found = true;
                }
                else { if (!found) result = local; else result.Encapsulate(local); found = true; }
            }
            return found;
        }

        static bool IsArt(Transform t, Transform stop)
        {
            while (t && t != stop) { if (t.name.StartsWith("Art/")) return true; t = t.parent; }
            return false;
        }

        static void Drawer(Transform anchor, NightFurniture f, Bounds face, string fill)
        {
            var art = NightHomeArt.Group(anchor, "Art/DrawerJoinery");
            float w = face.size.x - .010f, h = face.size.y - .008f;
            float x = face.center.x, y = face.center.y;
            float z = face.center.z - .025f;
            float innerDepth = Mathf.Max(.10f, f.d - .12f);
            InsetPanel(art, "DrawerFace", x, y, z, w, h, fill);
            Pull(art, new Vector3(x, y + h * .08f, z + .022f), w);
            float bottom = face.min.y + .025f;
            Box(art, "DrawerBase", x, bottom, -.010f, w - .035f, .023f, innerDepth, "wood", .005f);
            Box(art, "DrawerLeftSide", x - w * .5f + .022f, bottom + h * .38f, -.01f, .024f, h * .76f, innerDepth, "wood", .005f);
            Box(art, "DrawerRightSide", x + w * .5f - .022f, bottom + h * .38f, -.01f, .024f, h * .76f, innerDepth, "wood", .005f);
            Box(art, "DrawerBack", x, bottom + h * .38f, -.01f - innerDepth * .5f + .012f, w - .035f, h * .76f, .024f, "wood", .005f);
            // Quiet readable contents become visible only when the original search animation pulls the drawer.
            Box(art, "FoldedCloth", x - w * .13f, bottom + .029f, -.035f, w * .48f, .034f, Mathf.Min(.24f, innerDepth * .60f), "linen", .012f);
            Box(art, "Notebook", x + w * .12f, bottom + .060f, -.015f, w * .43f, .025f, Mathf.Min(.20f, innerDepth * .52f), "navy", .005f);
            NightHomeArt.Finish(art);
        }

        static void Cabinet(Transform root, Transform art, NightFurniture f, float height, bool kitchen)
        {
            float w = f.w, d = f.d, foot = Mathf.Min(.11f, height * .16f), top = .055f;
            string fill = kitchen ? "sage" : "wood";
            Legs(art, w, d, foot + .025f, Mathf.Min(.070f, w * .12f), "walnut");
            Box(art, "BottomRail", 0, foot + .025f, 0, w - .045f, .055f, d - .040f, "walnut");
            Box(art, "BackPanel", 0, (height + foot) * .5f, -d * .5f + .025f, w - .04f, height - foot - .055f, .035f, "walnut", .005f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(art, "CabinetSide", side * (w * .5f - .02f), (height + foot) * .5f, -.015f, .04f, height - foot - .035f, d - .035f, "walnut");
                Box(art, "SideInset", side * (w * .5f - .013f), (height + foot) * .5f, -.024f, .012f, height - foot - .135f, d - .16f, fill, .006f);
            }
            Box(art, "TopShadowReveal", 0, height - top - .011f, 0, w - .026f, .026f, d - .020f, "walnut", .005f);
            Box(art, kitchen ? "HonedStoneTop" : "RoundedWoodTop", 0, height - top * .5f, 0, w, top, d, kitchen ? "ceramic" : "wood", .017f);
            var drawers = SemanticChildren(root, "Drawer:");
            float low = height * .40f, high = height * .70f;
            if (drawers.Count > 0)
            {
                bool got = false;
                foreach (Transform drawer in drawers)
                {
                    Bounds face;
                    if (!OriginalBounds(drawer, true, out face)) continue;
                    Drawer(drawer, f, face, fill);
                    // All current source drawers share the furniture transform; conversion keeps this valid for future pivots.
                    float y0 = root.InverseTransformPoint(drawer.TransformPoint(new Vector3(face.center.x, face.min.y, face.center.z))).y;
                    float y1 = root.InverseTransformPoint(drawer.TransformPoint(new Vector3(face.center.x, face.max.y, face.center.z))).y;
                    if (!got) { low = y0; high = y1; got = true; } else { low = Mathf.Min(low, y0); high = Mathf.Max(high, y1); }
                }
                float z = d * .5f - .025f;
                Box(art, "UpperDrawerRail", 0, high + .013f, z, w - .04f, .025f, .035f, "walnut", .005f);
                Box(art, "LowerDrawerRail", 0, low - .013f, z, w - .04f, .025f, .035f, "walnut", .005f);
                int bays = Mathf.Max(1, drawers.Count);
                for (int i = 0; i < bays; i++)
                {
                    float width = (w - .055f) / bays, x = -w * .5f + .0275f + width * (i + .5f);
                    float bottomHeight = low - foot - .05f;
                    InsetPanel(art, "LowerDoor", x, foot + .03f + bottomHeight * .5f, z, width - .014f, bottomHeight, fill);
                    float upperHeight = height - top - high - .035f;
                    InsetPanel(art, "UpperFrieze", x, high + .020f + upperHeight * .5f, z, width - .014f, upperHeight, fill);
                    if (i > 0) Box(art, "BayDivider", x - width * .5f, (height + foot) * .5f, -.02f, .022f, height - foot - .07f, d - .10f, "walnut", .005f);
                }
            }
            else
            {
                int doors = w > .95f ? 2 : 1;
                for (int i = 0; i < doors; i++)
                {
                    float width = (w - .070f) / doors, x = -w * .5f + .035f + width * (i + .5f);
                    InsetPanel(art, "Door", x, (foot + height - top) * .5f, d * .5f - .025f, width - .014f, height - foot - top - .035f, fill);
                    Pull(art, new Vector3(x, height * .70f, d * .5f - .007f), width);
                }
            }
        }

        static void Book(Transform anchor, int index)
        {
            Bounds source;
            if (!OriginalBounds(anchor, false, out source)) source = new Bounds(Vector3.zero, new Vector3(.075f, .21f, .24f));
            var art = NightHomeArt.Group(anchor, "Art/BoundBook");
            float w = source.size.x, d = source.size.z, h = source.size.y * (1 - (index % 4) * .035f);
            Vector3 c = source.center; c.y = source.min.y + h * .5f;
            string cover = index % 4 == 0 ? "navy" : index % 4 == 1 ? "sage" : index % 4 == 2 ? "leather" : "walnut";
            Box(art, "PaperBlock", c.x, c.y, c.z - .005f, w - .012f, h - .014f, d - .024f, "paper", .002f);
            for (int side = -1; side <= 1; side += 2)
                Box(art, "HardCover", c.x + side * (w * .5f - .003f), c.y, c.z, .006f, h, d, cover, .002f);
            Box(art, "RoundedSpine", c.x, c.y, c.z + d * .5f - .005f, w, h, .011f, cover, .004f);
            for (int band = -1; band <= 1; band += 2)
                Box(art, "SpineRule", c.x, c.y + band * h * .30f, c.z + d * .5f + .001f, w * .73f, .003f, .002f, "paper", .0005f);
            Box(art, "SpineLabel", c.x, c.y + .004f, c.z + d * .5f + .001f, w * .43f, h * .17f, .002f, "paper", .001f);
            NightHomeArt.Finish(art);
        }

        static void Shelf(Transform root, Transform art, NightFurniture f)
        {
            float w = f.w, h = f.h, d = f.d;
            Legs(art, w, d, .105f, .06f, "walnut");
            Box(art, "RecessedBack", 0, h * .5f, -d * .5f + .025f, w - .075f, h - .14f, .032f, "wood", .004f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(art, "Stile", side * (w * .5f - .026f), h * .5f + .025f, 0, .052f, h - .05f, d - .018f, "walnut", .013f);
                Box(art, "StileFace", side * (w * .5f - .026f), h * .5f + .025f, d * .5f - .018f, .058f, h - .08f, .028f, "wood", .008f);
            }
            Box(art, "Crown", 0, h - .027f, 0, w, .054f, d, "wood", .012f);
            Box(art, "BaseMoulding", 0, .09f, 0, w - .01f, .075f, d - .01f, "wood", .012f);
            var books = SemanticChildren(root, "Book:");
            var shelves = new List<float>();
            for (int i = 0; i < books.Count; i++)
            {
                Bounds b;
                if (!OriginalBounds(books[i], false, out b)) b = new Bounds(Vector3.zero, new Vector3(.075f, .21f, .24f));
                float bottom = root.InverseTransformPoint(books[i].TransformPoint(new Vector3(b.center.x, b.min.y, b.center.z))).y;
                bool exists = false; foreach (float y in shelves) if (Mathf.Abs(y - bottom) < .035f) exists = true;
                if (!exists) shelves.Add(bottom);
                Book(books[i], i);
            }
            if (shelves.Count == 0) for (int i = 0; i < 4; i++) shelves.Add(.15f + i * (h - .32f) / 4);
            shelves.Sort();
            for (int i = 0; i < shelves.Count; i++)
            {
                float y = shelves[i];
                Box(art, "ShelfBoard", 0, y - .022f, 0, w - .08f, .043f, d - .055f, "wood", .008f);
                Box(art, "ShelfEdge", 0, y - .020f, d * .5f - .025f, w - .075f, .048f, .035f, "walnut", .007f);
            }
            // Existing books occupy the left two thirds. The larger bookcases get a restrained right-hand still life.
            if (w > 1.25f && shelves.Count > 1)
            {
                float x = w * .31f, y = shelves[1];
                Box(art, "LinenArchiveBox", x, y + .090f, -.025f, w * .18f, .18f, d * .54f, "linen", .015f);
                Box(art, "ArchiveBoxLid", x, y + .183f, -.025f, w * .185f, .025f, d * .555f, "linen", .008f);
                Box(art, "ArchiveLabelHolder", x, y + .098f, d * .245f, .075f, .045f, .008f, "brass", .003f);
                Box(art, "ArchiveLabel", x, y + .098f, d * .245f + .005f, .058f, .029f, .004f, "paper", .001f);
                if (shelves.Count > 2)
                {
                    float baseY = shelves[2];
                    NightHomeArt.Cylinder(art, "Art/CeramicPot", new Vector3(x, baseY + .071f, -.01f), .064f, .142f, "ceramic", 14);
                    NightHomeArt.Cylinder(art, "Art/PotLip", new Vector3(x, baseY + .145f, -.01f), .068f, .013f, "ceramic", 14);
                    NightHomeArt.Cylinder(art, "Art/PotOpening", new Vector3(x, baseY + .152f, -.01f), .052f, .003f, "charcoal", 14);
                }
            }
        }

        static void Table(Transform art, NightFurniture f, bool desk)
        {
            float w = f.w, d = f.d, h = f.h, top = .064f;
            float leg = Mathf.Min(.075f, w * .065f), topY = h - .02f;
            Legs(art, w - .02f, d - .02f, topY - top, leg, "walnut");
            Box(art, "SolidWoodTop", 0, topY - top * .5f, 0, w, top, d, "wood", .020f);
            for (int side = -1; side <= 1; side += 2)
            {
                Box(art, "LongApron", 0, topY - top - .055f, side * (d * .5f - .08f), w - .12f, .11f, .045f, "walnut", .008f);
                Box(art, "EndApron", side * (w * .5f - .08f), topY - top - .055f, 0, .045f, .11f, d - .14f, "walnut", .008f);
            }
            if (desk)
            {
                Box(art, "LeatherWritingMat", 0, h - .014f, .035f, w * .52f, .008f, d * .60f, "leather", .010f);
                Box(art, "MatBorder", 0, h - .009f, .035f - d * .27f, w * .46f, .002f, .004f, "brass", .001f);
                var pencil = Box(art, "RestingPencil", w * .13f, h - .005f, d * .21f, .16f, .006f, .006f, "wood", .001f);
                pencil.localRotation = Quaternion.Euler(0, -12, 0);
                Box(art, "DeskBackStretcher", 0, h * .25f, -d * .5f + .09f, w - .13f, .06f, .05f, "walnut", .008f);
            }
            else if (h < .60f)
            {
                float shelfY = h * .31f;
                Box(art, "CoffeeTableShelf", 0, shelfY, 0, w - .15f, .036f, d - .15f, "walnut", .008f);
                Box(art, "CoffeeTableBook", -w * .17f, shelfY + .036f, .01f, w * .33f, .035f, d * .40f, "navy", .006f);
                Box(art, "CoffeeTablePages", -w * .17f, shelfY + .037f, d * .19f + .01f, w * .31f, .024f, .014f, "paper", .003f);
                Box(art, "WovenTableRunner", w * .18f, h - .014f, 0, w * .28f, .008f, d * .78f, "linen", .004f);
            }
            else
            {
                Box(art, "LinenRunner", 0, h - .014f, 0, w * .68f, .008f, d * .27f, "linen", .005f);
                for (int side = -1; side <= 1; side += 2)
                    NightHomeArt.Cylinder(art, "Art/CeramicCoaster", new Vector3(side * w * .28f, h - .005f, side * d * .25f), .064f, .008f, "ceramic", 16);
            }
        }

        static void Television(Transform root, Transform art, NightFurniture f)
        {
            float baseH = f.h * .35f;
            Cabinet(root, art, f, baseH, false);
            float screenW = f.w * .88f, screenH = f.h * .53f, screenTop = f.h - .025f;
            float screenY = screenTop - screenH * .5f;
            Box(art, "TVStandFoot", 0, baseH + .015f, -.018f, screenW * .42f, .030f, f.d * .52f, "charcoal", .011f);
            Box(art, "TVStandNeck", 0, baseH + .14f, -.060f, .095f, .26f, .065f, "charcoal", .008f);
            Box(art, "ScreenHousing", 0, screenY, -.060f, screenW, screenH, .070f, "charcoal", .023f);
            Box(art, "DarkGlass", 0, screenY + .007f, -.020f, screenW - .045f, screenH - .055f, .012f, "navy", .012f);
            Box(art, "ScreenLowerBezel", 0, screenY - screenH * .5f + .025f, -.012f, screenW - .018f, .025f, .018f, "charcoal", .005f);
            Box(art, "StandbyMark", screenW * .40f, screenY - screenH * .5f + .025f, .001f, .012f, .004f, .003f, "brass", .001f);
        }

        static void StorageBox(Transform p, string name, Vector3 center, Vector3 size, string material)
        {
            var g = NightHomeArt.Group(p, "Art/" + name);
            g.localPosition = center;
            Box(g, "BoxBody", 0, 0, 0, size.x, size.y - .02f, size.z, material, .016f);
            Box(g, "Lid", 0, size.y * .5f - .013f, 0, size.x, .026f, size.z, "linen", .010f);
            Box(g, "LidSeam", 0, size.y * .5f - .034f, size.z * .5f + .001f, size.x - .035f, .005f, .003f, "walnut", .001f);
            Box(g, "PaperLabel", -size.x * .15f, size.y * .02f, size.z * .5f + .002f, size.x * .24f, size.y * .20f, .004f, "paper", .003f);
            Box(g, "FoldedHandle", size.x * .18f, size.y * .02f, size.z * .5f + .004f, size.x * .19f, .025f, .009f, "leather", .005f);
        }

        static void Boxes(Transform art, NightFurniture f)
        {
            float h0 = f.h * .46f, h1 = f.h * .32f, h2 = f.h * .22f;
            StorageBox(art, "LargeStorageBox", new Vector3(0, h0 * .5f, 0), new Vector3(f.w, h0, f.d), "linen");
            StorageBox(art, "MiddleStorageBox", new Vector3(-f.w * .085f, h0 + h1 * .5f, -.025f), new Vector3(f.w * .80f, h1, f.d * .88f), "linen");
            StorageBox(art, "SmallStorageBox", new Vector3(f.w * .09f, h0 + h1 + h2 * .5f, -.03f), new Vector3(f.w * .61f, h2, f.d * .66f), "linen");
        }
    }
}
