# SlayTheSpire2MGRMod

MGR 人物模组的《杀戮尖塔 2》重制工程。

## 当前状态

- 原生 Godot 4.5.1 Mono 工程，使用 `Godot.NET.Sdk/4.5.1` 和 `net9.0`。
- 当前开发目标游戏版本 `0.110.0`，依赖 RitsuLib `0.5.1` 或更高版本。
- 已建立角色、卡牌池、遗物池、药水池和 RitsuLib 自动注册入口。
- 已建立人物场景、角色选择背景、能量面板、商店/营火场景及中英本地化。
- 已迁移 4 张打击、4 张防御、窥视、安眠曲和迷你麦克风作为最小人物骨架。
- 新的乐句规则目前只存在于独立领域模型中，尚未连接战斗 UI；详见 `docs/DESIGN.md`。

## 构建

1. 安装 Godot 4.5.1 Mono 和 .NET 9 或以上 SDK。
2. 复制 `local.props.template` 为 `local.props`，填写游戏与 Godot 路径。
3. 日常开发验证执行下方的“仅编译检查”命令。

日常开发约定（适用于 Codex）：只进行 C# 编译检查；**不主动推送 GitHub，不主动部署或复制文件到游戏目录，也无需每次重复说明该约定。** Git 同步与游戏部署均由作者手动决定和执行。

仅验证 C# 编译：

```powershell
dotnet build /p:RunPckExport=false /p:CopyModOnBuild=false
```

工作区中的塔一旧模组、狐狸模组和塔二原版源码参考已移到仓库外，与本项目同级存放，不会进入 Git 或 Godot 导出包。
