// @role Selects languages and defines settings and save records
// @calls NightPortEnglish

using System;
using System.Collections.Generic;
using UnityEngine;
namespace NightBackPort
{
    public static class NightText
    {
        [Serializable] class Entry { public string key,value; }
        [Serializable] class Table { public Entry[] entries; }
        static Dictionary<string,string> translations;
        public static bool english;
        public static string Pick(string chinese,string englishText){return english?englishText:chinese;}
        public static string Translate(string text)
        {
            if(string.IsNullOrEmpty(text)||!english)return text??"";
            var native=NightPortEnglish.Translate(text);if(native!=null)return native;
            if(text.Length>7&&text[2]==':'&&text[5]==' '&&text[6]==' ')return text.Substring(0,7)+Translate(text.Substring(7));
            if(translations==null){translations=new Dictionary<string,string>();var a=Resources.Load<TextAsset>("NightBack/english");if(a)foreach(var e in JsonUtility.FromJson<Table>(a.text).entries)translations[e.key]=e.value;}
            string result;if(translations.TryGetValue(text,out result))return result;
            return text;
        }
    }
    [Serializable] public class NightSettings
    {
        public bool english,haptics=true,adaptive=true,headBob; public float master=.7f,effects=.7f,music=.35f,ambience=.2f,sensitivity=1f,gamepadSensitivity=1f,stickDeadzone=.16f,hapticIntensity=.75f,triggerStrength=.6f,brightness=1f;
        public string playerSkin="golden-frog",parentSkin="golden-bull";
    }
    [Serializable] public class NightProgress { public int unlocked=1; }
    [Serializable] public class NightSave
    {
        public int version=1,unlocked=1; public string savedAt; public NightGame game; public NightSettings settings=new NightSettings();
    }
}
