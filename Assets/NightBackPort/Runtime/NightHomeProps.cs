// @role Builds small household props
// @calls NightHomeArt

using System;
using UnityEngine;

namespace NightBackPort
{
    // Preserve the exported incident pivots and their authored local geometry. The game owns
    // every rescue pose; this pass only gives those surfaces the shared bedroom finish.
    public static class NightHomeProps
    {
        const string AppliedMarker = "Art Props Applied";

        public static void Apply(Transform house)
        {
            if (!house) return;
            // Take a snapshot before adding decoration so a new child is never treated as a source.
            var nodes = house.GetComponentsInChildren<Transform>(true);
            foreach (var node in nodes)
            {
                string name = node.name;
                if (name == "IncidentObject:vase") FinishVase(node);
                else if (name == "IncidentObject:pencils") FinishPencilCup(node);
                else if (name == "IncidentObject:fork") FinishFork(node);
                else if (name == "IncidentObject:tin") FinishTin(node);
                else if (name.StartsWith("Pencil:")) FinishPencil(node);
                else if (name == "Padlock") FinishPadlock(node);
                else if (name == "Lure:radio" || name == "Lure:back-radio") FinishRadio(node);
                else if (name == "Lure:toy") FinishToy(node);
            }
        }

        static bool Begin(Transform root)
        {
            if (root.Find(AppliedMarker)) return false;
            NightHomeArt.Group(root, AppliedMarker);
            return true;
        }

        // Names are the stable exported material slots, not visual-object names. Prefer the
        // renderer name because persisted editor material assets may have acquired a suffix.
        static int SourceSlot(Renderer renderer, Material source)
        {
            int slot;
            if (renderer.name.StartsWith("Material ") && int.TryParse(renderer.name.Substring(9), out slot)) return slot;
            if (source && source.name.StartsWith("Native ") && int.TryParse(source.name.Substring(7), out slot)) return slot;
            return -1;
        }

        static void Recolor(Transform root, Func<int, string> palette)
        {
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                // A repeated or later pass must not recolor the new helper geometry.
                if (renderer.name.StartsWith("Art")) continue;
                bool lureLamp = false;
                for (var ancestor = renderer.transform; ancestor; ancestor = ancestor.parent)
                    if (ancestor.name == "LureLamp") { lureLamp = true; break; }
                if (lureLamp) continue;
                var source = renderer.sharedMaterials;
                var replacement = new Material[source.Length];
                for (int i = 0; i < source.Length; i++)
                    replacement[i] = NightHomeArt.Material(palette(SourceSlot(renderer, source[i])));
                renderer.sharedMaterials = replacement;
            }
        }

        static void FinishVase(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot =>
            {
                switch (slot)
                {
                    case 60: return "ceramic"; // Authored hollow pottery profile.
                    case 61: return "paper";   // Cream inlay and soft flower petals.
                    case 62: case 63: return "sage";
                    case 64: return "brass";   // Small muted ochre flower centres.
                    default: return "ceramic";
                }
            });
        }

        static void FinishPencilCup(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot => slot == 77 ? "brass" : slot == 78 ? "wood" : "leather");
        }

        static void FinishPencil(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot =>
            {
                switch (slot)
                {
                    case 79: return "wood";
                    case 84: return "navy";
                    case 85: return "sage";
                    case 80: return "brass";   // Ferrule.
                    case 74: return "leather"; // Desaturated warm eraser.
                    case 81: return "paper";   // Exposed pale wood.
                    case 82: return "charcoal";
                    case 83: return "walnut";  // Fine shaft markings.
                    default: return "wood";
                }
            });
        }

        static void FinishFork(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot => slot == 140 ? "walnut" : "brass");
            // Keep the original thin tines, handle relief and 90-degree rest rotation exactly.
        }

        static void FinishTin(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot =>
            {
                switch (slot)
                {
                    case 149: case 152: return "navy";
                    case 150: case 151: case 155: return "brass";
                    case 153: return "paper";
                    case 154: return "walnut";
                    default: return "navy";
                }
            });
            // TinLid remains a separate descendant. Its original seam, inset panel, front
            // label and latch already provide the needed detail without altering the rescue.
        }

        static void FinishPadlock(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot => "brass");
            // Its body and shackle share one authored mesh; retain both and the visibility pivot.
        }

        static void FinishRadio(Transform root)
        {
            if (!Begin(root)) return;
            bool hasExportedCase = false;
            foreach (var renderer in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (renderer.name.StartsWith("Art")) continue;
                var original = renderer.sharedMaterials;
                if (original.Length == 1 && SourceSlot(renderer, original[0]) == 104)
                {
                    // Replace only the known wooden case. The antenna, speaker and LureLamp
                    // remain on their original nodes, including its per-frame emission feedback.
                    renderer.enabled = false;
                    hasExportedCase = true;
                }
            }
            Recolor(root, slot =>
            {
                switch (slot)
                {
                    case 104: return "walnut";
                    case 105: return "linen";
                    case 106: case 107: return "brass";
                    default: return "walnut";
                }
            });
            if (!hasExportedCase) return;

            // Exact original case bounds: x +/- .15, y 0.. .23, z +/- .08.
            NightHomeArt.Box(root, "Art Radio rounded walnut case", new Vector3(0, .115f, 0),
                new Vector3(.30f, .23f, .16f), "walnut", .012f);
            // This frame and grille stay inside the existing front-detail envelope z <= .09.
            NightHomeArt.Box(root, "Art Radio grille left", new Vector3(-.118f, .13f, .085f),
                new Vector3(.006f, .138f, .006f), "brass", .0015f);
            NightHomeArt.Box(root, "Art Radio grille right", new Vector3(.018f, .13f, .085f),
                new Vector3(.006f, .138f, .006f), "brass", .0015f);
            foreach (float y in new[] { .061f, .199f })
                NightHomeArt.Box(root, "Art Radio grille edge", new Vector3(-.05f, y, .085f),
                    new Vector3(.142f, .006f, .006f), "brass", .0015f);
            for (int i = 0; i < 5; i++)
                NightHomeArt.Box(root, "Art Radio woven grille", new Vector3(-.05f, .078f + i * .026f, .0885f),
                    new Vector3(.126f, .002f, .001f), "walnut", .0003f);
            // A small warm-ivory tuning scale reads as a household appliance at walking distance.
            NightHomeArt.Box(root, "Art Radio tuning scale", new Vector3(.093f, .085f, .0815f),
                new Vector3(.052f, .032f, .003f), "paper", .003f);
            for (int i = 0; i < 3; i++)
                NightHomeArt.Box(root, "Art Radio tuning mark", new Vector3(.078f + i * .015f, .085f, .0835f),
                    new Vector3(.002f, i == 1 ? .018f : .012f, .001f), "walnut", .0003f);
        }

        static void FinishToy(Transform root)
        {
            if (!Begin(root)) return;
            Recolor(root, slot =>
            {
                switch (slot)
                {
                    case 108: return "linen";
                    case 109: return "ceramic";
                    case 110: return "brass";
                    case 111: return "charcoal";
                    default: return "linen";
                }
            });
        }
    }
}
