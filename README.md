# SlayTheSpire2MGRMod

MGR 人物模组的《杀戮尖塔 2》重制工程。

## 当前状态

- 原生 Godot 4.5.1 Mono 工程，使用 `Godot.NET.Sdk/4.5.1` 和 `net9.0`。
- 目标游戏版本 `0.108.0`，依赖 RitsuLib `0.4.57` 或更高版本。
- 已建立角色、卡牌池、遗物池、药水池和 RitsuLib 自动注册入口。
- 已建立人物场景、角色选择背景、能量面板、商店/营火场景及中英本地化。
- 已迁移 4 张打击、4 张防御、窥视、安眠曲和迷你麦克风作为最小人物骨架。
- 新的乐句规则目前只存在于独立领域模型中，尚未连接战斗 UI；详见 `docs/DESIGN.md`。

## 构建

1. 安装 Godot 4.5.1 Mono 和 .NET 9 或以上 SDK。
2. 复制 `local.props.template` 为 `local.props`，填写游戏与 Godot 路径。
3. 执行 `dotnet build`。构建会把 DLL、清单和 PCK 部署到游戏的 `mods/SlayTheSpire2MGRMod/`。

如只验证 C# 编译，可执行：

```powershell
dotnet build /p:RunPckExport=false /p:CopyModOnBuild=false
```

`塔1老版本/` 仅用于设计参考，已被 Git 忽略，不会进入塔二仓库。
