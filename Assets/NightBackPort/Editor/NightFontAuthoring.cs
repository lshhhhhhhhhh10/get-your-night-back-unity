using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.TextCore.LowLevel;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

namespace NightBackPort.Editor
{
    public static class NightFontAuthoring
    {
        const string Folder = "Assets/NightBackPort/Resources/NightBack/Fonts/";
        [MenuItem("Night Back/Time Edition/Bake Rounded UI Fonts")]
        public static void Bake()
        {
            string authored = string.Concat(Directory.GetFiles("Assets/NightBackPort/Runtime", "*.cs", SearchOption.AllDirectories).Select(File.ReadAllText));
            authored += string.Concat(Directory.GetFiles("Assets/NightBackPort/Resources", "*.json", SearchOption.AllDirectories).Select(File.ReadAllText));
            authored += new string(Enumerable.Range(32, 95).Select(c => (char)c).ToArray());
            string chars = new string(authored.Where(c => !char.IsControl(c) && !char.IsSurrogate(c)).Distinct().OrderBy(c => c).ToArray());
            var latin = BakeFont("LilitaOne-Regular.ttf", "NightRoundedLatin-SDF.asset", chars, 1024, out string latinMissing);
            var chinese = BakeFont("ResourceHanRoundedCN-Bold.ttf", "NightRoundedCJK-SDF.asset", latinMissing, 2048, out string missing);
            var symbols = AssetDatabase.LoadAssetAtPath<FontAsset>(Folder + "NightUI-SDF.asset");
            chinese.fallbackFontAssetTable = new List<FontAsset> { symbols };
            latin.fallbackFontAssetTable = new List<FontAsset> { chinese };
            EditorUtility.SetDirty(latin); EditorUtility.SetDirty(chinese);
            const string settingsPath = "Assets/NightBackPort/GeneratedUI/NightTextSettings.asset";
            var text = AssetDatabase.LoadAssetAtPath<PanelTextSettings>(settingsPath);
            text.defaultFontAsset = latin;
            text.fallbackFontAssets = new List<FontAsset> { chinese, symbols };
            text.clearDynamicDataOnBuild = false;
            EditorUtility.SetDirty(text);
            var panel = AssetDatabase.LoadAssetAtPath<PanelSettings>("Assets/NightBackPort/NightPanelSettings.asset");
            panel.textSettings = text; EditorUtility.SetDirty(panel);
            AssetDatabase.SaveAssets();
            string unresolved = new string(chars.Where(c => !latin.HasCharacter(c, true, true)).ToArray());
            Directory.CreateDirectory("NightBackEvidence/RoundedFonts-20261012");
            File.WriteAllText("NightBackEvidence/RoundedFonts-20261012/font-bake.json", JsonUtility.ToJson(new Receipt {
                authoredCharacters = chars.Length, latinCharacters = latin.characterTable.Count, chineseCharacters = chinese.characterTable.Count,
                latinAtlases = latin.atlasTextures.Length, chineseAtlases = chinese.atlasTextures.Length,
                symbolFallback = missing, missingCharacters = unresolved
            }, true));
            if (!string.IsNullOrEmpty(unresolved)) throw new InvalidOperationException("Unresolved UI glyphs: " + unresolved);
        }
        static FontAsset BakeFont(string sourceName, string assetName, string chars, int atlasSize, out string missing)
        {
            var source = AssetDatabase.LoadAssetAtPath<Font>(Folder + sourceName);
            if (!source) throw new InvalidOperationException("Bundled font missing: " + sourceName);
            var font = AssetDatabase.LoadAssetAtPath<FontAsset>(Folder + assetName);
            if (!font) {
                font = FontAsset.CreateFontAsset(source, 48, 8, GlyphRenderMode.SDFAA, atlasSize, atlasSize, AtlasPopulationMode.Dynamic, true);
                font.name = Path.GetFileNameWithoutExtension(assetName); AssetDatabase.CreateAsset(font, Folder + assetName);
            }
            font.TryAddCharacters(chars, out missing, true);
            font.boldStyleWeight = 0; font.boldStyleSpacing = 0;
            font.material.SetFloat("_WeightBold", 0);
            if (!AssetDatabase.Contains(font.material)) AssetDatabase.AddObjectToAsset(font.material, font);
            foreach (var texture in font.atlasTextures) if (texture && !AssetDatabase.Contains(texture)) AssetDatabase.AddObjectToAsset(texture, font);
            EditorUtility.SetDirty(font.material); EditorUtility.SetDirty(font);
            return font;
        }
        [Serializable] class Receipt {
            public int authoredCharacters, latinCharacters, chineseCharacters, latinAtlases, chineseAtlases;
            public string symbolFallback, missingCharacters;
        }
    }
}
