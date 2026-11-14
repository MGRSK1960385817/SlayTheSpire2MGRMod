# STS2 v0.111.0-v0.113.0 接口变化与 MGR 兼容方案

## 一、目标与证据边界

本文专门记录`v0.111.0`到`v0.113.0`这一段接口差异。当前发布候选因Steam渠道歧义临时同时支持v0.107.1、v0.111.0和v0.113.0；v0.107.1到v0.111.0的差异仍以同目录旧方案为准。

判断依据依次为：

1. 官方社区迁移说明：<https://tutorials.sts2modding.com/docs/07-migration-99-100/>；
2. 本地固定的原始程序集：v0.111.0 commit `41cef1ea`与v0.113.0 commit `16ac0fe3`；
3. 两端真实Release编译与v0.113.0程序集反编译核对；
4. RitsuLib 0.6.12的v0.111.0/v0.113.0对应变体。

网页给出迁移方向，但不能替代真实程序集编译。网页未逐条强调的`DynamicVar`、`HoverTip`与`TweenHelper`变化，正是通过编译和反编译补充确认的。

## 二、MGR实际命中的变化

| 接口 | v0.111.0 | v0.113.0 | MGR处理 |
|---|---|---|---|
| 遗物准入 | 覆盖公开虚方法`IsAllowed`，使用`IsBeforeAct3TreasureChest` | 覆盖受保护虚方法`IsAllowedInternal`，使用`IsLateInAct3` | `BookOfGrudges`与`BlackGoldRecord`以`STS2_V111`条件编译；两端都保持“第三幕晚期前允许出现”的原语义 |
| 选牌器 | `CardSelectCmd.Selector`与`LocalSelector`静态入口 | `RunManager.Instance.CardSelectorStack`按玩家提供selector | `MgrCrossVersionApi`统一提供按玩家和本地selector；宽选牌命令不再直接绑定某一版入口 |
| 撤销结束回合准备 | `UndoReadyToEndTurn(Player)` | `TryUndoReadyToEndTurn(Player)`，并新增`CanUnEndTurn` | 由兼容层归一调用；不改变MGR触发撤销的既有时机 |
| 动态变量转换 | 可覆盖`GetBaseValueForIConvertible` | 该虚方法已移除，转换采用当前显示/预览值链路 | v0.111.0保留override；两端都在owner设置和音符和弦变化时同步`BaseValue` |
| 悬停提示ID | `HoverTip.Id`可在构造后赋值 | `Id`只有getter | 不再写入ID；补充提示通过其MGR本地化键前缀识别 |
| Tween等待 | `TweenHelper.AwaitFinished`返回`bool` | 返回`TweenOutcome` | 兼容层统一为`Task<bool>`，只有`TweenOutcome.Finished`视为成功完成 |

## 三、迁移说明中核查但未命中MGR的项目

- 死亡后Hook来源对象、部分卡牌移动/奖励/地图/测试支持接口虽有变化，但当前MGR没有对应旧签名调用点。
- 新的`CanUnEndTurn`可用于提前检查，但MGR当前只需要保持原有“尝试撤销”语义，直接使用`TryUndoReadyToEndTurn`即可；不额外复制原版判断。
- `CardSelectorStack`由每位玩家分别持有，MGR必须传入效果所属玩家，不能一律使用本地玩家。只有纯界面判断才调用`LocalSelector`。
- 本轮没有发现需要新增Harmony Transpiler、改变联机消息格式或迁移存档字段的变化。

## 四、构建与发布结构

- `MGRMod.json`版本为`0.8.1`，为允许当前Steam public加载，`min_game_version`保持`0.107.1`；RitsuLib最低版本为`0.6.12`。
- `MGRMod.csproj`对v0.107.1定义`STS2_V107`、对v0.111.0定义`STS2_V111`，默认外层目标为v0.113.0。
- 发布包包含`lib/0.107.1`、`lib/0.111.0`与`lib/0.113.0`三份原生payload。
- 稳定启动器继续读取`release_info.json`，从manifest中选择不高于宿主版本的最高目标并校验SHA-256。
- 共享PCK不因托管接口变化分叉；三版payload共用同一份卡图、场景、本地化与FMOD音频Bank。

## 五、验证要求

编译阶段必须满足：三版payload和Loader均使用各自真实归档引用完成Release构建，0警告、0错误；manifest哈希与实物一致；内容登记以warnings-as-errors通过；PCK内音频文件与源文件哈希一致。

实机阶段必须分别在真实v0.107.1、v0.111.0和v0.113.0覆盖：

- Loader和RitsuLib选择正确变体；
- 记仇小本本、黑金唱片的出现阶段；
- 宽选牌与本地选牌界面、战斗结束时未完成选择的清理；
- 准备结束回合后由MGR效果撤销；
- 森罗万象的当前伤害动态数字；
- 三类补充悬停提示；
- 音符槽与演奏队列Tween正常完成、取消和场景切换；
- MGR音频Bank加载、23条GUID映射、至少一段战斗音效；
- 保存/读取与双人联机握手。

编译和PCK核验通过不能代替上述实机语义回归。在三端回归完成前，本轮状态保持“待验证”，不得把本地候选包记为已发布。
