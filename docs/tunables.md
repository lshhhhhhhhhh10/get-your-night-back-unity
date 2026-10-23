# Tunables

Generated from `@tune` headers by the menu `CGDD/Export Trace Docs`. Hand edits are overwritten on the next export.

| Script | Field | Type | Range | Default | Note |
| --- | --- | --- | --- | --- | --- |
| NightBedroomTitle | IntroDuration | float | 4.8-4.8 | 4.8 | Baseline duration of getting out of bed; not changed. |
| NightClock | RealSecondsPerPhase | float | 90-90 | 90 | Baseline simulation seconds per routine phase; cycle also uses 360 seconds, so do not tune this in isolation. |
| NightController | deadzone | float | 0.05-0.35 | 0.16 | Initial controller deadzone 0.16; NightApp replaces it each frame with settings.stickDeadzone; settings UI already supports this range. |
| NightFirstNightHints | ReadDistance | float | 3.15-3.15 | 3.15 | Baseline nearby reading distance in world units; not changed. |
| NightGame_Core | RecognitionTime | float | 0.25-0.25 | 0.25 | Baseline seconds of sustained recognition before being caught; not changed. |
| NightGame_Core | ParentWarningSeconds | float | 5-5 | 5 | Baseline warning before a parent leaves bed; not changed. |
| NightStoryWall | ReadingDurationScale | float | 2.2-2.2 | 2.2 | Baseline multiplier for story-wall reading duration; not changed. |

