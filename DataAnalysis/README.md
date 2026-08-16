# MGR 数据分析目录

- `Download-MgrTelemetry.ps1`：只下载 `mgr_clean_run_metrics` 授权的 MGR 遥测事件；本地结果只保留国家、城市和 MGR 分析负载，不保留 IP、经纬度、运行环境字段或 PostHog 空列。
- `Test-MgrTelemetryData.ps1`：校验下载记录的结构版本、音符总数、伤害分类守恒、层数连续性、首层初始化回复和卡牌获得层数；默认检查 `Data/` 中最新文件。
- `Data/`：本地下载结果，可能包含明文 Steam ID；被 Git 与 Godot 导出排除。

本目录中的脚本与数据不参与游戏运行，也不会进入发布用 PCK。
