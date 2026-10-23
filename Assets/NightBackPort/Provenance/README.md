# 角色来源与处理

玩家角色来自学生在 2026-09-09 提供的 `peak-characters.zip`。附件内层 `PEAK Characters.rar` 包含 Blender / FBX 网格和贴图；原文件没有骨骼与动画。

本项目选用第三个角色，用本地 Blender 修复贴图路径、压缩贴图尺寸、简化部分围巾网格、添加基础骨骼并导出 `peak-character.glb`。动作由 `source/main.js` 实时驱动。模型与贴图一同包含在 GLB 中，无远程资源依赖。

这是学生提供素材的来源记录，不表示这些模型由 Codex 原创，也不表示本项目与 PEAK 官方有关联。详细处理信息见 `provenance.json`。家长现在也使用同一份附件角色，独立克隆骨骼，体型放大 12%，服装轻微偏紫，带睡姿和巡查动作；没有新增或声称来自附件的原生动画。室内家具仍由程序化几何构成。


## v0.7.0 衣柜新增

- `peak-character-1.glb`：附件 Character 1，螃蟹造型。修复贴图，螃蟹帽减面，补同名基础骨骼。
- `peak-character-2.glb`：附件 Character 2，厨师造型。修复贴图，厨师帽减面，补同名基础骨骼。
- 纸箱、抱枕耳罩、星星睡帽由 `source/skin-model.js` 用程序化几何构成，部分衣服使用独立纯色材质。基础人体、脸部和骨架来源仍是第三个附件角色。

新增角色处理记录见 `character-1-provenance.json`、`character-2-provenance.json`，可重现脚本见 `source/tools/export-wardrobe.py`。六张卡片缩略图与首页衣柜图由实际游戏预览截取。此记录不赋予或声称其他游戏的模型许可；没有下载其他游戏角色包。


## 家猫（v0.9.0）

家猫由本项目 `source/cat-model.js` 的程序几何直接生成，未使用 PEAK 附件或下载其他游戏的猫模型。橘色身体、曲面条纹、圆头、耳朵、胡须、眼睛、四肢和分节尾巴均在代码中制作与驱动。没有新增 GLB 或贴图文件；场景和接花瓶特写共用这套模型。行为在 `source/cat.js` 中维护，声音在 `source/audio.js` 中合成。


## v0.10.0：网络参考与现成猫模型

- `quaternius-cat.glb`：Quaternius **Cat / Cat Blob**，页面 https://poly.pizza/m/2f54vbV0In 标示 Public Domain (CC0)。实际下载 https://static.poly.pizza/7ccb71fe-dabb-4a6f-a98a-8992bb5e6bc7.glb 。许可 https://creativecommons.org/publicdomain/zero/1.0/ 。文件保留原骨骼和九条动画；运行时调整至约 0.68 高并补自制小脚与尾巴。场景与花瓶特写共用它。选择过程中比较过另一只立方体猫，最终未采用。当前运行版本替代 v0.9 的程序橘猫；旧程序仅保留为初始化兼容。
- 奶龙全身参考：官方角色介绍 https://www.nailoong.com/ipStar/Nailong/ ，以及 https://www.duitang.com/blog/?id=1353954971 的全身图片。圆黄身体、大眼睛、浅色肚皮、短四肢作为自制低多边形改编依据。
- 奶蛙全身参考：https://www.naiwa.world/ 的 `assets/naiwa-standing.png`，图中为黄色网络变体，非官方角色授权。学生明确确认奶白色圆滚滚版本，因此模型改为奶白配色，圆眼、圆肚和短肢。
- 奶鼠：搜索了奶鼠 / 奶蛙 / 全身，但未获得可验证为唯一角色原设的公开全身图片；用户确认奶白色圆滚形态后，制作大耳朵、圆肚和细尾巴的游戏改编。不把普通鼠图片冒称为奶鼠官方原设。三个模型均由 `source/milk-skins.js` 自制，不是从商业游戏提取或下载的官方人物模型。
- 网页参考图不作为贴图或图片重新分发；九张衣柜缩略图均为游戏自身三维预览截图。CC0 只适用于注明的 Quaternius 猫资产，不覆盖其他角色 IP。


## v0.14.7：两套用户多视图重建

`golden-frog.glb` 和 `golden-bull.glb` 由本轮用户提供的两张多视图参考，在 Blender 5.2.1 中本地建模并绑定；没有沿用 PEAK 人体或下载其他人物包。分别有 30／38 根骨骼、四条自制演示动画；GLB 的 7／8 个材质组内嵌纹理，游戏独立驱动两个角色的骨骼。

源文件为 `character-studio/turnaround-characters.blend`，自制纹理为 `character-studio/fleece-normal.png`。图片的作者与角色 IP 许可未核实；“用户提供参考”“本地制作几何／绑定”不等于官方授权或全资产 CC0。参考图哈希、制作范围、实际三角面数、查看方法与验证见 [角色建模说明](../../docs/character-studio/README.md) 和 [模型检查](../../docs/character-studio/model-audit.json)。历史九套仍可使用，当前衣柜共十一套。
