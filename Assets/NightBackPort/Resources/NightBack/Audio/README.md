# 录音素材来源

下表六个门与物件 WAV 由 laleksic 的 **Various Sound Effects** 剪辑转换而来；连续门轴和三类脚步的独立来源见后文。

- 作者与来源：https://opengameart.org/content/various-sound-effects
- 下载地址：https://opengameart.org/sites/default/files/sounds_8.zip
- 许可：CC0 1.0，https://creativecommons.org/publicdomain/zero/1.0/
- 2026-09-09 核对：作者说明为手机录音、经 Audacity 去噪，页面明确标注 CC0。
- 处理：65 Hz 高通、8 ms 淡入、峰值限制至 0.85、32 kHz 单声道 PCM；运行时按动作切片、调整音量与播放速率。

| 本地文件 | 原文件 | 用途 |
| --- | --- | --- |
| door-handle.wav | door_open.wav | 转动把手、开门起始 |
| door-hinge.wav | door_creak_open.wav | 家长推门与新录音载入失败时的后备素材 |
| door-bump.wav | door_close.wav | 开合终点快速撞框，仅在真实接触时触发一次；运行时取 0.305–0.645 秒瞬态与余响，增益最高 3 倍，随撞击强度降低 |
| door-latch.wav | lock.wav | 门锁机械声切片 |
| drawer.wav | drawer_open.wav | 搜索抽屉时的滑动 |
| metal-drop.wav | clang_metal.wav | 叉子、铁盒落地 |

2026-09-10 本地撞击修订沿用上表已有 WAV，没有新增下载或重新宣称核对许可。主要峰值原在约 0.326 秒；运行切片将其对齐接触前沿。离线 Chrome 完整音频链测得近距离最大撞击峰值约 0.731，主峰在起播约 0.030 秒；测试条件为主音量／音效 100%，背景声关闭，非人类听感或实体设备验证。

未在本页来源表中列出的音乐和音效由项目 Web Audio 程序生成。运行时只访问本仓库资源，不请求外部音频服务。

## v0.11 连续门轴录音

- 本地文件：`hinge-real.wav`。
- 作者：**chonkdonk**；作品：**creaky door hinge.wav**。
- 原始页面：https://freesound.org/people/chonkdonk/sounds/619819/
- 页面公开提供的 HQ MP3 试听：https://cdn.freesound.org/previews/619/619819_13093778-hq.mp3
- 2026-09-10 核对：作者说明是门轴吱响、页面同时标注 CC0 / Public Domain 和 CC0 许可链接：https://creativecommons.org/publicdomain/zero/1.0/ 。使用的是公开 HQ 试听转换素材，并非声称取得登录后原始 24 bit WAV。
- 处理：选择原录音 0.55 秒起的约 16.3 秒；85 Hz 高通、6500 Hz 低通、增益 26 倍（原试听电平很低）；移除超过 0.13 秒、低于 −42 dB 的静段，保留短停顿；局部动态电平调整最大 4 倍、峰值限制 0.85；32 kHz 单声道 PCM 和起始淡入。
- 运行时：约 2.35 秒长的不同声段交叠 0.32 秒；按施力微调播放速率，摩擦控制音量与滤波，释放后约 65 ms 结束声源。没有把短促电子滑音叠在此录音上，也不把其当作角色语音。

新文件由仓库本地加载，无运行时 Freesound 请求。门把手与门锁仍沿用上表的真实录音。

## v0.12 地面脚步实录（2026-09-10 核对）

| 本地文件 | 作者／作品与原页面 | 下载输入 | 许可 |
| --- | --- | --- | --- |
| `step-wood-1.wav` 至 `step-wood-4.wav` | mikeask — [Steps in wood floor](https://opengameart.org/content/steps-in-wood-floor) | 页面提供的原始 [WAV](https://opengameart.org/sites/default/files/steps%20in%20wood%20floor.wav) | 页面明确 CC0 |
| `step-tile-1.wav` 至 `step-tile-4.wav` | RutgerMuller — [Footsteps on Tiles.wav](https://freesound.org/people/RutgerMuller/sounds/50725/) | 公开 [HQ MP3 试听](https://cdn.freesound.org/previews/50/50725_179538-hq.mp3) | 页面明确 Creative Commons 0 |
| `step-carpet-1.wav` 至 `step-carpet-4.wav` | jop9798 — [Footsteps on a carpet.wav](https://freesound.org/people/jop9798/sounds/142008/) | 公开 [HQ MP3 试听](https://cdn.freesound.org/previews/142/142008_400517-hq.mp3) | 页面明确 Creative Commons 0 |

许可：[CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/)。这是脚步录音的游戏适配，地毯原录音为靴子踩地毯，不声称是穿袜子的原始拟音；运行时减轻音量用于游戏里的轻步。两个 Freesound 来源使用公开压缩试听，没有声称下载到登录后原始 WAV。

处理脚本：`source/tools/prepare-footsteps.py`。木地板裁剪起点为 0.55 / 1.10 / 2.125 / 2.65 秒；地毯为 0.63 / 1.255 / 2.155 / 4.205 秒；瓷砖为 2.43 / 3.205 / 6.78 / 7.73 秒。每段 0.46 秒，65 Hz 高通、8500 Hz 低通、单声道 32 kHz / 16 bit，峰值最高 0.72，增益上限 14 倍，5 ms 淡入与 30 ms 淡出。四段交替并轻微变速，蹲行降低音量，家长脚步更沉。

声音从仓库载入。空间位置用 HRTF、距离衰减与遮挡低通表现；它是游戏声学近似，没有模拟完整室内反射。其余未在来源表中注明的音效与音乐仍是程序合成。


## v0.14.5 木板受力与环境掩护实录（2026-09-10 核对）

| 本地文件 | 作者、原作品与页面 | 许可 |
| --- | --- | --- |
| `floor-creak-1.wav` | Rudmer_Rotteveel — [Wood Creak Single V7](https://freesound.org/people/Rudmer_Rotteveel/sounds/506662/) | CC0 |
| `floor-creak-2.wav` | Rudmer_Rotteveel — [Wood Creak Single V9](https://freesound.org/people/Rudmer_Rotteveel/sounds/506664/) | CC0 |
| `floor-creak-3.wav` | Rudmer_Rotteveel — [Wood Creak Single V6](https://freesound.org/people/Rudmer_Rotteveel/sounds/506661/) | CC0 |
| `snore-real.wav` | Daxter31 — [snoring](https://freesound.org/people/Daxter31/sounds/114609/) | CC0 |
| `washer-spin.wav` | Zabuhailo — [wringing in the centrifuge.wav](https://freesound.org/people/Zabuhailo/sounds/144811/) | CC0 |

三条木板录音来自同一 [Wood Creaks and Squeeks 声音包](https://freesound.org/people/Rudmer_Rotteveel/packs/28378/)，作者说明为踩下木地板的吱响。逐页核对 [CC0 1.0](https://creativecommons.org/publicdomain/zero/1.0/) 许可后，下载页面公开 HQ MP3 试听。未登录下载原始 WAV，不把有损试听转换声称为无损原录音。洗衣声源自作者录制的 Siberia-2 旧式洗衣机脱水，不声称对应场景模型的品牌或型号。

处理脚本：[prepare-floor-environment.py](../../source/tools/prepare-floor-environment.py)。木板去掉前后静段，起点 0.01653／0.04584／0.02006 秒，长度 0.94941／0.78159／1.28425 秒；鼾声截取 6.1–7.8 秒，洗衣机截取稳定脱水段 16–22 秒。统一 65 Hz 高通、9000 Hz 低通、32 kHz 单声道 16 bit PCM、5 ms 淡入和 30 ms 淡出，峰值上限木板／洗衣机 0.72、鼾声 0.78。具体下载地址与输入／输出 SHA-256 见 [素材清单](recordings-v145.json)。

木板轮换三条实录，受力、轻落脚和踩响用不同长度与增益；替代之前的电子滑音。已有鞋底接触录音保留。鼾声及脱水声按游戏时间连续播放，暂停、醒来、结束窗口时淡出；续玩从保存相位进入。自身脚步／木板声在鼾声掩护时降至 60%、洗衣机有效范围内降至 55%，结束恢复；这是混音增益，与父母听觉里的 28%／30% 原掩护系数不同。

父母脚步独立于自身脚步音轨；HRTF 加适度左右电平差，隔墙保留部分高频，背后略闷，近远衰减继续。关键线索出现时降低背景音乐。环境声为便于读取原玩法而采用宽声场和温和距离衰减，是游戏声学近似，不是完整建筑声场模拟。全部 24 个录音从本仓库加载，未列出的音乐和其他音效仍为程序生成。


## v0.14.6 声传播更新

沿用上面的 24 个本地录音，没有新增下载、改动 WAV 或变更许可。新增三频段数字 RMS 测量，用于近似掩蔽；`source/tools/measure-audio-bands.py` 可重现 `source/sample-bands.js`。实际门、墙和家具的分频传播、HRTF、地面反射和短混响统一用于播放与父母听觉，取消固定范围／系数与自身音轨降音量。短混响是程序生成的近似，不是实测住宅录音。依据和限制见 [声学说明](../../docs/acoustics.md)。
