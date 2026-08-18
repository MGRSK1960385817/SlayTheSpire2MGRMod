# MGRMod

MGR 人物模组的《杀戮尖塔 2》重制工程。目前处于发布前收尾阶段。

## 当前内容

- 80 张单人奖励池卡牌：20 张白卡、35 张蓝卡、25 张金卡。
- 4 张基础牌、2 张先古牌和 3 张衍生牌。
- 11 件职业遗物，包含初始遗物与先古替换遗物。
- 核心机制：音符槽、和弦、演奏、强音、起音/尾音、星空、幽灵与万象音符。
- 人物动画、战斗与选人特效、能量框、营火/商店表现、先古对话及中英本地化。

内容名称、启用状态与效果摘要以
[`docs/tools/MGR_content_registry.json`](docs/tools/MGR_content_registry.json) 为人类维护入口；实际游戏文本位于
`MGRMod/localization/<语言>/`。

## 环境与依赖

- Godot 4.5.1 Mono
- .NET 9 SDK
- 《杀戮尖塔 2》版本 `0.107.1` 或 `0.111.0`
- RitsuLib `0.5.13` 或更高版本（模组 ID：`STS2-RitsuLib`）

MGR 不依赖 CrossVersionCompat。发布包顶层的 `MGRMod.dll` 是稳定启动器，会按游戏版本选择 `lib/0.107.1/MGRMod.dll` 或 `lib/0.111.0/MGRMod.dll`；两个 payload 都针对对应游戏 API 与 RitsuLib 变体原生编译。日常滚动升级规则见 [`MGR双版本兼容维护手册.md`](docs/project/MGR双版本兼容维护手册.md)，逐版本接口变化、实现细节和 CVC 对比见 [`STS2_v0.107.1-v0.111接口变化与MGR兼容方案.md`](docs/project/STS2_v0.107.1-v0.111接口变化与MGR兼容方案.md)。

复制 `local.props.template` 为 `local.props`，填写本机游戏目录与 Godot 可执行文件路径。普通构建固定使用工作区 `.tools/` 中保存的 v0.107.1 与 v0.111.0 原始引用，不再要求当前 Steam 客户端版本与 `Sts2CompatTarget` 一致；该属性只用于显式关闭双版本打包后的单 payload 诊断。`local.props` 仅供本机使用，不应提交。

## 日常检查

普通构建会导出共享 PCK，同时编译 v0.107.1、v0.111.0 和稳定启动器，并把完整六文件包部署到本机游戏模组目录：

```powershell
dotnet build -c Release
```

需要生成同样的双版本包但不部署到游戏目录时：

```powershell
dotnet build -c Release /p:CopyModOnBuild=false
```

产物位于 `.artifacts/MGRMod-cross-version/`。仅诊断某一个 payload、明确不需要双版本包和 PCK 时，必须显式关闭外层构建：

```powershell
dotnet build -c Release `
  /p:BuildCrossVersionBundle=false `
  /p:Sts2CompatTarget=0.111.0 `
  /p:Sts2DataDir="<对应版本引用目录>" `
  /p:RunPckExport=false `
  /p:CopyModOnBuild=false
```

检查登记表、注册代码、图片、Godot `.import` UID 和中英本地化是否同步：

```powershell
pwsh -NoProfile -File docs/tools/Validate-MgrContent.ps1
```

把名称差异等警告也视为失败：

```powershell
pwsh -NoProfile -File docs/tools/Validate-MgrContent.ps1 -WarningsAsErrors
```

校验器会固定核对奖励池基线 `20/35/25`，但不能替代进游戏后的机制、动画与存档回归测试。

## 主要目录

- `Scripts/Cards`：卡牌与卡牌基类。
- `Scripts/Powers`：能力与持续战斗效果。
- `Scripts/Relics`：职业遗物。
- `Scripts/Mechanics`：音符、和弦、演奏、UI、动画及规则服务。
- `Scripts/Characters`：人物模型、场景与人物周边特效。
- `MGRMod`：Godot 场景、图片、音频与本地化资源。
- `docs`：按项目、设计、本地化、记录和工具分类的维护文档；入口见 [`docs/总览目录.md`](docs/总览目录.md)。
- `DataAnalysis`：PostHog 下载脚本和被 Git 忽略的本地数据。

完整结构与发布前检查见 [`docs/project/MGR模组架构与开发检查.md`](docs/project/MGR模组架构与开发检查.md)。

## 开发约定

- 普通构建会自动刷新本地游戏目录中的完整双版本包；Git 同步和创意工坊上传仍由作者手动执行。
- 卡牌或遗物改名时，应同步检查类名、文件名、图片、注册主键、本地化键与内容登记表。
- 塔一旧模组、狐狸、魔理沙、工匠、忍者及原版资源参考均位于仓库外，只作为实现参考，不进入导出包。
