// @role Tracks the four-phase household routine
// @tune RealSecondsPerPhase:float:90-90:Baseline simulation seconds per routine phase; cycle also uses 360 seconds, so do not tune this in isolation.

using System;
using UnityEngine;

namespace NightBackPort
{
    // 时间改变可利用的声音、照明和家长路线；经过时间本身永远不会判负。
    [Serializable] public class NightClock
    {
        public const float RealSecondsPerPhase = 90f;
        public const float GameSecondsPerRealSecond = 10f;
        public float departureSeconds, elapsedSeconds;
        public int announcedPhase = -1, warnedCycle = -1;
        public NightClock(float departure = 0) { departureSeconds = Mathf.Max(0, departure); }
        public float totalSeconds { get { return departureSeconds + elapsedSeconds; } }
        public int cycle { get { return Mathf.FloorToInt(totalSeconds / 360f); } }
        public int phaseIndex { get { return Mathf.FloorToInt(totalSeconds / RealSecondsPerPhase) % 4; } }
        public float phaseAge { get { return totalSeconds % RealSecondsPerPhase; } }
        public float nextChange { get { return RealSecondsPerPhase - phaseAge; } }
        public bool washerActive { get { return phaseIndex == 1; } }
        public bool inspectionSoon { get { return phaseIndex == 1 && nextChange <= 12f; } }
        public float lightFactor { get { return phaseIndex == 2 ? 1.12f : phaseIndex == 3 ? .70f : phaseIndex == 1 ? .90f : 1f; } }
        public string phaseName { get { return new[] { "入睡 · 听长鼾声", "洗衣 · 借空间声掩护", "喝水 · 走廊检查", "熄灯 · 观察暗处路线" }[phaseIndex]; } }
        public string displayTime
        {
            get { int minutes = (22 * 60 + 30 + Mathf.FloorToInt(totalSeconds / 6f)) % 1440; return (minutes / 60).ToString("00") + ":" + (minutes % 60).ToString("00"); }
        }
        public void Tick(float worldDelta) { elapsedSeconds += Mathf.Max(0, worldDelta); }
    }
}
