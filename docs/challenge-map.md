# Challenge baseline / 当前挑战基线

Recorded on 2026-10-22 from the existing native implementation (latest gameplay record: Raw 107). This table documents current behavior; it does not introduce a new design decision.

| Challenge | Win/loss | Player action | Current data | Source |
| --- | --- | --- | --- | --- |
| Retrieve and return / 取物返程 | Find the phone, then return home to win; sustained parent recognition causes loss | Observe, move, interact, return | RecognitionTime = 0.25 s | NightGame_Core / NightGame_Interactions |
| Sound and attention / 声音与警觉 | Noise alone does not immediately lose the game | Control force, release, support, place gently; use real spatial sound cover | ParentWarningSeconds = 5 s | NightGame_Core / NightAcoustics |
| Household routine / 家庭作息 | No whole-run time limit or speed reward added | Listen and watch for safe outward and return routes | 90 simulation seconds per phase; four phases | NightClock / NightGame_Core |
| Action practice / 基础练习 | Separate practice completion, without a run timer | Grip, push/pull, release, support, place and stroke | 11 steps; kept unchanged | NightGame_Tutorial |
| Controller access / 手柄操作 | Does not change winning or losing | Read continuous input; release safely on pause or focus loss | Initial deadzone 0.16; settings range 0.05–0.35 | NightController / NightApp |
| Nearby instructions / 环境提示 | Does not change winning or losing | Approach objects and read instructions in the world | ReadDistance = 3.15; ReadingDurationScale = 2.2 | NightFirstNightHints / NightStoryWall |

Equal minimum/maximum entries in the parameter table record existing baselines. They are not proposed new ranges or controls. The controller range already exists in the settings UI. The 90-second routine, intro duration and reading presentation were implementation choices under earlier authorization; this documentation does not relabel them as student-confirmed designs.

关系图按文件展示，箭头记录主要调用／数据依赖，不是自动推导的完整运行时调用图。同一个 NightGame 的 partial 文件分别显示，不表示多个游戏实例。保留的声音字幕目前在正式游玩中隐藏；旧白盒、录屏驱动、验证探针和停用的卧室滤镜不纳入当前框架。
