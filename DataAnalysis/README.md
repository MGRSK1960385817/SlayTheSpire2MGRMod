# MGR 数据分析目录

- `Download-MgrTelemetry.ps1`：只下载 `mgr_clean_run_metrics` 授权的 MGR 遥测事件；使用 `(timestamp, uuid)` 游标分页并逐条写入 `.jsonl.gz`，另生成不含身份明细的 `.manifest.json`。默认每页 1000 条并下载全部，也可使用 `-SinceUtc`、`-UntilUtc` 或 `-MaxEvents` 限定范围。本地事件只保留国家、城市和 MGR 分析负载，不保留 IP、经纬度、运行环境字段或 PostHog 空列。
- `Test-MgrTelemetryData.ps1`：流式校验 `.jsonl.gz`/`.jsonl` 下载记录，也兼容旧 `.json` 快照；检查结构版本、音符总数、伤害分类守恒、层数连续性、首层初始化回复和卡牌获得层数。默认跳过低于 `MinimumSchemaVersion` 的历史记录并只显示异常；使用 `-FailOnLegacySchema` 可把旧结构视为错误，`-ShowAll` 可列出全部受检记录。
- `Analyze-MgrCardMetrics.py`：使用最新压缩快照生成隐私安全的卡牌胜率报告、战斗奖励选取率报告和完整统计 CSV；同时列出最高/最低排名及同稀有度前列/末位，并自动排除旧结构、不完整记录、非战斗选择、基础/衍生/先古牌和联机专属牌。
- `Analyze-MgrRelicMetrics.py`：使用最新压缩快照生成MGR遗物获得后胜率、A0/A10+拆分及同稀有度排名；比较池只包含能从本地源码确认稀有度的MGR与原版遗物，其他模组遗物不会被错误归类。
- `Data/`：本地下载结果，可能包含明文 Steam ID；被 Git 与 Godot 导出排除。
- `Reports/`：不含玩家身份的聚合分析结果，可随新快照重新生成。

本目录中的脚本与数据不参与游戏运行，也不会进入发布用 PCK。

## 常用命令

全量下载当前全部正式对局（默认每页 1000 条）：

```powershell
& .\DataAnalysis\Download-MgrTelemetry.ps1
```

下载指定时间之后的对局，或只取少量记录进行冒烟检查：

```powershell
& .\DataAnalysis\Download-MgrTelemetry.ps1 -SinceUtc '2026-08-01T00:00:00Z'
& .\DataAnalysis\Download-MgrTelemetry.ps1 -PageSize 100 -MaxEvents 250
```

校验最新压缩归档；默认只输出异常，`-ShowAll` 会列出每一局：

```powershell
& .\DataAnalysis\Test-MgrTelemetryData.ps1
& .\DataAnalysis\Test-MgrTelemetryData.ps1 -ShowAll
```

生成卡牌胜率和选取率报告：

```powershell
python .\DataAnalysis\Analyze-MgrCardMetrics.py
```

生成遗物胜率与同稀有度排名：

```powershell
python .\DataAnalysis\Analyze-MgrRelicMetrics.py
```
