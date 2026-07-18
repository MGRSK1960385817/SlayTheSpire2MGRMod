# MGR 视觉特效参数表

本表面向手动调整。战斗 UI 的集中参数优先修改：

`Scripts/Mechanics/MgrVisualTuning.cs`

修改数值后重新编译即可；不需要改卡牌逻辑。

## 1. 音符槽与音符

实现文件：

- `Scripts/Mechanics/MgrNoteVisuals.cs`：槽位、入场 Tween、数字、颜色与 Glow。
- `Scripts/Mechanics/MgrFloatingNoteVisual.cs`：持续的上下漂浮和呼吸。
- `Scripts/Mechanics/MgrVisualTuning.cs` 的 `Notes`：集中参数。

当前参数：

| 参数 | 默认值 | 作用 |
| --- | ---: | --- |
| `RackOffset` | `(0, -430)` | 整排音符相对角色节点的位置 |
| `RackZIndex` | `50` | 整排音符层级 |
| `ArtworkScale` | `(0.68, 0.68)` | 音符图片大小 |
| `DesiredSlotSpacing` | `96` | 槽位理想间距 |
| `MaximumRackWidth` | `480` | 槽位总宽度上限；槽多时自动压缩间距 |
| `SlotRadius` | `42` | 空槽半径 |
| `ChordHoldSeconds` | `0.45 秒` | 和弦完成后，满槽画面保留多久 |
| `EntranceStartScale` | `0.28` | 新音符出现时的初始大小 |
| `EntranceOvershootScale` | `1.18` | 弹出时最大大小 |
| `EntranceGrowSeconds` | `0.13 秒` | 放大阶段时长 |
| `EntranceSettleSeconds` | `0.09 秒` | 回落到正常大小的时长 |
| `EntranceStartYOffset` | `18` | 新音符从槽位下方多少像素开始 |
| `EntranceFlashScale` | `1.38` | 入场色环最大大小 |
| `EntranceFlashAlpha` | `0.52` | 入场色环初始透明度 |
| `BobAmplitude` | `5` | 上下漂浮幅度，像素 |
| `BobAngularSpeed` | `1.75` | 上下漂浮速度 |
| `BreathAmplitude` | `0.055` | 呼吸缩放幅度，即约 ±5.5% |
| `BreathAngularSpeed` | `2.05` | 呼吸速度 |
| `PhaseStep` | `0.72` | 相邻音符的相位差；越大越不像同步运动 |

入场先后顺序由 `MgrNoteSystem.ChannelSingleNote` 等待每个入场 Tween 实现。当前每颗音符完整入场约 `0.22 秒`，因此一次生成四颗时会按槽位从左到右依次出现。

音符效果数字仍位于 `MgrNoteVisuals.cs`：

- 位置：`(-36, 21)`，尺寸 `(72, 36)`。
- 字号：`24`。
- 描边：`8`。
- 阴影描边：`2`。

空槽图形仍位于同一文件：

- 空槽符号位置：`(-24, -28)`，尺寸 `(48, 56)`。
- 空槽符号字号：`30`，描边 `5`。
- 外圈宽度：`5`；内圈宽度：`2`。

## 2. 演奏牌队列

实现文件：

- `Scripts/Mechanics/MgrPerformanceVisuals.cs`
- `Scripts/Mechanics/MgrVisualTuning.cs` 的 `Performances`

当前参数：

| 参数 | 默认值 | 作用 |
| --- | ---: | --- |
| `RackOffset` | `(0, -650)` | 演奏牌堆相对角色的位置 |
| `RackZIndex` | `55` | 演奏牌堆基础层级 |
| `MiniatureScale` | `0.25` | 队列中卡牌大小 |
| `HoveredMiniatureScale` | `0.29` | 鼠标移上去时的小幅放大 |
| `PreviewScale` | `0.68` | 鼠标右侧详情卡大小 |
| `DesiredSpacing` | `52` | 相邻演奏牌露出的宽度 |
| `MaximumWidth` | `520` | 整排最大宽度 |
| `EnterQueueSeconds` | `0.28 秒` | 普通打出后飞入演奏堆的时长 |
| `TriggerScale` | `1.2` | 回合开始触发时跳动的最大大小 |
| `TriggerGrowSeconds` | `0.14 秒` | 触发放大时长 |
| `TriggerSettleSeconds` | `0.18 秒` | 触发回落时长 |
| `ExitSeconds` | `0.38 秒` | 演奏结束飞向弃牌/消耗堆的时长 |
| `PreviewGrowSeconds` | `0.12 秒` | 右侧详情卡弹出时长 |
| `PreviewMouseXOffset` | `34` | 详情卡与鼠标的横向距离 |

其他仍在 `MgrPerformanceVisuals.cs` 内的数字：

- 进入演奏堆后，原打出卡牌淡到 `12%` 再删除视觉节点。
- 触发 Glow 边距：四周 `11` 像素。
- 剩余演奏次数：字号 `32`，描边 `8`。
- 触发 Glow 最高透明度：`0.9`。
- 离开队列时，有牌堆目标缩放到 `0.34`；无牌堆目标缩放到 `0.82`。
- 详情卡被限制在屏幕边缘 `8` 像素内。

## 3. 野兽化弃牌前展示

实现文件：

- `Scripts/Powers/YazyuutokasuPower.cs`
- `Scripts/Mechanics/MgrVisualTuning.cs` 的 `DiscardReveal`

参数：

| 参数 | 默认值 | 作用 |
| --- | ---: | --- |
| `RaiseDistance` | `72` | 被丢弃牌向上抬起的距离 |
| `ScaleMultiplier` | `1.08` | 抬起时放大倍数 |
| `RaiseSeconds` | `0.14 秒` | 抬起动画时长 |
| `HoldSeconds` | `0.22 秒` | 抬起后给玩家辨认的停留时间 |

高亮颜色目前写在 `YazyuutokasuPower.cs`：`(1.2, 1.12, 1.24)`。

## 4. 能量框

场景：

`SlayTheSpire2MGRMod/scenes/characters/Mgr_energy_counter.tscn`

主要手调位置：

- 根控件尺寸：`128 × 128`，中心点 `(64, 64)`。
- 能量数字边距：左 `16`、上 `-29`、右 `-16`、下 `29`。
- 数字字号：`36`。
- 数字描边：`16`；阴影描边 `16`；阴影偏移 `(3, 2)`。
- 内部两组粒子目前主要作为兼容占位，`amount = 1`；其中一个寿命 `0.5 秒`。

贴图和能量颜色来源：

- `Scripts/Characters/MgrCardPool.cs`
- `Scripts/Characters/MgrCharacter.cs`
- `SlayTheSpire2MGRMod/images/placeholders/winefox/energy_card_icon.png`

## 5. 角色与选人画面

场景和资源：

- 战斗人物：`SlayTheSpire2MGRMod/scenes/characters/Mgr_character.tscn`
  - 图片边界：左 `-120`、上 `-322`、右 `120`、下 `8`。
- 选人背景：`SlayTheSpire2MGRMod/scenes/characters/Mgr_character_select_bg.tscn`
  - 设计区域：`1920 × 1200`，从 `(-960, -600)` 到 `(960, 600)`。
  - 使用图片：`images/characters/Mgr_character_select_background.jpg`。
- 选人图标注册：`Scripts/Characters/MgrCharacterAssets.cs`
  - 使用图片：`images/characters/Mgr_character_select.png`。
- 选人音效：`Scripts/Characters/MgrAudio.cs` 与
  `Scripts/Patches/MgrCharacterSelectSfxPatch.cs`
  - 音频文件：`SlayTheSpire2MGRMod/audio/MGR_charselect.ogg`。
- 营火人物：`Mgr_rest_site.tscn`，主体大致范围 `260 × 340`。
- 商店人物：`Mgr_merchant.tscn`。

这些角色场景的布局数字仍应直接在 Godot 编辑器里调，编辑 `.tscn` 也可以，但不建议把它们搬进战斗 UI 的集中参数类。

## 6. 声音反馈

实现文件：`Scripts/Characters/MgrAudio.cs`

- 生成音符：`audio/NoteChannel.ogg`，默认音量 `0.2`。
- 触发和弦：`audio/Chord.ogg`，默认音量 `0.2`。
- 角色选择：`audio/MGR_charselect.ogg`。

声音属于反馈特效，但没有大小或位置参数。

## 调整建议

一次只改一组：

1. 先调 `RackOffset` 和尺寸。
2. 再调间距与最大宽度。
3. 最后调动画时间、漂浮幅度和呼吸幅度。

这样游戏内出现偏移时，容易判断是“布局”还是“动画”造成的。
