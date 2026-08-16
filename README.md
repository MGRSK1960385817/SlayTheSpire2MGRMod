# MGRMod

MGR 人物模组的《杀戮尖塔 2》重制工程。目前处于发布前收尾阶段。

## 当前内容

- 80 张单人奖励池卡牌：20 张白卡、35 张蓝卡、25 张金卡。
- 4 张基础牌、2 张先古牌和 3 张衍生牌。
- 11 件职业遗物，包含初始遗物与先古替换遗物。
- 核心机制：音符槽、和弦、演奏、强音、起音/尾音、星空、幽灵与万象音符。
- 人物动画、战斗与选人特效、能量框、营火/商店表现、先古对话及中英本地化。

内容名称、启用状态与效果摘要以
[`docs/MGR_content_registry.json`](docs/MGR_content_registry.json) 为人类维护入口；实际游戏文本位于
`MGRMod/localization/<语言>/`。

## 环境与依赖

- Godot 4.5.1 Mono
- .NET 9 SDK
- 《杀戮尖塔 2》最低版本 `0.110.0`
- RitsuLib `0.5.1` 或更高版本（模组 ID：`STS2-RitsuLib`）

复制 `local.props.template` 为 `local.props`，然后填写本机游戏目录与 Godot 可执行文件路径。`local.props` 仅供本机使用，不应提交。

## 日常检查

只进行 C# 编译，不导出 PCK，也不复制到游戏目录：

```powershell
dotnet build /p:RunPckExport=false /p:CopyModOnBuild=false
```

检查登记表、注册代码、图片、Godot `.import` UID 和中英本地化是否同步：

```powershell
pwsh -NoProfile -File docs/Validate-MgrContent.ps1
```

把名称差异等警告也视为失败：

```powershell
pwsh -NoProfile -File docs/Validate-MgrContent.ps1 -WarningsAsErrors
```

校验器会固定核对奖励池基线 `20/35/25`，但不能替代进游戏后的机制、动画与存档回归测试。

## 主要目录

- `Scripts/Cards`：卡牌与卡牌基类。
- `Scripts/Powers`：能力与持续战斗效果。
- `Scripts/Relics`：职业遗物。
- `Scripts/Mechanics`：音符、和弦、演奏、UI、动画及规则服务。
- `Scripts/Characters`：人物模型、场景与人物周边特效。
- `MGRMod`：Godot 场景、图片、音频与本地化资源。
- `docs`：内容登记、卡池设计、架构与检查、特效登记、文本规范及只读校验器。
- `DataAnalysis`：PostHog 下载脚本和被 Git 忽略的本地数据。

完整结构与发布前检查见 [`docs/MGR模组架构与开发检查.md`](docs/MGR模组架构与开发检查.md)。

## 开发约定

- 日常修改完成后只做必要的编译与静态检查；Git 同步和游戏目录部署由作者手动执行。
- 卡牌或遗物改名时，应同步检查类名、文件名、图片、注册主键、本地化键与内容登记表。
- 塔一旧模组、狐狸、魔理沙、工匠、忍者及原版资源参考均位于仓库外，只作为实现参考，不进入导出包。
