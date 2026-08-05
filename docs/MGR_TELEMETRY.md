# MGR 对局数据统计接入说明

## 采用的方案

MGR 使用 RitsuLib 0.5.1 的遥测框架，不自行维护 HTTP 队列。RitsuLib 负责：

- 在主菜单显示独立的 MGR 数据授权项；
- 只有玩家明确同意后才采集和发送；
- 从原版的对局历史模型中生成数据；
- 本地排队、批量发送、重试与网络错误隔离；
- 上传失败时不阻塞或中断游戏。

入口位于 `Scripts/Telemetry/MgrTelemetry.cs`，由 `Scripts/Entry.cs` 在模组初始化时登记。

## 当前采集范围

目前只申请 `RunHistory`，并且只接收满足下列条件的记录：

1. 对局中至少有一名玩家使用 MGR；
2. 对局已经结束；
3. 对局不是玩家中途放弃。

RitsuLib 的运行历史包含原版已经记录的路线、胜负、卡牌选择、最终牌组、遗物、遭遇与商店等对局信息。MGR 只额外附加：

- `schema_version`：MGR 附加数据结构版本；
- `mod_version`：产生记录时的 MGR 程序集版本。

没有申请诊断日志、模组清单或额外的设备信息。MGR 的附加字段使用 `PrivateToApplicant`，不会被其他遥测申请方搭便车读取。

## 云端尚未启用的原因

忍者模组上传到作者自己的 Cloudflare Worker/PostHog 转发地址。该服务器不属于 MGR，不能复用。

当前 `MgrTelemetry.cs` 中的 `PostHogHost` 与 `PostHogProjectApiKey` 为空，因此会使用 `DisabledTelemetryAdapter`：授权、筛选和数据结构均已接好，但不会把数据发送到互联网。

正式启用时，在同一文件中填写：

```csharp
private const string PostHogHost = "https://你的固定转发域名";
private const string PostHogProjectApiKey = "仅限采集用途的项目键或代理标识";
```

推荐像忍者模组一样使用自有的受限转发服务。不要把 PostHog 个人密钥、管理密钥或其他具有读取/管理权限的秘密写入客户端模组；客户端里只能放本来就允许公开的采集键，或权限受限的代理标识。

如果后端不是 PostHog，可以把 `CreateAdapter()` 改为 RitsuLib 的 `HttpJsonTelemetryAdapter`，其余授权、筛选和附加数据代码无需改变。

## 与原版上传机制的关系

原版也有运行统计上传，但它只向 MegaCrit 的固定服务上传符合条件的原版数据。检测到模组环境后，原版不会把模组对局直接上传到 MegaCrit，而是把处理机会交给模组遥测钩子。因此 MGR 不应伪装成原版上传，也不应调用 MegaCrit 的私有端点。

## 后续扩展原则

如需分析音符、和弦或演奏机制，应给 `MgrRunContextProvider` 增加聚合后的战斗统计，而不是上传逐帧 UI 状态。每次扩展数据字段时提升 `schema_version`，并同步更新本说明和授权描述。
