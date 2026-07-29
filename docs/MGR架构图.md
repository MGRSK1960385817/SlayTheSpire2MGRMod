# MGR 模组架构图

本文用于两个不同视角：第一张图回答“整个模组由什么组成、一次出牌会经过哪里”；第二张图只展开音符与演奏 UI，供后续定位动画与调整参数。

## 图一：整个模组

```mermaid
flowchart TB
    subgraph BOOT["启动、依赖与注册"]
        MANIFEST["SlayTheSpire2MGRMod.json<br/>模组清单与 RitsuLib 依赖"]
        GODOT["project.godot + PCK<br/>场景、图片、音频、字体"]
        DLL["SlayTheSpire2MGRMod.dll<br/>C# 游戏逻辑"]
        RITSU["STS2-RitsuLib<br/>模板、自动注册、Hook、音频后端"]
        ENTRY["Entry.Initialize<br/>注册 Godot 脚本、运行时补丁和程序集"]
        DISCOVERY["Register* 属性扫描<br/>角色、卡牌、遗物、能力、单例"]

        MANIFEST --> ENTRY
        GODOT --> ENTRY
        DLL --> ENTRY
        RITSU --> ENTRY
        ENTRY --> DISCOVERY
    end

    subgraph CONTENT["人物与内容层"]
        CHARACTER["MgrCharacter<br/>生命、金币、主题色、人物场景"]
        ASSETS["MgrCharacterAssets + scenes/characters<br/>选人、地图、战斗人物、能量框"]
        POOLS["MgrCardPool / MgrRelicPool / MgrPotionPool<br/>奖励池、能量颜色、牌框"]
        STARTER["RegisterCharacterStarter*<br/>初始牌组与初始遗物"]
        CARD_BASE["MgrCard<br/>星空、初始演奏次数、起音/尾音、结束钩子"]
        CARDS["Scripts/Cards<br/>MGR 卡、衍生牌、选项牌"]
        RELICS["Scripts/Relics<br/>遗物与战斗钩子"]
        POWERS["Scripts/Powers<br/>强音、双倍音符及流派能力"]

        CHARACTER --> ASSETS
        CHARACTER --> POOLS
        POOLS --> STARTER
        CARD_BASE --> CARDS
        CARDS --> POOLS
        RELICS --> POOLS
    end

    DISCOVERY --> CHARACTER
    DISCOVERY --> CARDS
    DISCOVERY --> RELICS
    DISCOVERY --> POWERS

    subgraph COMBAT["战斗机制层"]
        LISTENER["MgrNoteSystem<br/>全局战斗监听器与统一调度入口"]
        RESOLVER["CardNoteResolver<br/>任意卡牌 → 对应音符种类"]
        NOTE_STATE["MgrCombatState + PhraseState<br/>音符序列、槽位数、回合/战斗计数器"]
        NOTE_MODEL["MgrNote / NoteKind / MgrNoteFactory<br/>8 种音符及基础数值"]
        NOTE_EFFECT["MgrNoteEffects<br/>和弦逐音符结算、强音与能力/遗物修正"]
        PERF_SYSTEM["MgrPerformanceSystem<br/>入队、按顺序自动打出、结束与牌堆路由"]
        PERF_STATE["MgrPerformanceState + Entry<br/>有序队列、初始次数、剩余次数"]
        MUTATION["辅助机制<br/>演奏刻印、战斗内数值修改、诅咒、加权随机、音符还原"]
        GAME_CMDS["塔二原生 CardCmd / CardPileCmd / CreatureCmd<br/>真实出牌、抽牌、伤害、格挡、弃牌、消耗"]

        LISTENER --> RESOLVER
        RESOLVER --> NOTE_MODEL
        LISTENER --> NOTE_STATE
        NOTE_STATE -->|"槽位填满：形成和弦"| NOTE_EFFECT
        NOTE_MODEL --> NOTE_EFFECT
        NOTE_EFFECT --> GAME_CMDS
        LISTENER -->|"已结算的演奏牌"| PERF_SYSTEM
        PERF_SYSTEM <--> PERF_STATE
        PERF_SYSTEM -->|"回合开始 AutoPlay"| GAME_CMDS
        GAME_CMDS -->|"自动打出同样触发 AfterCardPlayed"| LISTENER
        CARDS --> MUTATION
        MUTATION --> NOTE_STATE
        MUTATION --> PERF_SYSTEM
        POWERS --> NOTE_EFFECT
        RELICS --> LISTENER
        RELICS --> NOTE_EFFECT
        RELICS --> PERF_SYSTEM
    end

    CARDS --> GAME_CMDS
    STARTER --> GAME_CMDS

    subgraph PRESENTATION["表现与资源层"]
        NOTE_UI["MgrNoteVisuals + MgrFloatingNoteVisual<br/>空槽、音符、数值、入场、漂浮、呼吸"]
        PERF_UI["MgrPerformanceVisuals<br/>演奏牌堆、入队、触发、悬停预览、离队"]
        TUNING["MgrVisualTuning<br/>音符与演奏 UI 的集中可调参数"]
        AUDIO["MgrAudio<br/>选人、生成音符、触发和弦"]
        LOC["localization/eng + zhs<br/>卡牌、遗物、能力、人物文本"]
        RES["images / scenes / audio / fonts<br/>Godot 打包资源"]
        PATCHES["Scripts/Patches<br/>动态演奏描述、星空首行、选人音效路由"]

        NOTE_STATE --> NOTE_UI
        PERF_STATE --> PERF_UI
        TUNING --> NOTE_UI
        TUNING --> PERF_UI
        LISTENER --> AUDIO
        RES --> NOTE_UI
        RES --> PERF_UI
        RES --> ASSETS
        LOC --> CARDS
        LOC --> RELICS
        LOC --> POWERS
        PATCHES --> CARDS
        PATCHES --> AUDIO
    end

    DISCOVERY --> PATCHES

    REGISTRY["docs/MGR_content_registry.json<br/>供作者阅读、改名和停用内容的开发登记表<br/>游戏运行时不会读取"]
    REGISTRY -.->|"人工同步"| CARDS
    REGISTRY -.->|"人工同步"| RELICS
    REGISTRY -.->|"人工同步"| LOC
```

### 阅读与检查顺序

1. 启动失败或内容没注册：先看 `Entry.cs`、模组清单、`Register*` 属性和 RitsuLib 版本。
2. 人物、卡池或初始配置不对：看 `Scripts/Characters`，再看卡牌/遗物上的注册属性。
3. 出牌、音符、和弦异常：从 `MgrNoteSystem.cs` 沿着 `CardNoteResolver.cs`、`MgrCombatState.cs`、`MgrNoteEffects.cs` 检查。
4. 演奏入队、触发或结算异常：看 `MgrPerformanceSystem.cs`、`MgrPerformanceState.cs`、`MgrPerformanceEntry.cs`。
5. 机制正常但画面异常：只看第二张图中的表现层；不要先改状态层。

## 图二：音符与演奏 UI / 动画

```mermaid
flowchart LR
    TUNE["MgrVisualTuning.cs<br/>集中布局与动画参数"]
    CREATURE["NCombatRoom 的角色节点<br/>两套 UI 的共同坐标原点"]

    subgraph NOTES["音符 UI：状态是真相，Godot 节点只是镜像"]
        NOTE_EVENT["出牌或卡牌效果调用 ChannelNote"]
        NOTE_ADD["MgrCombatState.AddNote<br/>记录本回合生成数并判断和弦"]
        NOTE_GATE["SemaphoreSlim 动画门<br/>同批音符按调用顺序逐个入场"]
        NOTE_RACK["MgrNoteRack<br/>相对角色 RackOffset；动态计算槽位间距"]
        NOTE_SLOT["NoteSlot × 当前 Capacity"]
        EMPTY["空槽<br/>12 段虚线圆框"]
        ENTRANCE["FilledNoteEntrance<br/>下方淡入 → 放大过冲 → 回落"]
        IDLE["MgrFloatingNoteVisual<br/>随机相位、速度、初始缩放<br/>持续上下浮动与呼吸"]
        ART["Sprite2D + EffectAmount<br/>音符图片与带颜色描边的数值"]
        CHORD["槽位填满<br/>保持一小段时间后恢复空槽"]
        ACCEL_NOTE["生成动画加速<br/>max(0.10, 0.28 - 本回合已生成数 × 0.018)"]
        ACCEL_CHORD["和弦停留加速<br/>max(0.12, 0.42 - 本回合已触发数 × 0.05)"]

        NOTE_EVENT --> NOTE_ADD
        NOTE_ADD --> NOTE_GATE
        NOTE_GATE --> NOTE_RACK
        NOTE_RACK --> NOTE_SLOT
        NOTE_SLOT -->|"未填充"| EMPTY
        NOTE_SLOT -->|"已填充"| ENTRANCE
        ENTRANCE --> IDLE
        IDLE --> ART
        NOTE_ADD -->|"Phrase 完成"| CHORD
        CHORD --> NOTE_RACK
        ACCEL_NOTE --> NOTE_GATE
        ACCEL_CHORD --> CHORD
    end

    subgraph PERF["演奏 UI：有序牌列与原生牌堆路由并行"]
        PLAY["普通打出或效果直接入队"]
        OBSERVE["ObserveResolvedCardPlay / EnqueueCard<br/>建立 MgrPerformanceEntry"]
        PERF_STATE["有序队列<br/>最早进入者在数组首位、画面最右侧"]
        PERF_RACK["MgrPerformanceRack<br/>牌间距受 MaximumWidth 压缩并重叠"]
        ENTER["入队动画<br/>搜索原 NCard 最多 30 帧<br/>取消原牌堆 Tween，飞入并淡出原节点"]
        MINI["PerformanceCardView<br/>缩略牌 + 剩余次数 + HoverHitbox"]
        HOVER["CanvasLayer 90<br/>鼠标右侧生成完整 NCard 预览并限制在屏幕内"]
        TURN["回合开始<br/>按快照从旧到新处理"]
        PULSE["原地触发反馈<br/>1.0 → 1.2 → 1.0，并闪紫色 Glow"]
        AUTOPLAY["CardCmd.AutoPlay<br/>skipCardPileVisuals = true"]
        COUNT["剩余演奏次数 -1<br/>刷新牌面与数字"]
        FINISH["次数归零<br/>OnPerformanceFinished 钩子"]
        ROUTE["塔二原生结果路由<br/>弃牌 / 消耗 / 能力牌离场"]
        EXIT["离队动画<br/>缩小、淡出并飞向真实目标牌堆"]

        PLAY --> OBSERVE
        OBSERVE --> PERF_STATE
        PERF_STATE --> PERF_RACK
        OBSERVE --> ENTER
        ENTER --> MINI
        PERF_RACK --> MINI
        MINI --> HOVER
        TURN --> PULSE
        PULSE --> AUTOPLAY
        AUTOPLAY --> COUNT
        COUNT -->|"仍有次数"| PERF_STATE
        COUNT -->|"归零"| FINISH
        FINISH --> ROUTE
        ROUTE --> EXIT
        EXIT --> PERF_STATE
    end

    CREATURE --> NOTE_RACK
    CREATURE --> PERF_RACK
    TUNE --> NOTE_RACK
    TUNE --> NOTE_GATE
    TUNE --> IDLE
    TUNE --> PERF_RACK
    TUNE --> ENTER
    TUNE --> PULSE
    TUNE --> HOVER
    TUNE --> EXIT
    AUTOPLAY -->|"这仍是真实出牌，因此也生成音符"| NOTE_EVENT
```

### UI 调参入口

集中参数都在 `Scripts/Mechanics/MgrVisualTuning.cs`。更细的颜色、标签、层级和节点结构仍在各自的表现文件中。

| 想调整的东西 | 优先修改 | 当前关键值 |
| --- | --- | --- |
| 音符整排位置、大小、间距 | `Notes.RackOffset / ArtworkScale / DesiredSlotSpacing / MaximumRackWidth` | `(0,-350)`、`0.76`、`96`、`480` |
| 空槽外观 | `SlotRadius / EmptySlotDashCount / EmptySlotDashFill / EmptySlotDashWidth` | `30`、`8`、`0.48`、`2.5` |
| 单颗音符入场 | `FirstNoteEntranceSeconds / MinimumNoteEntranceSeconds / Entrance*` | 首颗 `0.28s`，最低 `0.10s`，起始缩放 `0.28`，过冲 `1.18` |
| 多音符生成加速 | `NoteEntranceAccelerationPerNote` | 本回合每已有一颗减 `0.018s` |
| 和弦满槽停留 | `FirstChordHoldSeconds / MinimumChordHoldSeconds / ChordHoldAccelerationPerChord` | `0.42s` → 最低 `0.12s`，每次减 `0.05s` |
| 音符漂浮与呼吸差异 | `Bob* / Breath* / InitialScaleVariance / PhaseVariance` | 上下 `5px`；缩放约 `±5.5%`；速度随机约 `±20%` |
| 演奏牌整排位置、大小、重叠 | `Performances.RackOffset / MiniatureScale / DesiredSpacing / MaximumWidth` | `(0,-500)`、`0.33`、`52`、`520` |
| 演奏牌入队 | `EnterQueueSeconds` | `0.28s` |
| 演奏触发跳动 | `TriggerScale / TriggerGrowSeconds / TriggerSettleSeconds` | `1.2`、`0.14s`、`0.18s` |
| 演奏结束离队 | `ExitSeconds` | `0.38s` |
| 悬停详情大小与位置 | `PreviewScale / PreviewGrowSeconds / PreviewMouseXOffset` | `0.8`、`0.12s`、鼠标右侧 `34px` |

完整的逐项说明见 `docs/MGR视觉特效参数表.md`。

### 表现层文件边界

| 文件 | 只负责什么 |
| --- | --- |
| `MgrVisualTuning.cs` | 集中参数，不处理战斗规则 |
| `MgrNoteVisuals.cs` | 音符槽、入场、数值、清空与布局 |
| `MgrFloatingNoteVisual.cs` | 单颗音符持续的漂浮、呼吸和随机差异 |
| `MgrPerformanceVisuals.cs` | 演奏牌入队、重叠、触发、悬停、离队 |
| `MgrNoteSystem.cs` | 决定何时播放音符/和弦表现，并提供动画加速计数 |
| `MgrPerformanceSystem.cs` | 决定何时入队、触发和结束；表现层不修改这些规则 |
