# STS2 v0.111.0-v0.113.0 接口变化与 MGR 兼容方案

## 一、目标与证据边界

本文专门记录`v0.111.0`到`v0.113.0`这一段接口差异。当前发布候选因Steam渠道歧义临时同时支持v0.107.1、v0.111.0和v0.113.0；v0.107.1到v0.111.0的差异仍以同目录旧方案为准。

判断依据依次为：

1. 官方社区迁移说明：<https://tutorials.sts2modding.com/docs/07-migration-99-100/>；
2. 本地固定的原始程序集：v0.111.0 commit `41cef1ea`与v0.113.0 commit `16ac0fe3`；
3. 两端真实Release编译与v0.113.0程序集反编译核对；
4. RitsuLib 0.6.12的v0.111.0/v0.113.0对应变体。

网页给出迁移方向，但不能替代真实程序集编译。当前网页已经明确列出`DynamicVar.GetBaseValueForIConvertible`移除和`HoverTip`属性只读化；但它没有解释能力克隆阶段的赋主时序，也没有列出`TweenHelper.AwaitFinished`返回类型变化。后两项是通过本地两版程序集编译与反编译补充确认的。

## 二、MGR实际命中的变化

| 接口 | v0.111.0 | v0.113.0 | MGR处理 |
|---|---|---|---|
| 遗物准入 | 覆盖公开虚方法`IsAllowed`，使用`IsBeforeAct3TreasureChest` | 覆盖受保护虚方法`IsAllowedInternal`，使用`IsLateInAct3` | `BookOfGrudges`与`BlackGoldRecord`以`STS2_V111`条件编译；两端都保持“第三幕晚期前允许出现”的原语义 |
| 选牌器 | `CardSelectCmd.Selector`与`LocalSelector`静态入口 | `RunManager.Instance.CardSelectorStack`按玩家提供selector | `MgrCrossVersionApi`统一提供按玩家和本地selector；宽选牌命令不再直接绑定某一版入口 |
| 撤销结束回合准备 | `UndoReadyToEndTurn(Player)` | `TryUndoReadyToEndTurn(Player)`，并新增`CanUnEndTurn` | 由兼容层归一调用；不改变MGR触发撤销的既有时机 |
| 动态变量转换 | 可覆盖`GetBaseValueForIConvertible` | 该虚方法已移除，转换采用当前显示/预览值链路 | v0.111.0保留override；能力真正施加、叠层及音符和弦变化后同步`BaseValue`，禁止在能力克隆阶段提前读取持有者 |
| 悬停提示ID | `HoverTip.Id`可在构造后赋值 | `Id`只有getter | 不再写入ID；补充提示通过其MGR本地化键前缀识别 |
| Tween等待 | `TweenHelper.AwaitFinished`返回`bool` | 返回`TweenOutcome` | 兼容层统一为`Task<bool>`，只有`TweenOutcome.Finished`视为成功完成 |

### 动态变量的能力克隆时序

`PowerModel.MutableClone()`会先在`DeepCloneFields()`中执行`DynamicVarSet.Clone(this)`并调用每个动态变量的`SetOwner()`，之后`PowerCmd.Apply()`才会进入`ApplyInternal()`为能力设置战斗持有者。也就是说，动态变量此时已经持有“可变能力实例”，但该能力的`Owner`仍为空。

“八十八键的宇宙”曾在`CurrentDamageVar.SetOwner()`中立即读取`DisplayAmount`，从而沿`Owner.Player`触发空引用，使整张牌无法施加能力。动态变量如依赖能力持有者或战斗状态，必须等到`AfterApplied`、对应能力层数变化Hook或更晚的明确生命周期再同步，不能把`SetOwner()`误当成“能力已经进入战斗”。

## 三、卡牌功能逐项复核

本轮把迁移页的v0.111.0至v0.113.0条目、本地程序集公开/受保护成员差异、MGR源码调用点和反射/Harmony目标进行了交叉映射。除已修复的“八十八键的宇宙”外，没有发现第二个已经能够确认会让卡牌失效或改变结算结果的接口回归。

| 风险面 | MGR命中范围 | 本地程序集核对 | 结论 |
|---|---|---|---|
| 自定义`DynamicVar` | `UniverseOf88KeysPower.CurrentDamageVar` | v0.113.0移除可覆盖的转换方法，并让转换与字符串显示统一读取`PreviewValue`；能力克隆时仍会在战斗持有者赋值前调用`SetOwner()` | 已确认并修复；这是本轮唯一确认会导致整张牌失效的接口相关BUG |
| 原版计算变量 | `Bird`、`Chorus`、`MaguroDash`、`NightWalker`、`RainbowScale`使用`CalculatedVar`；`MaguroAssault`、`MaguroCleave`使用`CalculatedDamageVar`；`HeatAbnormal`继承`DamageVar` | v0.113.0的`CalculatedVar`不再覆盖旧转换方法和`ToString()`，但原版基类已改为显示`PreviewValue`；`SetOwner()`设置`BaseValue`时会重置预览值，`UpdateCardPreview()`继续重新计算。`CalculatedDamageVar`和`DamageVar`的预览Hook链保持一致 | 无需MGR补丁；未发现数字停留为0、只显示基础值或结算取错值的路径 |
| 按玩家选牌栈 | `ImagineCreatePower`的宽选牌界面；其余牌使用原版`CardSelectCmd`入口 | v0.113.0将selector移到`RunManager.Instance.CardSelectorStack`；兼容层按效果所属玩家获取selector，本地UI判断才读取`LocalSelector` | 已适配；其余直接调用`CardSelectCmd`的牌由原版内部处理，无旧静态selector引用 |
| 变形回调重命名 | `ImagineCreatePower`调用`CardCmd.Transform` | 原版由`AfterTransformedFrom/To`改为携带另一张牌参数的新回调；MGR没有覆盖旧或新回调，类型替换状态附着于同一张候选克隆 | 无需补丁；仍需实机确认变形后的类型、升级和牌堆位置 |
| 伤害命令重载 | `Bird`、`HeatAbnormal`直接传入伤害Hook类型；`CrimeAndPunishmentPower`、`FrenzyPower`、`UniverseOf88KeysPower`和音符效果通过兼容层造成单体伤害 | v0.113.0只给多目标`IEnumerable<Creature>`末级重载增加可选`ModifyDamageHookType`并新增`DamageTargetData`重载；MGR兼容层筛选的6/7参数单体重载在两版中保持不变 | 无需补丁；不会误选新增的8参数多目标重载 |
| `HoverTip`只读属性 | MGR的卡牌、能力、遗物补充提示 | v0.113.0的`Id`等属性不可再赋值；MGR只通过本地化字符串构造提示，并读取构造器生成的ID前缀，不在构造后写属性 | 已适配；提示功能不受影响，仍需实机查看排版与去重 |
| Tween完成结果 | 音符槽、演奏队列、能力/枪击/终结视觉 | v0.111.0返回`bool`，v0.113.0返回`TweenOutcome`；需要判断成功与否的路径经兼容层只把`Finished`视为成功，其余直接`await`的路径原本就忽略返回值 | 已适配；该变化未列在当前迁移页中，但不会改变卡牌结算，只影响视觉清理判断 |
| 生物中心VFX | 流血冲击与注视效果 | v0.113.0新增可选`forceIfDead = false`；MGR继续使用两参数调用，默认行为与v0.111.0相同 | 无需补丁 |
| 反射与Harmony目标 | 卡牌克隆/复制、牌池生成、能力卡飞行动画、假商人、音效等共享补丁 | 已逐个核对v0.111.0与v0.113.0目标类型、成员名和参数；当前使用的目标仍存在且签名匹配 | 未发现编译无法捕获的目标漂移 |

`DynamicVar`的影响因此不能简单理解为“所有动态数字都会坏”。原版自带变量已经随v0.113.0迁移；需要额外审查的是模组自行派生`DynamicVar`、覆盖旧转换方法，或在`SetOwner()`里读取尚未准备好的卡牌/能力持有者。本次兼容审计时，MGR的这类自定义直接派生只有“八十八键的宇宙”。后续卡牌重做已移除此旧版衰减数值及其自定义动态变量。

## 四、迁移说明中核查但未命中MGR的项目

- 死亡后Hook来源对象、额外回合Hook来源对象、`ModifyUnblockedDamageTarget`可空状态、箭矢/格挡前Hook、部分卡牌移动/奖励/地图/测试支持接口虽有变化，但当前MGR没有对应旧签名调用点。
- `HpLossHookPhase`新增位标记并改变既有枚举数值；MGR没有引用该枚举，也没有把它序列化为整数，因此不会发生旧数值映射到新阶段的问题。
- `CreatureCmd.Kill`、`LoseMaxHp`和`GameMode.Custom`等签名/成员变化在MGR中没有调用点。
- 新的`CanUnEndTurn`可用于提前检查，但MGR当前只需要保持原有“尝试撤销”语义，直接使用`TryUndoReadyToEndTurn`即可；不额外复制原版判断。
- `CardSelectorStack`由每位玩家分别持有，MGR必须传入效果所属玩家，不能一律使用本地玩家。只有纯界面判断才调用`LocalSelector`。
- 本轮没有发现需要新增Harmony Transpiler、改变联机消息格式或迁移存档字段的变化。

## 五、构建与发布结构

- `MGRMod.json`版本为`0.8.1`，为允许当前Steam public加载，`min_game_version`保持`0.107.1`；RitsuLib最低版本为`0.6.12`。
- `MGRMod.csproj`对v0.107.1定义`STS2_V107`、对v0.111.0定义`STS2_V111`，默认外层目标为v0.113.0。
- 发布包包含`lib/0.107.1`、`lib/0.111.0`与`lib/0.113.0`三份原生payload。
- 稳定启动器继续读取`release_info.json`，从manifest中选择不高于宿主版本的最高目标并校验SHA-256。
- 共享PCK不因托管接口变化分叉；三版payload共用同一份卡图、场景、本地化与FMOD音频Bank。

## 六、验证要求

编译阶段必须满足：三版payload和Loader均使用各自真实归档引用完成Release构建，0警告、0错误；manifest哈希与实物一致；内容登记以warnings-as-errors通过；PCK内音频文件与源文件哈希一致。

实机阶段必须分别在真实v0.107.1、v0.111.0和v0.113.0覆盖：

- Loader和RitsuLib选择正确变体；
- 记仇小本本、黑金唱片的出现阶段；
- 宽选牌与本地选牌界面、战斗结束时未完成选择的清理；
- “想象、创造”选牌后的变形结果、升级状态和牌堆位置；
- 准备结束回合后由MGR效果撤销；
- 森罗万象的当前伤害动态数字；
- `Bird`、`Chorus`、`MaguroDash`、`NightWalker`、`RainbowScale`、`MaguroAssault`、`MaguroCleave`和`HeatAbnormal`的动态数字预览与实际结算一致；
- 三类补充悬停提示；
- 音符槽与演奏队列Tween正常完成、取消和场景切换；
- MGR音频Bank加载、23条GUID映射、至少一段战斗音效；
- 保存/读取与双人联机握手。

编译和PCK核验通过不能代替上述实机语义回归。在三端回归完成前，本轮状态保持“待验证”，不得把本地候选包记为已发布。
