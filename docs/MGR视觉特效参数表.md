# MGR 视觉特效参数表

本表只面向表现调整。音符与演奏牌的集中参数位于：

`Scripts/Mechanics/MgrVisualTuning.cs`

只改该文件中的数值不会改变音符、和弦或演奏的战斗规则。完整关系图见 `docs/MGR架构图.md`。

## 1. 音符槽与音符

实现文件：

- `Scripts/Mechanics/MgrNoteVisuals.cs`：槽位布局、虚线空槽、音符入场、数字与颜色。
- `Scripts/Mechanics/MgrFloatingNoteVisual.cs`：每颗音符持续的漂浮、呼吸和随机差异。
- `Scripts/Mechanics/MgrNoteSystem.cs`：提供“本回合已生成音符数”和“本回合已触发和弦数”。

### 整体布局

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.RackOffset` | `(0, -430)` | 整排音符相对战斗人物节点的位置；X 向右，Y 向下 |
| `Notes.RackZIndex` | `50` | 音符排层级 |
| `Notes.ArtworkScale` | `(0.68, 0.68)` | 音符图片缩放 |
| `Notes.DesiredSlotSpacing` | `96` | 槽位理想中心间距 |
| `Notes.MaximumRackWidth` | `480` | 音符排最大宽度；槽位增多后自动压缩间距 |

实际间距公式：

`min(DesiredSlotSpacing, MaximumRackWidth / (槽位数 - 1))`

### 空槽

当前空槽只有虚线圆框，不再给已填充音符绘制外环。

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.SlotRadius` | `42` | 虚线圆半径 |
| `Notes.EmptySlotDashCount` | `12` | 虚线段数 |
| `Notes.EmptySlotDashFill` | `0.48` | 每一段占其扇区的比例 |
| `Notes.EmptySlotDashWidth` | `3` | 线宽 |

空槽颜色仍在 `MgrNoteVisuals.CreateDashedEmptySlot` 中：`(0.72, 0.76, 0.84, 0.58)`。

### 音符入场与连续生成加速

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.FirstNoteEntranceSeconds` | `0.28s` | 本回合第一颗音符的总入场时间 |
| `Notes.MinimumNoteEntranceSeconds` | `0.10s` | 连续生成时的时间下限 |
| `Notes.NoteEntranceAccelerationPerNote` | `0.018s` | 本回合每已有一颗音符，后续入场减少的时间 |
| `Notes.EntranceStartScale` | `0.28` | 起始缩放 |
| `Notes.EntranceOvershootScale` | `1.18` | 放大阶段的过冲缩放 |
| `Notes.EntranceGrowFraction` | `0.62` | 总时间中用于淡入、上移和放大的比例 |
| `Notes.EntranceStartYOffset` | `18` | 从目标槽位下方多少像素开始 |

总时间公式：

`max(0.10, 0.28 - 本回合此前生成音符数 × 0.018)`

每个音符排共用一个动画门，因此同一个效果生成多颗音符时会按调用顺序逐个出现，不会同帧一起刷出。

### 和弦完成后的停留与连续触发加速

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.FirstChordHoldSeconds` | `0.42s` | 本回合第一次和弦完成后的满槽停留 |
| `Notes.MinimumChordHoldSeconds` | `0.12s` | 连续触发时的停留下限 |
| `Notes.ChordHoldAccelerationPerChord` | `0.05s` | 本回合每已有一次触发，后续停留减少的时间 |
| `Notes.FastChordCommandThreshold` | `2` | 已触发两次后，伤害/格挡等原生命令使用快速表现路径 |

停留公式：

`max(0.12, 0.42 - 本回合此前触发和弦数 × 0.05)`

### 漂浮、呼吸与随机差异

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.BobAmplitude` | `5px` | 上下浮动幅度 |
| `Notes.BobAngularSpeed` | `1.75` | 基础浮动速度 |
| `Notes.BobSpeedVariance` | `0.22` | 每颗音符的浮动速度随机范围约 ±22% |
| `Notes.BreathAmplitude` | `0.055` | 呼吸缩放约 ±5.5% |
| `Notes.BreathAngularSpeed` | `2.05` | 基础呼吸速度 |
| `Notes.BreathSpeedVariance` | `0.20` | 每颗音符的呼吸速度随机范围约 ±20% |
| `Notes.InitialScaleVariance` | `0.07` | 每颗音符的基础缩放随机范围约 ±7% |
| `Notes.PhaseStep` | `0.72` | 相邻槽位的固定相位差 |
| `Notes.PhaseVariance` | `0.65` | 每颗音符额外随机相位范围 |

这些随机数只控制视觉，不参与战斗 RNG 和机制结算。

### 音符数值标签与颜色

这些值目前写在 `MgrNoteVisuals.cs`，尚未集中到 `MgrVisualTuning.cs`：

- 标签位置 `(-36, 21)`，尺寸 `(72, 36)`。
- 字号 `24`，彩色描边 `8`，黑色阴影描边 `2`。
- 攻击 `#ff3b30`；技能 `#22d967`；能力 `#1f9eff`；状态 `#60666d`。
- 诅咒 `#e8bd00`；任务 `#4fc9d1`；星空 `#f020c8`；幽灵 `#a875ff`。

## 2. 演奏牌队列

实现文件：

- `Scripts/Mechanics/MgrPerformanceVisuals.cs`：整排布局、入队、触发、悬停预览和离队。
- `Scripts/Mechanics/MgrPerformanceSystem.cs`：触发动画的时机与最终牌堆路由。

### 整排布局与重叠

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.RackOffset` | `(0, -650)` | 演奏排相对战斗人物节点的位置 |
| `Performances.RackZIndex` | `55` | 基础层级 |
| `Performances.MiniatureScale` | `(0.25, 0.25)` | 队列中卡牌大小 |
| `Performances.HoveredMiniatureScale` | `(0.29, 0.29)` | 鼠标移入时的缩略牌大小 |
| `Performances.DesiredSpacing` | `52` | 相邻演奏牌露出的理想宽度 |
| `Performances.MaximumWidth` | `520` | 整排最大宽度；牌多后自动加重重叠 |

实际间距公式：

`min(DesiredSpacing, MaximumWidth / (演奏牌数 - 1))`

队列中最早进入的牌位于最右侧，层级也最高；新牌从左侧继续加入。

### 入队动画

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.EnterQueueSeconds` | `0.28s` | 原打出卡牌飞入演奏槽位的时长 |

流程中的硬编码细节位于 `MgrPerformanceVisuals.cs`：

- 最多搜索原始 `NCard` 节点 `30` 帧。
- 找到后终止其原生 `PlayPileTween`，同时移动、缩小并淡出。
- 淡到 `12%` 不透明度后释放原节点；演奏排中的缩略牌是另一张持续存在的视图。

### 回合开始触发

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.TriggerScale` | `1.2` | 原地跳动的峰值缩放 |
| `Performances.TriggerGrowSeconds` | `0.14s` | 放大与亮起阶段 |
| `Performances.TriggerSettleSeconds` | `0.18s` | 回落与熄灭阶段 |

触发 Glow 仍写在 `MgrPerformanceVisuals.cs`：

- 颜色 `#d73ee7`，峰值透明度 `0.9`。
- 比缩略牌四周各多 `11px`。
- 只在原位置跳动，不把牌移到屏幕中央。

### 剩余次数

剩余演奏次数现在按缩略牌右下角计算，并已集中到
`MgrVisualTuning.Performances`：

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `RemainingLabelSize` | `(56, 48)` | 次数标签的控件尺寸 |
| `RemainingLabelBottomRightInset` | `(18, 18)` | 标签中心相对右下角向左、向上内缩的距离；越小越靠外 |
| `RemainingLabelFontSize` | `32` | 字号 |
| `RemainingLabelOutlineSize` | `8` | 描边大小 |
| `RemainingLabelColor` | 白色 | 数字颜色 |
| `RemainingLabelOutlineColor` | `#a915b8` | 描边颜色 |
| `RemainingLabelZIndex` | `25` | 标签相对缩略牌的层级 |

每次演奏后仍由 `Refresh` 读取 `RemainingPerformanceTurns`。标签位置公式为：

`右下角 - BottomRightInset - LabelSize / 2`

### 鼠标悬停预览

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.PreviewScale` | `(0.68, 0.68)` | 完整详情牌大小 |
| `Performances.PreviewGrowSeconds` | `0.12s` | 从 `0.5` 缩放弹到目标大小的时间 |
| `Performances.PreviewMouseXOffset` | `34px` | 详情牌出现在鼠标右侧的距离 |

其他细节：

- 预览与 HoverHitbox 位于私有 `CanvasLayer 90`，避免被战斗 UI 截走输入。
- 详情牌始终限制在屏幕边缘内 `8px`。
- 缩略牌和 HoverHitbox 悬停时提升到 `ZIndex 300`。

### 演奏结束与离队

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.ExitSeconds` | `0.38s` | 飞向真实弃牌/消耗/抽牌堆的时长 |

仍写在 `MgrPerformanceVisuals.cs`：

- 有真实牌堆目标时缩放到 `0.34`；找不到目标时缩放到 `0.82`，并向上 `100px`。
- `0.12s` 后开始淡出，淡出用时 `0.26s`。
- 牌堆数量由最终自动打出的塔二原生结果路由更新；这个动画只提供视觉反馈。

## 3. 音频反馈

实现文件：`Scripts/Characters/MgrAudio.cs`。

| 事件 | 资源 | 默认音量 |
| --- | --- | ---: |
| 生成音符 | `audio/NoteChannel.ogg` | `0.2` |
| 触发和弦 | `audio/Chord.ogg` | `0.2` |
| 角色选择 | `audio/MGR_charselect.ogg` | `1.0` |

## 4. 角色静态 UI

这些位置优先在 Godot 场景编辑器中调整，不属于音符/演奏的集中动画参数：

- 战斗人物：`SlayTheSpire2MGRMod/scenes/characters/Mgr_character.tscn`。
- 能量框：`SlayTheSpire2MGRMod/scenes/characters/Mgr_energy_counter.tscn`。
- 选人背景：`SlayTheSpire2MGRMod/scenes/characters/Mgr_character_select_bg.tscn`。
- 选人/地图/能量等资源映射：`Scripts/Characters/MgrCharacterAssets.cs`。

## 调参顺序

1. 先改两套 `RackOffset`，确认整体位置。
2. 再改缩放、槽位/卡牌间距和最大宽度。
3. 然后改入场、触发与离队时长。
4. 最后调整漂浮、呼吸、随机差异、颜色和文字。

这样发现异常时，能较容易区分是“布局”“动画”还是“战斗状态”造成的。
