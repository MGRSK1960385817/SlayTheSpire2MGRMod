# MGR 对局数据统计接入说明

## 设计目标

MGR 使用 RitsuLib 0.5.13 的授权、缓存、重试和 PostHog 发送能力，但不使用 RitsuLib 的内置 `RunHistory` 采集器。内置采集器会保留完整 `SerializableRun`；MGR 改为监听 `RunEndedEvent`，自行构造字段白名单。

入口与过滤逻辑位于 `Scripts/Telemetry/MgrTelemetry.cs`，精简负载结构位于 `Scripts/Telemetry/MgrRunMetricsBuilder.cs`。

## 与 RitsuLib 官方教程的关系

实现已按 [RitsuLib 数据遥测教程](https://tutorials.sts2modding.com/docs/04-ritsulib/04-29-telemetry/) 复核：

- 使用独立 `TelemetryApplicant`、独立授权申请和 `ITelemetryClient.CapturePayload`；RitsuLib 负责玩家授权、本地队列、失败重试和发送隔离。
- 教程的“自动上传一局数据”会把完整 `SerializableRun` 作为 `RunHistory` 负载发送。MGR 为了数据最小化，刻意不申请 `RunHistory`，而是在对局结束后构造受版本控制的自定义字段白名单。
- 教程中的 `captureFilter` 只属于自动 `RunHistory` 请求；MGR 使用 `MgrTelemetryEligibility` 和 `MgrRunSanityValidator` 完成等价且更细的上传前过滤。
- `properties` 只保存常用查询维度，完整的层数、卡组和 MGR 机制聚合放在结构化 `payload` 中；这与教程对“索引属性保持扁平、复杂数据放负载”的建议一致。游戏版本与模组版本均同时作为查询属性保留。
- 教程建议不要发送账号标识；MGR 目前根据项目既定决定保留明文 Steam ID，以便跨安装识别同一玩家。这是有意识的隐私取舍，不是教程默认做法，因此授权文本现在会明确披露。若将来不再需要反查账号，应优先改为服务端 HMAC 后的稳定 ID。
- MGR 没有使用 contribution provider：当前只有 MGR 自己的一条自定义事件，不需要跨申请方拼接数据，也避免引入额外授权依赖。
- 当前仍直接使用教程提供的 PostHog 适配器。正式发布时可以把地址换成代理以隐藏项目采集 Token、做服务端限流和字段复核，但代理不是客户端真实性证明；不应仅为了“看起来安全”而增加一个没有实际校验能力的转发层。

RitsuLib 0.5.13 的内置 PostHog 适配器不会把 MGR 的 `event_id` 写入 PostHog 顶层 `uuid`，因此当前不宣称 PostHog 会自动进行强幂等去重。`event_id` 保持稳定并作为查询属性上传；下载和分析时应以它去重。若未来确实需要入库级幂等，应由受控代理把稳定事件 ID 映射到 PostHog 顶层事件 UUID，而不是在客户端伪造一个未经适配器支持的属性。

## 授权与上传端点

- 玩家必须在 RitsuLib 的遥测授权界面明确同意 MGR 独立的 `mgr_clean_run_metrics` 申请项。开发阶段不迁移或兼容旧的 `run_history` 授权记录。
- MGR 不申请 RitsuLib 的 `run_history`，也不启用其完整 `RunHistory` 采集器；实际只上传下面列出的字段白名单负载 `mgr_run_completed`，因此不会由 MGR 额外产生 `run_history.completed`。
- 同时尊重原版“上传游戏数据”开关；原版开关关闭时不上传。
- 当前使用 PostHog US Cloud，RitsuLib 最终请求其 `/batch/` 接口。
- 客户端只包含 PostHog 项目采集 Token，不包含个人密钥、管理密钥或读取权限密钥。
- 上传失败由 RitsuLib 缓存和重试，不会阻塞游戏或对局结算。
- 经玩家明确授权后，每条记录会包含明文 Steam ID 和 MGR 随机安装 ID；不会上传玩家昵称。
- 授权界面的说明会直接列出明文 Steam ID、主要数据类别和明确不发送的内容，避免只写笼统的“改善体验”。

## 整局过滤规则

只有同时满足以下条件的记录才允许上传：

1. 玩家同时允许原版数据上传和 MGR 遥测申请；
2. 正式游戏版本，不是编辑器、测试环境或全控制台模式；
3. 不是玩家档案记录中的前三局；`NumberOfRuns <= 3` 时不上传；
4. 正常胜利或失败均可；放弃局必须游玩至少 5 分钟，或到达至少 10 个地图节点；
5. 标准模式，不是每日挑战或其他自定义模式；
6. 严格单人，`Players.Count == 1`；
7. 唯一玩家使用 MGR；
8. 非放弃局至少到达 5 个地图节点；放弃局改用第 4 条的独立门槛；
9. 对局计时有效；
10. 如果完全胜利，计时时长不得少于 20 分钟；
11. 本局没有实际调用 Loadout/LoadOut2 的数据修改功能；仅安装或打开其面板不会导致排除。
12. 同一 Steam ID 在本次安装中距离上一次接受提交至少 60 秒；这是客户端防重复，不是服务器认证。
13. 对局结构与统计值通过下述硬上限校验；越界记录直接丢弃，不进入隔离数据集。

Loadout/LoadOut2 的真实清单 ID 是 `Loadout`。MGR 会在不引用其程序集的情况下，动态监听 Loadout 所有实际增删卡牌、遗物、药水、修改数值、应用预设、杀怪等操作共同经过的变更入口。只有该入口在本局被调用时才排除；单纯安装、加载或打开面板不会排除。污染标记按本局开始时间与种子持久化，因此使用修改器后保存退出再读取，该局仍会被排除。

这是对可选第三方模组内部接口的尽力兼容。若 Loadout 更新后入口不存在，MGR 会在日志中报警并停用这项过滤，不会退回“安装即排除”的粗略规则，也不会影响游戏运行。

Loadout 的检测结果只用于决定是否上传，不会把模组清单发送到云端。

## 上传字段白名单

顶层信息：

- `event_id`、MGR 随机安装 ID、明文 Steam ID；
- 数据结构版本、MGR 版本、游戏版本；三者也作为 PostHog 查询属性保留，方便按游戏更新或模组版本切分样本；
- 胜负、标准模式、进阶等级；
- 到达地图节点数、有效对局秒数、重开次数；
- 失败时最后一个战斗遭遇 ID；
- 各幕 ID 与是否完成。

最终玩家状态：

- 角色 ID、当前/最大生命、最大能量、药水槽、金币；
- 原版已经汇总的总伤害与施加异常次数；
- 最终卡组：卡牌 ID、升级次数、加入层数、附魔 ID 与层数；
- 最终遗物：遗物 ID、获得层数；
- 剩余药水：药水 ID 与槽位。

MGR 机制整局聚合：

- 音符生成总数，以及攻击、技能、能力、状态、诅咒、星空、幽灵、万象各自的生成数；
- 填满音符槽形成和弦的次数；
- 和弦效果实际结算次数（包含积雨云涂鸦、节拍器等额外触发）；
- 演奏队列实际打出牌的次数；
- 对敌人的实际生命伤害，粗分为卡牌直接伤害、音符伤害、能力/遗物等其他来源，以及未能归类的伤害；四项能够与原版总伤害对账。

这些数据仅保存整局整数合计，不保存逐次触发时间、音符排列、目标、卡牌播放序列或单场战斗明细。从结构版本 7 开始，聚合计数写入 RitsuLib 的本局存档数据槽：玩家 SL 时，计数会与游戏状态一起回滚到保存点，再统计重新进行的操作，因此允许重载的对局保持 `tracking_complete=true` 和 `reload_safe=true`。若存档数据槽不可用，则整条记录会被校验器拒绝，而不是上传一份伪装完整的零值统计。伤害采用原版 `UnblockedDamage` 口径，即真正穿透格挡、减少敌人生命的数值。

每个地图节点：

- 幕序号、层数、地图图标类型，以及实际进入的最终房间类型；问号节点因此会保留 `map_point_type=Unknown`，同时通过 `resolved_room_type` 表明最终是事件或战斗；
- 房间/遭遇 ID、战斗回合数；不再展开重复的怪物 ID 列表；
- 当前生命、最大生命、最大生命增减、金币、回复量，以及金币获取、消费、失去和被盗；第一层先古节点把角色从 0 初始化到满血的过程不计为回复；
- 所受伤害取“原版逐次记录值”和“相邻节点生命变化反推值”中的较大值，以补齐致死伤害或直接生命变化；单节点硬上限为 `100000`；
- 获得、移除、升级与降级的卡牌；
- 卡牌、遗物、药水奖励中的选择与跳过；
- 药水使用与丢弃；
- 事件和先古选项的稳定本地化键；
- 休息点选择及商店购买的遗物、药水和无色牌。

## 身份、去重与一分钟防重复

- `install_id` 在首次需要上传时由系统安全随机源生成 128 位 GUID（32 位十六进制文本），并持久化到 `user://mgr_telemetry_identity.json`。它代表“当前游戏用户数据目录中的这次安装实例”，不是硬件指纹：同一设备、同一用户数据目录会保持不变；删除用户数据、换系统账户或换设备会生成新值。随机碰撞概率可以忽略。开发期旧格式不会兼容；无法读取有效 `install_id` 时直接生成新值并覆盖旧状态。
- `steam_id` 使用原版平台接口读取，以十进制字符串上传。Steam ID 超过 JavaScript 安全整数范围，不能作为 JSON 数字发送，否则可能被 PostHog 舍入。
- `event_id` 为 `SHA-256(install_id | steam_id | 本局种子 | 本局真实开始时间戳)` 的十六进制结果。开始时间戳能够区分同一玩家反复使用同一个种子开始的新局；同一局保存重载或由 RitsuLib 网络重试时，该时间戳不变，因此事件 ID 也不变。原始种子与开始时间本身不会上传。
- 成功交给 RitsuLib 本地队列后，记录该 Steam ID 的提交时间；60 秒内再次产生的新对局记录会被跳过。RitsuLib 对已经入队事件的网络重试不受这条限制。
- 当前没有 Steam 票据认证，Steam ID、安装 ID、时间和事件 ID 都可以被修改过的客户端伪造。这套机制用于防止程序错误、重复回调和低成本的普通重复数据，不是反作弊系统。

## 越界数据硬过滤

以下限制故意远高于正常对局，用于排除损坏记录和最粗糙的伪造数据：

所有数值上限统一定义在 `Scripts/Telemetry/MgrRunSanityValidator.cs`。其中音符、和弦与伤害计数由 `MgrRunTelemetryAccumulator.IsSane()` 执行检查，但同样引用校验器中的统一常量，不再各自维护一份数值。

- 幕数 `1..6`，地图节点总数不超过 `100`，单节点房间数不超过 `16`；
- 有效对局时长不超过 `24` 小时，进阶不超过 `20`，重载次数不超过 `1000`；
- 最终牌组 `1..1000` 张，遗物不超过 `100` 个，药水不超过 `11` 瓶；
- 最大生命不超过 `1000`，最大能量不超过 `10`，金币不超过 `10000`；不采集或校验音符槽数量；
- 单地图节点记录的奖励、事件和先古选择合计不超过 `128` 项；
- 音符、和弦、和弦结算和演奏触发等单项计数不超过 `1000000`，各类音符之和必须等于音符总数；
- 卡牌、音符和其他来源的实际伤害各不超过 `100000000`；
- 序列化后的自定义负载不超过 `2000000` 个字符。

任意检查失败时只在本地日志写入 `MGR telemetry skipped:` 原因，整条记录直接丢弃。

## 明确不上传的内容

- 完整 `SerializableRun` 或存档文件；
- 联机 Net ID、玩家昵称；
- 多人对局或其他玩家数据；
- RNG、随机概率池、卡牌/遗物抓取袋内部状态；
- 地图坐标和玩家地图涂鸦；
- 解锁状态、图鉴、成就与个人总胜率；
- 卡牌与遗物开放式 `SavedProperties`；
- 模组清单、Loadout 设置或其他模组私有数据；
- 精确开始/结束时间；
- 异常、堆栈、日志和诊断快照。

MGR 在 RitsuLib 完成授权与本地排队后、交给 PostHog 前，会再次按白名单清理传输信封：保留发送所需的框架匿名安装标识、MGR 索引字段与游戏语言，移除 `.NET` 版本、完整操作系统版本、进程架构、RitsuLib 构建配置等通用诊断字段，并设置 `$process_person_profile=false`，避免为这些事件维护 PostHog 人物档案。

PostHog 会依据网络请求自动补充 GeoIP。云端原始事件的 GeoIP 丰富化无法在客户端要求“只计算两个字段”；本地下载脚本因此只输出一份国家和城市，明确丢弃原始 IP、经纬度、邮编、时区以及 `$set/$set_once` 等副本。若未来要求云端也完全不保存 IP/经纬度，就必须关闭 GeoIP，此时国家和城市也会一并失去，或增加受控代理服务在入库前自行裁切。

`DataAnalysis/Download-MgrTelemetry.ps1` 只下载 `request_id=mgr_clean_run_metrics` 的事件，并将本地结果整理为 `uuid`、时间、事件名、申请 ID、国家、城市和 MGR 白名单负载；旧 `run_history` 测试记录与 PostHog 通用空列不会进入新的本地分析文件。

## 本地验证流程（不必完整通关）

建议分成两步，避免打一整局以后才发现云端配置有误：

1. **网络冒烟测试**：先向 PostHog 单独发送一个名称为 `mgr_telemetry_smoke_test` 的测试事件，确认项目 Token、US Cloud 地址和网络均正常。该事件不得混入正式 `mgr_run_completed` 分析。
2. **游戏链路测试**：在 RitsuLib 中允许 MGR 遥测，且本局不要实际使用 Loadout 修改功能；Loadout 可以保持安装。使用已完成至少三局的本地档案开始一局单人标准 MGR。游玩满 5 分钟后直接放弃，或到达 10 个地图节点后放弃，即满足当前放弃局门槛，无需完整通关。
3. 在日志中确认没有出现 `MGR telemetry skipped:` 或构建负载失败，再到 PostHog 的 Live Events 搜索 `mgr_run_completed`。RitsuLib 可能先写入本地队列并重试，因此断网时不会立刻出现。
4. 检查事件的 `schema_version=7`、`event_id`、`install_id`、`steam_id`、`mgr_mechanics`、`floor_reached` 和 `duration_seconds`。可以主动进行一次 SL；新记录仍应显示 `tracking_complete=true`、`reload_safe=true`，且被放弃的操作不会重复计数。

若当前档案还没有完成三局，前三局按设计必定被过滤。开发期间可使用一个已经玩过三局的测试档案，避免为了验证上传反复完成新手局。

## 下载到本地

仓库提供 `DataAnalysis/Download-MgrTelemetry.ps1`。脚本通过 PostHog 官方 `POST /api/projects/:project_id/query/` 接口执行 HogQL，默认下载 `mgr_run_completed`，并将结果写入 `DataAnalysis/Data`。该数据目录被 Git 和 Godot 导出共同排除，避免把明文 Steam ID 意外提交或打进模组包。

个人 API key 不得写入脚本或仓库。推荐仅为当前 PowerShell 进程设置环境变量：

```powershell
$env:POSTHOG_PERSONAL_API_KEY = Read-Host 'PostHog Personal API key' -AsSecureString |
    ForEach-Object { ([System.Management.Automation.PSCredential]::new('posthog', $_)).GetNetworkCredential().Password }
& .\DataAnalysis\Download-MgrTelemetry.ps1
Remove-Item Env:POSTHOG_PERSONAL_API_KEY
```

若不设置环境变量，直接运行脚本也会使用隐藏输入提示。下载冒烟测试事件可使用：

```powershell
& .\DataAnalysis\Download-MgrTelemetry.ps1 -EventName mgr_telemetry_smoke_test
```

脚本输出 JSON，不输出或保存 API key。个人 API key 继承账号权限，应使用最小读取权限并像密码一样保管。

## 接口滥用与数据可信度

PostHog 的项目采集 Token 属于客户端采集凭据，发布模组后必然可以被读取；它不是秘密，也不能证明事件真的来自未经修改的游戏。只把过滤逻辑写在模组客户端中，可以挡住正常玩家的脏数据，但挡不住主动伪造 HTTP 请求的人。

当前方案按项目决定直接发送到 PostHog，不要求玩家进行 Steam 登录，也不引入 Cloudflare Worker。客户端已经执行字段白名单、一分钟防重复、事件 ID 和越界硬过滤，但嵌入模组 DLL 的规则都可以被反编译或修改。对公开客户端遥测，现实目标是减少意外脏数据，无法做到密码学意义上的绝对可信。

明文 Steam ID 的优势是能够跨安装、跨设备聚合同一账户，便于人工排查重复数据；代价是数据导出或泄露后可以与公开 Steam 资料直接关联。HMAC 方案同样能够跨安装稳定聚合，却无法从分析数据反推出 Steam ID，因而降低身份关联和误分享风险。本项目根据当前决定上传明文值，因此授权文本和本说明必须持续明确披露这一点。

## 扩展原则

后续如需增加强音等维度，应继续采用整局或逐场战斗的聚合计数，不上传逐帧 UI 状态。每次新增、删除或改变字段含义时：

1. 提升 `MgrRunMetricsBuilder.SchemaVersion`；
2. 更新本说明；
3. 确认字段属于明确白名单；
4. 重新检查是否能从新字段反推出玩家身份或无关存档状态。
