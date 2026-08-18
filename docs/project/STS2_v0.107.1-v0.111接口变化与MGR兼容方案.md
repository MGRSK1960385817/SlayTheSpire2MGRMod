# STS2 v0.107.1—v0.111 接口变化与 MGR 兼容方案

## 1. 范围与结论

目标是让同一份 MGR 发布包在正式版 `v0.107.1` 与测试版 `v0.111.0` 上载入和运行。

上游资料为[“正式版 至 测试版”迁移记录](https://tutorials.sts2modding.com/docs/07-migration-99-100/)。该页面明确说明它只记录“可能要求模组改代码”的变化，不是完整 API diff。因此本文把上游记录、MGR 实际调用点以及 CrossVersionCompat（下称 CVC）v0.9.22 的行为合并分析。

最终方案不依赖或内嵌 CVC，而是原生多版本包：

1. 顶层 `MGRMod.dll` 是仅使用双端稳定接口编译的 `MGRMod.Loader`；
2. 游戏为 v0.107.1 时加载 `lib/0.107.1/MGRMod.dll`，为 v0.111.0 时加载 `lib/0.111.0/MGRMod.dll`；
3. 两个 payload 来自同一套源码，通过 `STS2_V107` 只分叉无法共用的 override 签名，并分别引用对应的 STS2/RitsuLib 程序集；
4. `MGRMod.json` 最低游戏版本为 `0.107.1`，运行时依赖只保留 RitsuLib 0.5.13。

本文件保留 v0.107.1—v0.111 的详细差异与实现证据。长期只滚动保留“当前正式版 + 最新测试版”两个发布目标，后续版本更新步骤和归档规范统一见 [`MGR双版本兼容维护手册.md`](MGR双版本兼容维护手册.md)。

游戏加载器首先加载文件名为 `MGRMod.dll`、程序集身份为 `MGRMod.Loader` 的小型启动器。启动器从 `release_info.json` 读取宿主版本，校验 payload 的 SHA-256，将正确版本载入同一 `AssemblyLoadContext`，关联回 MGR 的 Mod 记录，然后调用真正的 `MGRMod.Entry.Initialize()`。启动器与 payload 的程序集身份不同，因此不会发生同名程序集冲突。

## 2. v0.107 到 v0.108

### 战斗 Hook 与卡牌去向

- `AbstractModel.ModifyDamageAdditive`、`ModifyDamageMultiplicative`、`ModifyDamageCap` 增加 `CardPlay?` 参数。
- `CardModel.GetResultPileTypeForCardPlay()` 改为 `GetResultPileTypeAndPositionForCardPlay()`，返回值从 `PileType` 改为 `(PileType, CardPilePosition)`。
- `AttackCommand.FromCard`、`FromOsty` 增加 `CardPlay?`；`AttackCommand.CreateContextAsync` 与 `AttackContext.CreateAsync` 的参数从 `CardModel` 改为 `CardPlay`。
- `CardModel.PortraitPngPath` 由私有成员变成 `protected virtual`。
- 新增 `CardModel.CreateCloneForPlayer(Player)` 与 `GiveToAnotherPlayer(Player)`。

### 充能球、事件与时间线

- `OrbModel.Triggered` 改名为 `PassiveActivated`；旧 `Trigger()` 拆为 `ActivateEvoke(Creature[])` 与 `TriggerPassive(PlayerChoiceContext, Creature?)`，并新增 `EvokeActivated`。
- `EventModel.BeginEvent` 增加 `EventCombatSynchronizer?` 参数。
- 删除 `EventModel.GenerateInternalCombatState`、`ResetInternalCombatState` 和 `EncounterModel.IsDebugEncounter`；由新的 `EventCombatSynchronizer` 接管事件战斗状态。
- `EpochModel` 的 `Year`、`EraName`、`ModelId`、`IsArtPlaceholder`、`PackedPortraitPath` 被删除或私有化；新增 `HasRealPortrait` 与 `AllEpochs`。

### 卡池、存档与输入

- `CardCreationOptions` 删除 `CustomCardPool`、`ForNonCombatWithDefaultOdds`、`WithRngOverride`；`WithCardPools` 不再接收过滤器，改用 `WithFilter`。
- `SaveManager.IncrementNumReloads` 的 `bool isMultiplayer` 改为 `NetGameType`，并增加测试参数。
- `UserDataPathProvider` 增加可强制指定 Mod 状态的 profile/account 路径重载。
- `VoteToMoveToNextActAction` 构造函数增加当前 Act 索引。
- 控制器名称统一：`dPadNorth/South/East/West` → `dPadUp/Down/Right/Left`，`joystickPress` → `lStickPress`。

## 3. v0.108 到 v0.109

### CardLocation 与 Hook 签名

- 新增 `CardLocation(Player, PileType, CardPilePosition)`，替代散落的 `(PileType, CardPilePosition)` 元组。
- `CardModel.GetResultPileTypeAndPositionForCardPlay()` 改为 `GetResultLocationForCardPlay()`。
- `AbstractModel/Hook.ModifyCardPlayResultPileTypeAndPosition` 改为 `ModifyCardPlayResultLocation`。
- `AfterModifyingCardPlayResultPileOrPosition` 改为 `AfterModifyingCardPlayResultLocation`。
- `AfterBlockBroken` 增加 `PlayerChoiceContext`、目标与 breaker；`CreatureCmd.LoseBlock` 同步增加上下文和来源。
- `CardModel.CreateDupe()` 增加新的 owner 参数。

### 抽牌、战斗流程与选择上下文

- `CardPileCmd.Draw` 去掉 `async` 实现，但公开返回类型仍是 `Task<IEnumerable<CardModel>>`。
- 新增 `DrawWithoutBlockingOnOtherPlayers`，供联机中跨玩家抽牌使用。
- 新增 `CardCmd.ApplySingleTurnRetain`。
- `CombatManager.EndCardOrPotionEffect` 从 `void` 变成 `Task`；`EndPlayerTurnPhaseTwoInternal` 增加可选取消令牌；新增 `CombatBegan` 与 `RemoveDeadPlayerCardsFromCombat`。
- `PlayerChoiceContext.SignalPlayerChoiceBegun` 从只接收 options 改为同时接收 chooser 与 options；新增 `ModelStack`、`OwnerId` 和 `BranchingPlayerChoiceContext`。

### RNG 与序列化

- RNG 种子从 `uint` 扩展为 `ulong`，默认种子长度从 10 位变为 12 位。
- `StringHelper.GetDeterministicHashCode` 返回 `ulong`，旧算法保留为 `GetDeterministicHashCodeOld`。
- `Rng`、`PlayerRngSet`、`RunRngSet` 构造函数和 Seed 类型同步改为 `ulong`；删除 `Rng.Counter/FastForwardCounter`。
- 新增 `SerializableRng` 以及 RNG 的序列化/反序列化接口；序列化字典从 counter 整数改为 RNG 状态。
- `SavedPropertiesTypeCache` 的职责并入 `ModelIdSerializationCache`。

## 4. v0.109 到 v0.110

### CombatId

- 新增 `CombatId`，用于阻止上一场战斗的延迟操作泄露到下一场战斗。
- `BeginCardOrPotionEffect` 返回 `CombatId?`。
- `EndCardOrPotionEffect`、`CheckForEmptyHand`、`HandlePlayerDeath`、`RemoveDeadPlayerCardsFromCombat` 增加 `CombatId?`。
- `EndPlayerTurnPhaseTwoInternal` 和 `SwitchFromPlayerToEnemySide` 删除旧参数。

### 输入、选择与联机

- `MegaInput.accept` 改名为 `confirm`，删除 `releaseCard`，新增 `endTurn`。
- `BranchingPlayerChoiceContext` 构造函数增加来源 `GameAction`。
- 新增 `PeerVersionInfo`，用于联机版本和 Mod 校验。
- 单一 `LobbyPlayer` 拆为 `RunLobbyPlayer`、`LoadRunLobbyPlayer`、`StartRunLobbyPlayer`；`ConnectedPlayerIds` 改名为 `PlayerIds`。
- `ProgressState.TotalUnlocks` 变为计算属性，并新增 `GrantNextUnlock()`。

## 5. v0.110 到 v0.111

### 卡牌与回合结束

- `CardModel` 新增 `GeneratePlayCount` 与 `MoveToResultPileWithoutPlaying`。
- `CardCmd.Exhaust` 从 `Task` 改为 `Task<CardPileAddResult?>`。
- 虚无等回合结束卡牌改成先交错移动、再统一结算；新增 `StuckCombatException`。

### 角色动画

- `CharacterModel.GenerateAnimator(MegaSprite)` 增加 `Creature` 参数。
- 新增 `AnimationStates` 和低血量判断；原版角色转为声明标准动画状态。
- `AnimState` 新增分支/后继状态接口与低血量待机常量。

### 联机握手

- 新增 `HandshakeManager`、`HandshakeResult`、`HandshakeStatus`、`IHandshakeHandler`，在连接早期校验版本与 Mod。
- `PeerVersionInfo` 不再实现 `IPacketSerializable`，`Deserialize` 改为 `TryDeserialize`，并新增 `IsModded`。
- 大厅玩家和消息中的 version 信息重新布置；删除 `ClientConnectionFailedMessage`。
- 网络服务接口新增本地版本、连接失败事件和按 peer 查询版本；多个构造函数增加版本或 reader/writer 参数。
- `NetError` 重新分段编号并新增 `InvalidHandshake`、`LobbyJoinTimeout`；硬编码数值的模组必须更新。

### Mod、平台和其他接口

- `IModManagerFileIo` 新增递归建目录和复制文件；`ModManager` 的相关内部方法增加 file-I/O 参数。
- `PlatformUtil.OpenInviteDialog` 改为返回 `bool` 的 `TryOpenInviteDialog`。
- confirm 默认键和 end-turn 统一，新增旧设置存档迁移。
- `SaveManager/PrefsSaveManager` 新增加载状态。
- 多个 Orb/VFX 类型迁移到 `Nodes.Orbs`、`Nodes.Vfx.Utilities`、`Nodes.Debug` 命名空间。

## 6. CVC v0.9.22 的工作原理

CVC 的发布包由 `CrossVersionCompat.dll`、`Mono.Cecil.dll`、manifest 和版本 profile 组成。其说明和二进制离线检查显示，它按以下流水线工作：

1. 启动时用 Harmony 拦截 `AssemblyLoadContext.LoadFromAssemblyPath(string)`，因此必须先于目标模组载入。
2. 对即将载入的模组 DLL 使用 Mono.Cecil 检查所有指向 `sts2` 的方法、字段和类型引用。
3. 能直接绑定当前游戏 API 的调用保持不动；失效调用被重定向到 DLL 内生成的 shim。
4. 对已知语义变化使用人工维护的 backport，例如 `CardLocation`、`CreateDupe(Player)`、`CardCmd.Exhaust`、RNG 状态等。
5. 当前游戏缺少的新类型会被重定向到 CVC 自带 stand-in 类型。
6. 对参数增减但语义仍可对齐的调用生成参数桥接；不确定的转换会拒绝猜测并写入报告。
7. 模组载入后扫描失效 override，以 Harmony 复活参数只增减一个的同名虚方法；卡牌去向和 `AfterBlockBroken` 另有专用适配器。
8. 可绕过 `min_game_version`/Steam branch 门槛、隔离无法应用的 Harmony patch，并生成兼容报告与缓存修复后的 DLL。

CVC 不是“无条件让任何模组兼容”。它的安全边界是：明确可映射的 ABI 变化会修；无法证明语义等价的调用会报告为 refusal，命中时仍可能抛异常。

直接把 CVC 放进 MGR 文件夹并不能让 MGR 自我修复：CVC 的拦截器必须先于被修复程序集安装，而 MGR 一旦已经载入就不能原地改写。可以再增加一层 bootstrap，让它先调用 CVC 重写 payload，但这会把 Mono.Cecil、通用 backport、缓存和报告系统全部带进 MGR。MGR 只有两种明确目标版本，原生双 payload 更小、更可预测，也不会受 CVC profile 更新节奏影响。

## 7. MGR 命中点与处理

| MGR 命中点 | 风险 | 处理 |
| --- | --- | --- |
| `MgrCard.GetResultLocationForCardPlay` | v0.107 没有 `CardLocation`/新 virtual | `STS2_V107` 原生 override `GetResultPileTypeForCardPlay`；v0.111 保留 `CardLocation` override |
| `MgrNoteSystem.ModifyCardPlayResultLocation` | 同上 | `STS2_V107` 原生 override 旧版 tuple hook；v0.111 编译新版 hook |
| 三处带 `CardPlay?` 的伤害修正 override | v0.107 基类少一个参数，override 静默失效 | 条件编译两个原生签名，不再依赖 dead-override revival |
| `AttackCommand.FromCard(card, cardPlay)` | v0.107 只有单参数版本 | v0.107 payload 提供编译期扩展，忽略旧版不存在的 `CardPlay` 上下文 |
| `CardModel.CreateDupe(Player)` | v0.107 只有无参版本 | `MgrCrossVersionApi` 反射选择重载并校正 owner |
| `CardCmd.Exhaust` | v0.111 返回泛型 Task | 分别针对对应返回类型原生编译；MGR 不读取返回结果 |
| `CreateCloneForPlayer` | v0.107 不存在，CVC 未提供改名映射 | `MgrCrossVersionApi` 反射调用新接口；旧版用 `CreateClone` 并设置 owner |
| `DrawWithoutBlockingOnOtherPlayers` | v0.107 不存在；不同测试版的参数还发生过变化 | `MgrCrossVersionApi` 反射选择现有重载；v0.107 降级为普通 `Draw` |
| `SignalPlayerChoiceBegun` | v0.107 少 chooser 参数 | `MgrCrossVersionApi` 反射选择一参/两参版本 |
| `CardSelectCmd.LocalSelector` | v0.107 不存在 | 反射读取；旧版没有测试选择器时进入正常 UI 选择流程 |
| `CardSelectCmd.UndoEndTurnIfNecessary` | 较早版本没有这个 helper | MGR 内实现等价逻辑，调用跨版本稳定的 `CombatManager` 接口 |
| `NCardExhaustVfx` | v0.107 没有该类型 | 新版反射调用原生消耗动画；v0.107 使用 MGR 自带的缩小、旋转、淡出动画 |
| `CreatureCmd.Damage`/`Hook.ModifyDamage` | v0.108 增加 `CardPlay` 参数 | 通用伤害命令用反射选择签名；两个预览调用按目标版本条件编译 |
| 启动器关联 payload 到 `Mod` | v0.107 只有单数 `assembly`，且 `TryLoadMod` 会在初始化器返回后把它覆盖回 Loader；v0.111 提供公开关联方法并使用复数 `assemblies` | 优先调用新版公开方法；旧版先写入单数字段，再在 `OnModDetected` 中于原版覆盖完成后恢复 payload，并在无法关联时明确报错 |
| RitsuLib 0.5.13 运行时版本 | MGR 在两端都必须绑定同一套 RitsuLib API | 编译时分别使用主包与 `STS2.RitsuLib.Compat.0.107.1`；Workshop 变体包运行时选择对应实现 |

v0.107 上 `DrawWithoutBlockingOnOtherPlayers` 的降级只影响跨玩家 action queue 的交错方式：卡牌仍会为每名存活玩家完成抽牌并等待结果，不会丢失效果。

## 8. 验证记录与边界

- v0.107.1 payload 使用正式版客户端 commit `59260271` 的真实 `sts2.dll` 和 RitsuLib compat 包原生编译：成功，0 warning / 0 error。
- v0.111.0 payload 使用保存的测试版程序集和 RitsuLib 主包原生编译：成功，0 warning / 0 error。
- 两个 payload 的程序集引用都不包含 `CrossVersionCompat` 或 `Mono.Cecil`。
- `MGRMod.Loader` 使用 v0.107.1 编译成功；其全部 2 个 `sts2` 成员引用在 v0.107.1 与 v0.111.0 上均能解析，0 个缺失。
- 启动器原型在真实正式版环境中读取到 `v0.107.1`，能够正确选择并加载 `lib/0.107.1/MGRMod.dll`；顶层程序集身份为 `MGRMod.Loader`，payload 身份为 `MGRMod`。后续实机启动曾发现，v0.107.1 的模组记录使用单数 `Mod.assembly` 且没有新版 `ModManager.AssociateAssemblyWithMod` API；第一次字段修复又被 `TryLoadMod` 在初始化返回后覆盖。当前方案已改为在 `OnModDetected` 完成通知中最终恢复 payload，并保留明确失败检查。
- CVC 曾用于发现差异：对旧方案的 v0.111 编译产物在 v0.107.1 上会重定向 44 个调用点、生成 3 个 shim、应用 4 个 backport。最终原生方案已把这些运行时转换移到源码和构建阶段，不再随包分发 CVC。
- RitsuLib 0.5.13 的 v0.107.1 变体哈希与其变体 manifest 一致；MGR 对 RitsuLib 的 125 个成员引用全部能在该变体中解析，0 个缺失。
- 内容校验脚本以 warnings-as-errors 通过：94 张卡牌、80 张奖励池卡牌、11 件遗物、288 个导入 UID 均有效。
- 静态 ABI、版本选择、两种 `Mod` 关联形态、v0.107.1 原版覆盖顺序模拟、双端启动器编译和完整包构建验证已经完成；v0.107.1 第二版修复包已经部署，但仍需正常 Steam 客户端确认模型预热通过，再在真实 v0.107.1 与 v0.111.0 客户端分别做游戏内回归。

当前已具备 v0.107.1 客户端和程序集，但本次自动验证没有代替人工操作完成“启动、选人、战斗、联机和存档”全流程。建议最低回归集：

1. 启动时确认日志显示 `MGRMod.Loader` 选择正确 payload，同时 RitsuLib 选择对应变体；
2. 进入 MGR 单人战斗，打出四类音符并完成一次演奏；
3. 验证会改变卡牌去向的演奏牌，确认去向 override 已复活；
4. 联机验证“众生万象”和“交给我吧”，覆盖跨 owner clone、跨玩家抽牌和选择同步；
5. 覆盖至少一次 `CardCmd.Exhaust`、伤害加法/乘法修正和存档续跑；
6. 在 v0.111.0 重复上述关键路径，确认兼容桥没有改变最新版行为。
