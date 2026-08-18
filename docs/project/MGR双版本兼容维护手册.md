# MGR 双版本兼容维护手册

## 一、维护目标

MGR 的发布包长期只支持两个游戏渠道：

1. 当前 Steam 正式版；
2. 最新 Steam 测试版。

不为已经过期的中间测试版继续分发 payload。旧游戏程序集仍可保存在本地 `.tools/` 归档中，用于回归、定位和重新构建，但不会让创意工坊包无限增长。

当前两个目标是正式版 `v0.107.1` 与测试版 `v0.111.0`。详细接口差异及逐调用点处理见 [`STS2_v0.107.1-v0.111接口变化与MGR兼容方案.md`](STS2_v0.107.1-v0.111接口变化与MGR兼容方案.md)。

## 二、最终发布结构

```text
MGRMod.dll                         # 稳定启动器，程序集身份 MGRMod.Loader
MGRMod.json                        # 模组清单，运行依赖只有 RitsuLib
MGRMod.pck                         # 两个游戏版本共享的资源
mgrmod-variants.manifest           # payload 版本、相对路径与 SHA-256
lib/<正式版>/MGRMod.dll            # 正式版原生 payload，程序集身份 MGRMod
lib/<最新测试版>/MGRMod.dll        # 测试版原生 payload，程序集身份 MGRMod
```

启动器从游戏根目录的 `release_info.json` 读取版本，在清单中选择不高于宿主版本的最高目标，校验 SHA-256 后把 payload 载入同一个 `AssemblyLoadContext`，关联回 MGR 的 Mod 记录并调用 `MGRMod.Entry.Initialize()`。

启动器和 payload 使用不同程序集身份，避免顶层 `MGRMod.dll` 与真实主体发生同名装载冲突。资源 PCK 由两个 payload 共享；只有游戏 API、RitsuLib ABI 或版本特有行为需要进入 DLL 分支。

## 三、为什么不依赖或内嵌 CVC

CVC 的核心价值是用 Mono.Cecil 在加载前扫描任意模组，把失效的类型、字段、方法、参数、返回值、Harmony 目标和 override 重定向到当前游戏 API。它适合处理大量来源不明的第三方模组，但 MGR 只维护两个已知目标。

MGR 吸收了“先加载稳定引导层，再处理版本主体”的思路，但没有复制 CVC 的通用重写器、缓存、报告、stand-in 类型和 Mono.Cecil 运行时依赖。针对两个明确版本分别原生编译具有以下优点：

- override 与 Hook 使用真实签名，不依赖运行时复活；
- 编译阶段即可发现大部分 ABI 错误；
- 发布包更小，错误栈仍指向 MGR 源码；
- 不受 CVC profile 更新节奏和加载顺序影响；
- CVC 或 Mono.Cecil 不进入 MGR 的依赖清单和程序集引用。

## 四、源码兼容方法

同一套源码按差异性质使用两种处理方式。

### 1. 条件编译

无法共享的虚方法、override、Hook 和强类型返回值在 `MGRMod.csproj` 中按 `Sts2CompatTarget` 定义版本常量。目前正式版使用 `STS2_V107`。

典型命中包括：

- 卡牌结算去向从旧 `PileType`/tuple 迁移到 `CardLocation`；
- 伤害 Hook 增加 `CardPlay?`；
- `AttackCommand.FromCard` 参数改变；
- v0.107.1 不存在的新 VFX 类型。

条件编译的目标是让每个 payload 的继承与 Hook 签名都直接匹配对应游戏程序集，不能为了减少几行代码而让旧版 override 静默失效。

### 2. 反射桥

普通方法的重载增减、可选字段和能够证明语义等价的调用集中放在 `Scripts/Compatibility/MgrCrossVersionApi.cs`。

当前桥接包括：

- `CardModel.CreateDupe` 与 owner；
- `CreateCloneForPlayer` 的旧版降级；
- `CreatureCmd.Damage` 参数变化；
- 跨玩家非阻塞抽牌；
- `SignalPlayerChoiceBegun` 参数变化；
- `CardSelectCmd.LocalSelector`。

反射桥必须明确检查可接受的签名并提供可解释的旧版降级；不能按方法名随意选择任意重载。

## 五、当前构建与归档

当前正式版原始引用保存在：

```text
<工作区根目录>/.tools/sts2_v107.1_original_refs
```

其中包含 `sts2.dll`、`GodotSharp.dll`、`0Harmony.dll`、`Steamworks.NET.dll`、`release_info.json` 和 `SHA256SUMS.txt`。归档版本为 `v0.107.1`、commit `59260271`，已经实际用于重新编译正式版 payload 和启动器。

当前测试版原始引用同样固定保存在：

```text
<工作区根目录>/.tools/sts2_v111.0_original_refs
```

归档版本为 `v0.111.0`、commit `41cef1ea`，文件集合与正式版归档一致。普通构建只读取这两个归档，因此当前 Steam 客户端切换分支或以后更新，不会让旧目标引用丢失，也不会造成“编译常量是 107、实际 DLL 却是 111”的混用。

现在直接执行一次普通构建即可生成和部署整套双版本模组：

```powershell
dotnet build MGRMod.csproj -c Release
```

主项目会依次完成共享 PCK、v0.111.0 payload、v0.107.1 payload、稳定启动器和带 SHA-256 的 manifest，最终目录就是“最终发布结构”所列六个文件。内层 payload 构建会传入 `BuildCrossVersionBundle=false`，避免再次触发外层打包。设置 `/p:CopyModOnBuild=false` 时仍会构建完整包，但输出改为 `.artifacts/MGRMod-cross-version/`，不会写入游戏模组目录。

只有排查单一版本编译问题时才显式关闭双版本打包，并传入相互匹配的版本和引用目录：

```powershell
dotnet build MGRMod.csproj -c Release `
  /p:BuildCrossVersionBundle=false `
  /p:Sts2CompatTarget=0.107.1 `
  /p:Sts2DataDir="<正式版引用目录>" `
  /p:RunPckExport=false `
  /p:CopyModOnBuild=false
```

`Sts2CompatTarget` 必须与 `Sts2DataDir` 属于同一个游戏版本。若 v0.111 DLL 配上 `0.107.1`，编译器会错误启用 `STS2_V107`，旧版伤害 Hook、卡牌结算去向等 override 会集中报 `CS0115`；这不是五处源码同时损坏，而是目标与引用不匹配。

底层工作仍由 `tools/Build-MgrVariantBundle.ps1` 完成；`MGRMod.csproj` 已把它接入默认构建。`MGRWorkshop/Prepare-Workshop.ps1` 也改为只调用一次普通双版本构建，然后检查发布目录和哈希，不再重复构建 payload。准备脚本只刷新本地发布目录，不会联系 Steam。

## 六、游戏更新后的滚动流程

### 情况 A：只更新测试版

保留正式版目标，使用新的测试版 DLL 尝试直接编译和实机启动。若 ABI 与原测试版兼容，可以只更新支持说明；若不兼容，则替换测试版 payload、版本常量、构建脚本参数和 manifest 条目。旧测试版 payload 从发布包移除。

### 情况 B：正式版更新

先在更新游戏前归档当前正式版原始引用。更新后归档新正式版引用，用它替换正式版 payload；最新测试版 payload 继续保留。同步提高 `MGRMod.json` 的 `min_game_version`，旧正式版不再承诺支持。

### 情况 C：正式版与测试版合流

如果两个渠道的程序集和行为一致，可以让两个渠道使用同一份原生 payload，或保留两次独立编译但校验结果相同。等新的测试分支出现后再恢复两个不同目标。

每次更新的实际顺序：

1. 保存原始 DLL、`release_info.json` 和 SHA-256；
2. 对新 `sts2.dll` 做接口比较并阅读官方迁移记录；
3. 直接编译现有源码，让编译器先暴露强类型差异；
4. 按“override/Hook 用条件编译，普通调用用反射桥”的边界修复；
5. 确认 RitsuLib 已提供两个渠道对应的运行时变体；
6. 生成只含正式版与最新测试版的发布包；
7. 在两个真实客户端分别完成回归后再发布。

## 七、验证基线

静态和构建检查至少包括：

- 两个 payload 分别针对原始目标程序集编译；
- 启动器的 `sts2` 成员引用在两端都可解析；
- 顶层程序集身份为 `MGRMod.Loader`，payload 为 `MGRMod`；
- manifest 哈希与两个 payload 完全一致；
- payload 不引用 CVC 或 Mono.Cecil；
- PCK 不包含 Loader、工具、构建缓存或本地引用归档；
- `docs/tools/Validate-MgrContent.ps1 -WarningsAsErrors` 通过。

真实客户端回归至少覆盖：

- 日志中 MGR Loader 与 RitsuLib 都选择正确变体；
- 角色选择、进入战斗、四类音符、演奏和卡牌结算去向；
- 伤害修正、消耗、复制、跨玩家抽牌和选择；
- 保存/读取、胜利/失败/放弃；
- 多人握手和至少一条 MGR 联机专属机制。

编译成功只证明 ABI 可绑定，不能替代资源、音频、协议和玩法语义的实机验证。

### v0.107.1 实机启动已发现的关联差异（2026-08-18）

- 版本选择本身正常：MGR Loader 与 RitsuLib 均选择并成功加载 v0.107.1 变体。
- v0.111.0 提供 `ModManager.AssociateAssemblyWithMod(string, Assembly)`，且 `Mod` 使用复数 `assemblies`；v0.107.1 没有该方法，并且只提供单数 `Mod.assembly`。
- 启动器原有回退逻辑只尝试写入复数 `assemblies`，所以在 v0.107.1 上虽已加载 payload，却没有将它关联到原版模组记录；RitsuLib 的自动注册可以成功，但原版 `ModelDb` 无法发现 MGR 角色模型，最终抛出 `ModelNotFoundException`。
- 第一次修复虽然在初始化期间成功写入单数 `assembly`，但 v0.107.1 的 `TryLoadMod` 会在初始化器返回后执行 `mod.assembly = assembly`，再次把字段覆盖为顶层 Loader；因此仅检查字段存在与可写仍不足以解决问题。
- 当前修复保留新版公开方法作为首选；旧版除初次写入外，还订阅同样存在于双端的 `OnModDetected`，在原版完成覆盖之后、`ModelDb` 扫描之前把单数 `assembly` 最终恢复为 payload。找不到 MGR 模组记录或关联字段时仍会立即抛出明确异常。
- 使用真实 v0.107.1 类型模拟原版覆盖与完成事件后，字段能从 `MGRMod.Loader` 恢复到 payload；两端启动器编译和完整双版本构建均已通过。仍需由正常 Steam 客户端完成 v0.107.1 启动确认，因为 `--headless` 会在模组初始化前退出。
- 因此，双版本启动器的程序集关联必须按 ABI 世代分支处理；“日志显示选中了正确 payload”不能单独作为启动兼容通过的依据。

## 八、本次工作的可复用结论

- 只为真实 ABI 世代创建 payload，不为每个小版本机械新增目录；发布时始终滚动保留两个玩家实际使用的渠道。
- 旧版引用必须在 Steam 更新前保存；公开化后的缓存只能用于接口编译，原始 DLL 才是可靠归档。
- 稳定启动器只依赖两端共同存在的 API，版本特有逻辑全部留在 payload。
- 版本号判断、payload 路径和哈希都由 manifest 驱动；原始创意工坊文件不需要在玩家机器上改写。
- 涉及 multiplayer 协议、资源格式、原生扩展或 Transpiler IL 布局时，不应假设普通 API 桥能够自动解决。
- 发布包结构、工坊依赖和说明必须一起更新；本地生成发布候选包不等于已经上传或发布。
