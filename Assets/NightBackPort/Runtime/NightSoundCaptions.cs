// @role Retains the optional sound-caption implementation
// @calls NightNavigation

using System;
using System.Collections.Generic;
using UnityEngine;

namespace NightBackPort
{
    // source/main.js sound/updateHearing + source/spatial-audio.js.
    // This is a listener-side display queue. It never changes hearing, masking, AI or sound strength.
    public sealed class NightSoundCaptions
    {
        public const int Capacity = 5;
        public const float Lifetime = 2.2f, MinimumGain = .012f, MinimumSourceDistance = .6f;
        public const float ImpactPriorityTime = .45f, FootstepPriorityTime = 1f;

        sealed class Cue
        {
            public string kind;
            public Vector3 position;
            public float at;
        }
        struct CaptionLabel
        {
            public string zh, en;
            public CaptionLabel(string chinese, string english) { zh = chinese; en = english; }
        }
        static readonly Dictionary<string, CaptionLabel> Labels = new Dictionary<string, CaptionLabel>(StringComparer.Ordinal)
        {
            { "lockPin", new CaptionLabel("弹子卡入", "Pin setting") },
            { "lockScrape", new CaptionLabel("锁芯刮响", "Lock scraping") },
            { "parentStep", new CaptionLabel("脚步", "Footsteps") },
            { "snore", new CaptionLabel("鼾声", "Snoring") },
            { "bed", new CaptionLabel("床板声", "Bed creaking") },
            { "doorSoft", new CaptionLabel("推门声", "Door moving") },
            { "doorCreak", new CaptionLabel("门轴声", "Hinge creaking") },
            { "doorBump", new CaptionLabel("门框撞击", "Door hitting the frame") },
            { "latch", new CaptionLabel("门锁声", "Door latch") },
            { "crash", new CaptionLabel("花瓶落地", "Vase hitting the floor") },
            { "pencilDrop", new CaptionLabel("文具滚落", "Pencils rolling") },
            { "metalDrop", new CaptionLabel("金属碰撞", "Metal clattering") },
            { "toy", new CaptionLabel("玩具声", "Toy sound") },
            { "radio", new CaptionLabel("收音机", "Radio") },
            { "washer", new CaptionLabel("洗衣机", "Washing machine") },
            { "catMeow", new CaptionLabel("猫叫", "Meow") },
            { "catChirp", new CaptionLabel("猫叫", "Meow") },
            { "parentGiggle", new CaptionLabel("忍不住的笑声", "A stifled laugh") }
        };
        readonly List<Cue> cues = new List<Cue>(Capacity);
        NightGame owner;
        public int Count { get { return cues.Count; } }

        public void Clear() { cues.Clear(); owner = null; }
        void Bind(NightGame game)
        {
            if (ReferenceEquals(owner, game)) return;
            cues.Clear(); owner = game;
        }
        void Expire(float now)
        {
            while (cues.Count > 0 && now - cues[0].at > Lifetime) cues.RemoveAt(0);
        }
        public void Observe(NightGame game, NightEvent sound, float now)
        {
            Bind(game); Expire(now);
            if (game == null || sound == null || !game.active || game.status != "playing" || sound.strength <= 0 || !string.IsNullOrEmpty(sound.text)) return;
            if (!Labels.ContainsKey(sound.kind ?? "")) return;
            NightSoundPath path = game.SoundPath(sound.kind, sound.position, game.ListenerPosition);
            AddAudible(sound.kind, sound.position, now, path, game.player);
        }
        void AddAudible(string kind, Vector3 source, float now, NightSoundPath path, Vector3 listener)
        {
            if (!Labels.ContainsKey(kind ?? "") || path == null || !(path.gain > MinimumGain) || NightGeometry.Distance(listener, source) <= MinimumSourceDistance) return;
            cues.Add(new Cue { kind = kind, position = source, at = now });
            if (cues.Count > Capacity) cues.RemoveAt(0);
        }
        Cue Latest(float now)
        {
            for (int i = cues.Count - 1; i >= 0; i--) if (cues[i].kind == "doorBump" && now - cues[i].at < ImpactPriorityTime) return cues[i];
            for (int i = cues.Count - 1; i >= 0; i--) if (cues[i].kind == "parentStep" && now - cues[i].at < FootstepPriorityTime) return cues[i];
            return cues.Count == 0 ? null : cues[cues.Count - 1];
        }

        // yaw is the player's actual look yaw (NightApp.yaw), not the move-facing NightGame.heading.
        // Unity faces +Z at yaw zero; adapt the source's -Z convention while retaining its .38 sector boundary.
        public string Read(NightGame game, float yawRadians, float now, bool visible, bool english)
        {
            Bind(game); Expire(now);
            if (!visible || game == null || !game.active || game.status != "playing") { Clear(); return ""; }
            Cue cue = Latest(now); if (cue == null) return "";
            NightSoundPath path = game.SoundPath(cue.kind, cue.position, game.ListenerPosition);
            // Recheck the selected cue after movement or a door change. Do not reveal an older source instead.
            return Format(cue.kind, path, game.player, yawRadians, english);
        }
        static string Format(string kind, NightSoundPath path, Vector3 listener, float yawRadians, bool english)
        {
            CaptionLabel label;
            if (path == null || !(path.gain > MinimumGain) || !Labels.TryGetValue(kind ?? "", out label)) return "";
            string range = path.distance < 2 ? Pick("很近", "very close", english) : path.distance < 6 ? Pick("附近", "nearby", english) : Pick("远处", "far away", english);
            string route = path.route == "doorway" ? Pick("门洞传来", "through a doorway", english) : path.blocked ? Pick("隔挡低沉", "muffled by cover", english) : Pick("清晰直达", "clear and direct", english);
            return Direction(path.arrival, listener, yawRadians, english) + " · " + range + " · " + route + " ｜ " + (english ? label.en : label.zh);
        }
        static string Pick(string zh, string en, bool english) { return english ? en : zh; }
        public static string Direction(Vector3 arrival, Vector3 listener, float yawRadians, bool english)
        {
            float dx = arrival.x - listener.x, dz = arrival.z - listener.z;
            float distance = Mathf.Sqrt(dx * dx + dz * dz);
            float side = dx * Mathf.Cos(yawRadians) - dz * Mathf.Sin(yawRadians);
            float front = dx * Mathf.Sin(yawRadians) + dz * Mathf.Cos(yawRadians);
            if (distance < .3f) return Pick("身边", "Beside you", english);
            if (Mathf.Abs(side) < distance * .38f) return front >= 0 ? Pick("前方", "Ahead", english) : Pick("身后", "Behind you", english);
            return side > 0 ? front >= 0 ? Pick("右前方", "Ahead, right", english) : Pick("右后方", "Behind, right", english)
                : front >= 0 ? Pick("左前方", "Ahead, left", english) : Pick("左后方", "Behind, left", english);
        }

#if UNITY_EDITOR
        // Isolated display checks. These do not drive or alter the active app or any saved game.
        public static List<string> ValidateSourceParity()
        {
            var passed = new List<string>(); var queue = new NightSoundCaptions();
            var origin = Vector3.zero; var source = new Vector3(3, 0, 3);
            var path = new NightSoundPath { gain = .4f, distance = 4, arrival = source, route = "direct" };
            Check(Direction(source, origin, 0, false) == "右前方" && Direction(source, origin, Mathf.PI, false) == "左后方", "转头互换前后左右");
            Check(Direction(new Vector3(0, 0, 3), origin, 0, false) == "前方" && Direction(new Vector3(0, 0, -3), origin, 0, false) == "身后" && Direction(new Vector3(.1f, 0, .1f), origin, 0, false) == "身边", "原方向区间与近身阈值");
            passed.Add("声音方向：按玩家视线转头，Unity朝向适配保留原方向区间");
            var quiet = new NightSoundPath { gain = MinimumGain, distance = 4, arrival = source, route = "direct" };
            queue.AddAudible("parentStep", source, 0, quiet, origin);
            queue.AddAudible("parentStep", new Vector3(.6f, 0, 0), 0, path, origin);
            queue.AddAudible("phoneRing", source, 0, path, origin);
            Check(queue.Count == 0, "不可闻、近身和原词表外事件不暴露");
            passed.Add("字幕入队：严格gain>.012、源距>.6与原18类词表");
            for (int i = 0; i < 9; i++) queue.AddAudible("washer", source, i * .01f, path, origin);
            Check(queue.Count == 5 && Mathf.Abs(queue.cues[0].at - .04f) < .0001f, "只保留最近五条");
            queue.Expire(2.3f); Check(queue.Count == 0, "真实2.2秒后过期");
            passed.Add("有限队列：最多5条、真实2.2秒寿命");
            queue.AddAudible("parentStep", source, .1f, path, origin); queue.AddAudible("washer", source, .2f, path, origin);
            Check(queue.Latest(.3f).kind == "parentStep" && queue.Latest(1.101f).kind == "washer", "脚步1秒优先窗口");
            queue.Clear(); queue.AddAudible("doorBump", source, .1f, path, origin); queue.AddAudible("bed", source, .11f, path, origin);
            Check(queue.Latest(.2f).kind == "doorBump" && queue.Latest(.551f).kind == "bed", "撞框450毫秒后才显示翻身回应");
            passed.Add("声源优先级：撞框450毫秒、家长脚步1秒，其后最新声音");
            path.arrival = new Vector3(-2, 0, 2); path.distance = 7; path.route = "doorway"; path.blocked = true;
            Check(Format("parentStep", path, origin, 0, false) == "左前方 · 远处 · 门洞传来 ｜ 脚步", "使用门洞到达方向和路径距离");
            Check(Format("parentStep", path, origin, 0, true) == "Ahead, left · far away · through a doorway ｜ Footsteps", "英文词表");
            path.route = "direct"; path.distance = 1.9f;
            Check(Format("bed", path, origin, 0, false).Contains("很近 · 隔挡低沉"), "近距与隔挡");
            path.blocked = false; path.distance = 2;
            Check(Format("bed", path, origin, 0, false).Contains("附近 · 清晰直达"), "清晰与2单位距离边界");
            path.gain = MinimumGain;
            Check(Format("bed", path, origin, 0, false) == "", "当前已听不到即隐藏");
            passed.Add("空间与双语：门洞方向、传播距离、隔挡状态和动态不可闻隐藏");
            var game = new NightGame(0); game.Start(); queue.Clear();
            queue.Observe(game, new NightEvent { kind = "parentStep", position = game.player + Vector3.right, strength = 40 }, 0);
            Check(queue.Count == 1 && queue.Read(game, 0, .1f, true, false).Length > 0, "通过真实SoundPath接入");
            Check(queue.Read(game, 0, .2f, false, false) == "" && queue.Count == 0, "暂停清空可见字幕");
            queue.Observe(game, new NightEvent { kind = "parentStep", position = game.player + Vector3.right, strength = 40 }, .3f);
            var nextGame = new NightGame(0); nextGame.Start();
            Check(queue.Read(nextGame, 0, .4f, true, false) == "" && queue.Count == 0, "新一夜不继承上一夜声源");
            passed.Add("真实SoundPath连接、暂停清理与换夜隔离");
            return passed;
        }
        static void Check(bool value, string message) { if (!value) throw new InvalidOperationException("Sound caption check failed: " + message); }
#endif
    }
}
