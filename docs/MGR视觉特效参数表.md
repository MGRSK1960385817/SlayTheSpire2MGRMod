# MGR 视觉特效参数表

本表只面向表现调整。音符与演奏牌的集中参数位于：

`Scripts/Mechanics/MgrVisualTuning.cs`

只改该文件中的数值不会改变音符、和弦或演奏的战斗规则。完整关系图见 `docs/MGR架构图.md`。

## 1. 音符槽与音符

实现文件：

- `Scripts/Mechanics/MgrNoteVisuals.cs`：槽位布局、虚线空槽、音符入场、数字与颜色。
- `Scripts/Mechanics/MgrRotatingNoteSlotFrame.cs`：每个空槽独立的虚线旋转、定色游光、轨道光点与呼吸。
- `Scripts/Mechanics/MgrFloatingNoteVisual.cs`：每颗音符持续的漂浮、呼吸和随机差异。
- `Scripts/Mechanics/MgrNoteBurstVisual.cs`：音符生成、和弦触发及空槽切换时的代码绘制光晕与星芒。
- `Scripts/Mechanics/MgrNoteSystem.cs`：提供“本回合已生成音符数”和“本回合已触发和弦数”。

### 整体布局

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.RackOffset` | `(0, -350)` | 整排音符相对战斗人物节点的位置；X 向右，Y 向下 |
| `Notes.RackZIndex` | `50` | 音符排层级 |
| `Notes.ArtworkFillRatio` | `0.92` | 音符图片相对于音符槽直径的显示比例；运行时先按源图尺寸归一化，再按比例显示，替换高清图无需重算倍率 |
| `Notes.CurseAccentColor` | `#78101C` | 诅咒音符数值描边、生成星屑及和弦爆发共用的黑红色 |
| `Notes.DesiredSlotSpacing` | `96` | 槽位理想中心间距 |
| `Notes.MaximumRackWidth` | `480` | 音符排最大宽度；槽位增多后自动压缩间距 |

实际间距公式：

`min(DesiredSlotSpacing, MaximumRackWidth / (槽位数 - 1))`

### 空槽

空槽由八段淡色虚线构成，整框持续旋转；一段高亮游光沿八段虚线移动，另有一个小型发光星星沿槽边缘环绕。星星与游光共用同一个初始相位、速度和方向，并固定处于游光前端，形成“星星牵引游光”的效果。所有发光均使用与演奏次数横杠一致的固定颜色 X（`Performances.PerformanceAccentColor`），不会做彩虹变色。每个槽仍会独立抽取较大范围的框体旋转速度与游光速度，因此相邻槽不会整齐同步；已填充音符仍不绘制外环。

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.SlotRadius` | `30` | 虚线圆半径 |
| `Notes.EmptySlotDashCount` | `8` | 虚线段数 |
| `Notes.EmptySlotDashFill` | `0.48` | 每一段占其扇区的比例 |
| `Notes.EmptySlotDashWidth` | `2.5` | 基础线宽 |
| `Notes.EmptySlotBaseAlpha` | `0.36` | 淡色虚线框透明度 |
| `Notes.EmptySlotHighlightAlpha` | `0.96` | 当前高亮虚线段的透明度 |
| `Notes.EmptySlotHighlightWidthBoost` | `1.9` | 高亮段相对基础线宽的增量 |
| `Notes.EmptySlotRotationDegreesPerSecond` | `18` | 整个八段虚线框的基础旋转速度（度/秒） |
| `Notes.EmptySlotRotationMultiplierMin / Max` | `0.35 / 1.90` | 每个槽的旋转速度倍率范围；部分槽会反向 |
| `Notes.EmptySlotHighlightAngularSpeedMin / Max` | `0.85 / 3.65` | 沿虚线框移动的高亮速度范围 |
| `Notes.EmptySlotGlowOrbitRadius` | `31px` | 小型发光物体的轨道半径 |
| `Notes.EmptySlotGlowLeadDegrees` | `28°` | 星星沿运动方向领先游光中心的角度；方向反转时领先方向也随之反转。增大即让星星更靠前 |
| `Notes.EmptySlotGlowCoreRadius` | `2.8px` | 发光物体亮核半径 |
| `Notes.EmptySlotGlowHaloRadius` | `9.5px` | 发光物体柔光半径 |
| `Notes.EmptySlotGlowStarLength` | `6.5px` | 发光物体十字星芒长度 |
| `Notes.EmptySlotBreathAmplitude` | `0.035` | 空槽整体呼吸缩放幅度 |
| `Notes.EmptySlotBreathSpeed` | `1.25` | 空槽整体呼吸速度 |

空槽消失时会旋转收缩至中心；音符离开后，空槽会旋转、过冲放大后落回正常尺寸。`EmptySlotCollapseSeconds`、`EmptySlotAppearSeconds`、`EmptySlotTransitionRotation` 与 `EmptySlotAppearOvershootScale` 分别控制这两段过渡。

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

生成时还会播放少量、与该音符颜色一致的星芒与柔光。`EntranceBurstParticleCount`、`EntranceBurstSeconds` 和 `EntranceBurstEndRadius` 分别控制数量、时长与扩散范围。

### 和弦完成后的停留与连续触发加速

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Notes.FirstChordHoldSeconds` | `0.42s` | 本回合第一次和弦完成后的满槽停留 |
| `Notes.MinimumChordHoldSeconds` | `0.12s` | 连续触发时的停留下限 |
| `Notes.ChordHoldAccelerationPerChord` | `0.075s` | 本回合每已有一次触发，后续停留减少的时间 |
| `Notes.FastChordCommandThreshold` | `2` | 已触发两次后，伤害/格挡等原生命令使用快速表现路径 |

停留公式：

`max(0.12, 0.42 - 本回合此前触发和弦数 × 0.075)`

和弦开始结算时，每个已填充音符都会略微放大，并喷射较生成动画更多的同色星芒和柔光；`ChordBurstParticleCount`、`ChordBurstSeconds`、`ChordBurstEndRadius` 与 `ChordTriggerScale` 控制该效果。动画与战斗结算重叠播放，不会额外串行拖慢和弦。

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

万象音符把每张源图片仅作为透明度轮廓使用，不继承原图的 RGB 底色；其柔和的薰衣草、青色、金色与玫红色谱会持续流动。因此即使轮换到黑色的诅咒音符轮廓，也能保持完整彩色表现。

### 音符数值标签与颜色

这些值已经集中在 `MgrVisualTuning.Notes`：

- 标签位置 `(-36, 21)`，尺寸 `(72, 36)`。
- 字号 `24`，彩色描边 `8`，黑色阴影描边 `2`。
- 攻击 `#ff3b30`；技能 `#22d967`；能力 `#1f9eff`；状态 `#60666d`。
- 诅咒 `#78101c`；星空 `#f020c8`；幽灵 `#a875ff`。

## 2. 演奏牌队列

实现文件：

- `Scripts/Mechanics/MgrPerformanceVisuals.cs`：整排布局、入队、触发、悬停预览和离队。
- `Scripts/Mechanics/MgrPerformanceSystem.cs`：触发动画的时机与最终牌堆路由。
- `Scripts/Mechanics/MgrPerformanceStaffVisual.cs`：五线谱、游动音符、扫线和整排发光。
- `Scripts/Mechanics/MgrPerformanceIdleEdgeVisual.cs`：待机卡牌边缘流光。
- `Scripts/Mechanics/MgrPerformanceCounterVisual.cs`：卡牌上方的剩余次数节拍标记。

线谱使用 `MgrMusicGlyphRenderer.cs` 的八种代码绘制字形：四分音符、八分音符、十六分音符、二分音符、连梁双音、宽连梁三音、宽连梁四音，以及纵向跨越两根谱线的双行和声音符。已移除“大椭圆套小椭圆”的全音符。宽符号会自动为线谱留出更大的断口；双行和声音符会在相同 X 坐标同时切开相邻两根谱线。

线谱音符以特殊字形为高权重。待机时最多显示 `7` 个，整段演奏触发期间允许最多显示 `15` 个；两种状态共享 `0.78～1.22s` 的模拟生成间隔、`0.18s` 的失败重试时间、`0.48s` 的同排冷却和 `0.42s` 的相邻排规避窗口。

演奏开始后，整套谱线音符模拟由 `StaffPerformingFlowSpeedMultiplier = 1.75` 统一快进：游动速度、上下浮动速度、生成倒计时及生成避让冷却都同步变为 `1.75` 倍。空音符槽也复用这个全局倍率，槽框旋转、发光条沿框移动及牵引星运动会同步加速，但空槽本身的呼吸速度保持不变。它不会按战斗中生成了多少音符来额外硬塞固定数量的谱面符号。最后一张演奏牌结束后恢复 `1.0` 倍，并重新开始正常待机计时。

### 整排布局与重叠

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.RackOffset` | `(80, -470)` | 演奏框、线谱和演奏牌堆整体相对战斗人物节点的位置；X 增大即整体右移 |
| `Performances.StaffOffset` | `(0, -16)` | 只移动线谱、不移动演奏牌；X 控制左右，Y 控制上下 |
| `Performances.StaffWidth` | `560` | 线谱总宽度；只改变线谱，不改变演奏牌间距 |
| `Performances.StaffLineAlpha` | `0.34` | 线谱待机时的不透明度；数值越大越实，越小越透明 |
| `Performances.RackZIndex` | `55` | 基础层级 |
| `Performances.MiniatureScale` | `(0.35, 0.35)` | 队列中卡牌大小 |
| `Performances.HoveredMiniatureScale` | `(0.5, 0.5)` | 鼠标移入时的缩略牌大小 |
| `Performances.FilledRackCardThreshold` | `5` | 达到该数量后，由“未满”切换为“已满”压缩布局 |
| `Performances.UnfilledCardSpacing` | `82` | 未满时相邻演奏牌的固定间距；新牌从右向左加入 |
| `Performances.FilledRackBaseWidth` | `272` | 刚进入已满状态时的整排占用宽度 |
| `Performances.FilledRackWidthPerExtraCard` | `20` | 已满后每多一张牌，整排只额外扩宽的距离 |
| `Performances.FilledRackMaximumWidth` | `370` | 已满布局最终允许占用的最大宽度 |
| `Performances.RackCardOpacity` | `0.92` | 演奏队列中卡面本体的不透明度 |

未满时，最右侧位置保持固定，队列中最早进入的牌位于最右侧且层级最高，新牌以 `82px` 间距从其左侧依次加入。达到 `5` 张后，整排改为左右居中的已满布局：总宽度从 `272px` 开始，每多一张只增加 `20px`，最高 `370px`；实际牌距为“当前总宽度 / (牌数 - 1)”，因此牌越多，重叠仍会逐渐加深。

演奏牌架与音符槽会同时监听原版 `NOverlayStack`、`NCapstoneContainer` 和 `NMapScreen`：地图、卡组详情、牌堆详情或其他顶层覆盖界面打开时，线谱、演奏牌、音符槽、计数和悬停说明会整组隐藏；全部关闭后自动恢复。因此不要通过降低 ZIndex 来解决界面穿透，否则会同时破坏战斗内卡牌与特效的前后关系。

### 入队动画

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.EnterQueueSeconds` | `0.20s` | 本回合第一张演奏牌飞入槽位的时长 |
| `Performances.EntryAnimationAccelerationPerCard` | `0.25` | 本回合每多打出一张演奏牌，后续入队动画减少的时长倍率 |
| `Performances.MinimumEntryAnimationDurationScale` | `0.50` | 连续打出演奏牌时入队动画的最低时长倍率 |

实际入队时长为：第一张 `0.20s`，第二张 `0.15s`，第三张及以后统一 `0.10s`。

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
| `Performances.SequentialTriggerAccelerationPerCard` | `0.10` | 同一轮中每完成一张演奏牌，后续动画时长减少的比例 |
| `Performances.MinimumSequentialTriggerDurationScale` | `0.60` | 连续演奏动画的最低时长倍率 |

同一轮演奏从第一张的 `1.00×` 时长开始，之后依次为 `0.90×`、`0.80×`、`0.70×`，第五张及以后固定为 `0.60×`。这个倍率同时作用于扫线靠近、卡牌跳动、次数变化、扫线离开以及完成演奏后的离队动画；卡牌本身的战斗效果仍完整结算，不做跳帧。

触发 Glow 仍写在 `MgrPerformanceVisuals.cs`：

- 颜色 `#fff0b8`，峰值透明度 `0.78`。
- 比缩略牌四周各多 `11px`。
- 只在原位置跳动，不把牌移到屏幕中央。

触发时还会由 `MgrPerformanceCardBurstVisual.cs` 绘制黄白、粉、青、绿等星芒。`CardBurstSeconds`、`CardBurstParticleCount`、`CardBurstStartRadius` 与 `CardBurstEndRadius` 分别控制持续时间、数量和扩散范围。

### 待机边缘装饰

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `IdleEdgeMargin` | `5px` | 装饰线相对卡面边缘向外扩张的距离 |
| `IdleEdgeBaseWidth` | `1.65px` | 四角装饰线的屏幕线宽 |
| `IdleEdgeGlowWidth` | `4.8px` | 装饰线后方的柔光宽度 |
| `IdleEdgeBaseAlpha` | `0.34` | 装饰线透明度 |
| `IdleEdgeGlowAlpha` | `0.10` | 柔光透明度 |

只保留四个角的 L 形框，不再绘制四条边中点装饰，也没有物体绕卡牌运动。角框与次数横杠共用固定颜色 X，不做变色；演奏开始时会淡到约 `18%`，扫线离开后恢复。

### 剩余次数

剩余演奏次数显示为卡牌上方的“节拍标记”：数字两侧横线的数量跟随当前显示次数，最多三条，不再绘制数字下方的小点。它与缩略牌共用同一锚点，但不附着到会被塔二复用的 `NCard` 节点。

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `RemainingCounterSize` | `(54, 34)` | 数字控件尺寸 |
| `RemainingCounterTopGap` | `9px` | 标记与卡牌顶边的距离 |
| `RemainingCounterFontSize` | `26` | 数字字号 |
| `RemainingCounterOutlineSize` | `5` | 深色文字描边大小 |
| `RemainingCounterWingLength` | `24px` | 两侧第一条横线长度；其后两条依次缩短 |
| `RemainingCounterSingleWingLengthScale` | `0.76` | 只剩一次时，居中单横线相对标准长度的倍率 |
| `RemainingCounterDoubleWingLengthScale` | `0.88` | 剩余两次时，两条横线相对标准长度的倍率 |
| `RemainingCounterWingGap` | `14px` | 数字与横线的间隙 |
| `RemainingCounterWingSpacing` | `5px` | 相邻横线的纵向间距 |
| `RemainingCounterWingLineCount` | `3` | 数字每侧最多绘制的横线数量 |
| `RemainingCounterPulseSeconds` | `0.30s` | 触发时标记跳动的总时长 |
| `RemainingCounterChangeFraction` | `0.36` | 跳动进行到 36% 时更新数字 |

显示为 `0` 时两侧均没有横线；显示为 `1` 时只有一条经过专门缩短、垂直居中的横线；显示为 `2` 时绘制上下对称的两条；显示为 `3` 或更多时统一绘制三条。多条横线仍从上到下依次缩短。数字及其横杠始终固定在同一个位置，不再上下漂浮。回合开始的普通演奏会在扫线停于卡牌、卡牌发光时先把显示数字减一；不消耗次数的“立即触发”只播放同样的缩放跳动，不修改数字。

### 鼠标悬停预览

| 参数 | 当前值 | 作用 |
| --- | ---: | --- |
| `Performances.PreviewScale` | `(0.8, 0.8)` | 完整详情牌大小 |
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
- 选人和地图资源映射：`Scripts/Characters/MgrCharacterAssets.cs`。
- 卡面与文本能量图标：`Scripts/Characters/MgrCardPool.cs`、`MgrRelicPool.cs`、`MgrPotionPool.cs`，使用 `images/characters/energy_big.png` 与 `energy_text.png`。
- 战斗能量框：`SlayTheSpire2MGRMod/scenes/characters/Mgr_energy_counter.tscn`，使用从塔一移入的 `images/characters/energy/layer0.png` 至 `layer5.png` 与 `energyRefreshVFX.png`。其中 layer1～5 保留塔一各自的旋转方向和速度。

## 调参顺序

1. 先改两套 `RackOffset`，确认整体位置。
2. 再改缩放、槽位/卡牌间距和最大宽度。
3. 然后改入场、触发与离队时长。
4. 最后调整漂浮、呼吸、随机差异、颜色和文字。

这样发现异常时，能较容易区分是“布局”“动画”还是“战斗状态”造成的。
