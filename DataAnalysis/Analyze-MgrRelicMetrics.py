#!/usr/bin/env python3
"""Analyze MGR relic win rates against base-game relics of the same rarity."""

from __future__ import annotations

import argparse
import csv
import gzip
import json
import math
import re
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path
from statistics import median
from typing import Any, Iterable


RANKED_RARITIES = ("Common", "Uncommon", "Rare", "Shop")
RARITY_LABELS = {
    "Starter": "初始",
    "Common": "普通",
    "Uncommon": "罕见",
    "Rare": "稀有",
    "Shop": "商店",
}
HISTORICAL_MGR_ALIASES = {
    "MGR_MOD_RELIC_METRONOME": "MGR_MOD_RELIC_CLICK_TRACK",
}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, help="Downloaded .jsonl.gz snapshot")
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--minimum-schema", type=int, default=7)
    parser.add_argument("--minimum-sample", type=int, default=20)
    return parser.parse_args()


def latest_snapshot(data_dir: Path) -> Path:
    candidates = sorted(
        data_dir.glob("mgr_run_completed_*.jsonl.gz"),
        key=lambda path: path.stat().st_mtime,
        reverse=True,
    )
    if not candidates:
        raise FileNotFoundError(f"No telemetry snapshot found in {data_dir}")
    return candidates[0]


def pascal_to_upper_snake(value: str) -> str:
    value = re.sub(r"([a-z0-9])([A-Z])", r"\1_\2", value)
    value = re.sub(r"([A-Z]+)([A-Z][a-z])", r"\1_\2", value)
    return value.upper()


def read_snapshot(path: Path) -> Iterable[dict[str, Any]]:
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError as error:
                raise ValueError(f"Invalid JSON on line {line_number} of {path}") from error


def validate_payload(payload: dict[str, Any]) -> list[str]:
    issues: list[str] = []
    mechanics = payload.get("mgr_mechanics", {})
    note_kinds = mechanics.get("notes_by_kind", {})
    if sum(int(value) for value in note_kinds.values()) != int(
        mechanics.get("notes_generated", 0)
    ):
        issues.append("note_sum")

    damage_sources = mechanics.get("damage_by_source", {})
    damage_sum = sum(
        int(damage_sources.get(name, 0))
        for name in ("card", "note", "other", "unclassified")
    )
    if damage_sum != int(payload.get("final_player", {}).get("damage_dealt", 0)):
        issues.append("damage_sum")

    floors = payload.get("floors", [])
    for index, floor in enumerate(floors, 1):
        if int(floor.get("floor", 0)) != index:
            issues.append("floor_sequence")
            break
        if any(card.get("floor_added") is None for card in floor.get("cards_gained", [])):
            issues.append("gained_card_floor")
            break
    if floors and int(floors[0].get("hp_healed", 0)) != 0:
        issues.append("initial_setup_healing")
    if floors and int(floors[-1].get("current_hp", 0)) != int(
        payload.get("final_player", {}).get("current_hp", 0)
    ):
        issues.append("final_hp")
    return issues


def wilson_interval(
    successes: int, trials: int, z: float = 1.959963984540054
) -> tuple[float, float]:
    if trials <= 0:
        return 0.0, 0.0
    proportion = successes / trials
    denominator = 1 + z * z / trials
    center = (proportion + z * z / (2 * trials)) / denominator
    margin = z * math.sqrt(
        proportion * (1 - proportion) / trials + z * z / (4 * trials * trials)
    ) / denominator
    return max(0.0, center - margin), min(1.0, center + margin)


def percent(value: float | None, digits: int = 1) -> str:
    return "—" if value is None else f"{value * 100:.{digits}f}%"


def signed_percent(value: float | None, digits: int = 1) -> str:
    return "—" if value is None else f"{value * 100:+.{digits}f}pp"


def markdown_table(headers: list[str], rows: list[list[Any]]) -> str:
    def escape(value: Any) -> str:
        return str(value).replace("|", "\\|").replace("\n", " ")

    output = [
        "| " + " | ".join(escape(item) for item in headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    output.extend("| " + " | ".join(escape(item) for item in row) + " |" for row in rows)
    return "\n".join(output)


def load_mgr_catalog(repo_root: Path) -> dict[str, dict[str, Any]]:
    registry = json.loads(
        (repo_root / "docs/tools/MGR_content_registry.json").read_text(encoding="utf-8-sig")
    )
    localization = json.loads(
        (repo_root / "MGRMod/localization/zhs/relics.json").read_text(encoding="utf-8-sig")
    )
    source_dir = repo_root / "Scripts/Relics"
    catalog: dict[str, dict[str, Any]] = {}
    for relic in registry["relics"]:
        if relic.get("status") != 1:
            continue
        code_name = str(relic["codeName"])
        relic_id = f"MGR_MOD_RELIC_{pascal_to_upper_snake(code_name)}"
        source_path = source_dir / f"{code_name}.cs"
        rarity = str(relic.get("rarity", ""))
        if source_path.exists():
            source = source_path.read_text(encoding="utf-8-sig")
            match = re.search(
                r"public\s+override\s+RelicRarity\s+Rarity\s*=>\s*RelicRarity\.(\w+)",
                source,
            )
            if match:
                rarity = match.group(1)
        catalog[relic_id] = {
            "id": relic_id,
            "name": localization.get(f"{relic_id}.title", relic["name"]),
            "rarity": rarity,
            "source": "MGR",
        }
    return catalog


def load_base_catalog(workspace_root: Path) -> dict[str, dict[str, Any]]:
    reference_root = workspace_root / "塔2原版资源参考"
    source_dir = reference_root / "src/Core/Models/Relics"
    localization_path = reference_root / "localization/zhs/relics.json"
    if not source_dir.exists() or not localization_path.exists():
        raise FileNotFoundError("Base-game source/localization reference is unavailable")
    localization = json.loads(localization_path.read_text(encoding="utf-8-sig"))
    catalog: dict[str, dict[str, Any]] = {}
    for source_path in source_dir.glob("*.cs"):
        source = source_path.read_text(encoding="utf-8-sig")
        match = re.search(
            r"public\s+override\s+RelicRarity\s+Rarity\s*=>\s*RelicRarity\.(\w+)",
            source,
        )
        if not match:
            continue
        relic_id = pascal_to_upper_snake(source_path.stem)
        catalog[relic_id] = {
            "id": relic_id,
            "name": localization.get(f"{relic_id}.title", source_path.stem),
            "rarity": match.group(1),
            "source": "原版",
        }
    return catalog


def main() -> None:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    workspace_root = repo_root.parent
    input_path = args.input.resolve() if args.input else latest_snapshot(repo_root / "DataAnalysis/Data")
    output_dir = (args.output_dir or repo_root / "DataAnalysis/Reports").resolve()
    output_dir.mkdir(parents=True, exist_ok=True)

    catalog = load_base_catalog(workspace_root)
    mgr_catalog = load_mgr_catalog(repo_root)
    catalog.update(mgr_catalog)

    runs: dict[str, dict[str, Any]] = {}
    skipped = Counter()
    for envelope in read_snapshot(input_path):
        payload = envelope["payload"]
        if int(payload.get("schema_version", 0)) < args.minimum_schema:
            skipped["legacy"] += 1
            continue
        if not bool(payload.get("mgr_mechanics", {}).get("tracking_complete", False)):
            skipped["incomplete"] += 1
            continue
        if validate_payload(payload):
            skipped["invalid"] += 1
            continue
        event_id = str(payload.get("event_id", ""))
        if not event_id:
            skipped["missing_event_id"] += 1
            continue
        if event_id in runs:
            skipped["duplicate"] += 1
            continue
        runs[event_id] = {
            "victory": bool(payload.get("victory", False)),
            "ascension": int(payload.get("ascension", 0)),
            "payload": payload,
        }

    acquired_runs: dict[str, set[str]] = defaultdict(set)
    acquisition_floors: dict[str, dict[str, int]] = defaultdict(dict)
    for event_id, run in runs.items():
        payload = run["payload"]
        for relic in payload.get("final_player", {}).get("relics", []):
            relic_id = HISTORICAL_MGR_ALIASES.get(str(relic.get("id", "")), str(relic.get("id", "")))
            if not relic_id:
                continue
            acquired_runs[relic_id].add(event_id)
            floor_added = int(relic.get("floor_added", 0) or 0)
            previous = acquisition_floors[relic_id].get(event_id)
            if floor_added > 0 and (previous is None or floor_added < previous):
                acquisition_floors[relic_id][event_id] = floor_added
        for floor in payload.get("floors", []):
            floor_number = int(floor.get("floor", 0))
            for choice in floor.get("relic_choices", []):
                if not bool(choice.get("picked", False)):
                    continue
                relic_id = HISTORICAL_MGR_ALIASES.get(str(choice.get("id", "")), str(choice.get("id", "")))
                if not relic_id:
                    continue
                acquired_runs[relic_id].add(event_id)
                previous = acquisition_floors[relic_id].get(event_id)
                if floor_number > 0 and (previous is None or floor_number < previous):
                    acquisition_floors[relic_id][event_id] = floor_number

    total_runs = len(runs)
    total_wins = sum(int(run["victory"]) for run in runs.values())
    baseline = total_wins / total_runs
    metrics: list[dict[str, Any]] = []
    for relic_id, relic in catalog.items():
        ids = acquired_runs.get(relic_id, set())
        if not ids:
            continue
        wins = sum(int(runs[event_id]["victory"]) for event_id in ids)
        rate = wins / len(ids)
        ci_low, ci_high = wilson_interval(wins, len(ids))
        a0_ids = {event_id for event_id in ids if runs[event_id]["ascension"] == 0}
        a10_ids = {event_id for event_id in ids if runs[event_id]["ascension"] >= 10}
        metrics.append(
            {
                **relic,
                "acquired_runs": len(ids),
                "wins": wins,
                "win_rate": rate,
                "ci_low": ci_low,
                "ci_high": ci_high,
                "baseline_delta": rate - baseline,
                "median_floor": median(acquisition_floors[relic_id].values())
                if acquisition_floors[relic_id]
                else None,
                "a0_runs": len(a0_ids),
                "a0_wins": sum(int(runs[event_id]["victory"]) for event_id in a0_ids),
                "a0_win_rate": (
                    sum(int(runs[event_id]["victory"]) for event_id in a0_ids) / len(a0_ids)
                    if a0_ids
                    else None
                ),
                "a10_runs": len(a10_ids),
                "a10_wins": sum(int(runs[event_id]["victory"]) for event_id in a10_ids),
                "a10_win_rate": (
                    sum(int(runs[event_id]["victory"]) for event_id in a10_ids) / len(a10_ids)
                    if a10_ids
                    else None
                ),
            }
        )

    eligible_by_rarity: dict[str, list[dict[str, Any]]] = {}
    for rarity in RANKED_RARITIES:
        eligible = [
            item
            for item in metrics
            if item["rarity"] == rarity and item["acquired_runs"] >= args.minimum_sample
        ]
        eligible.sort(key=lambda item: (item["win_rate"], item["acquired_runs"]), reverse=True)
        eligible_by_rarity[rarity] = eligible
        for rank, item in enumerate(eligible, 1):
            item["rarity_rank"] = rank
            item["rarity_pool"] = len(eligible)
        for prefix in ("a0", "a10"):
            subgroup = [
                item
                for item in metrics
                if item["rarity"] == rarity
                and item[f"{prefix}_runs"] >= args.minimum_sample
                and item[f"{prefix}_win_rate"] is not None
            ]
            subgroup.sort(
                key=lambda item: (item[f"{prefix}_win_rate"], item[f"{prefix}_runs"]),
                reverse=True,
            )
            for rank, item in enumerate(subgroup, 1):
                item[f"{prefix}_rarity_rank"] = rank
                item[f"{prefix}_rarity_pool"] = len(subgroup)

    mgr_metrics = [item for item in metrics if item["source"] == "MGR"]
    mgr_metrics.sort(
        key=lambda item: (
            list(RARITY_LABELS).index(item["rarity"]) if item["rarity"] in RARITY_LABELS else 99,
            -item["win_rate"],
        )
    )

    ranked_rows: list[list[Any]] = []
    starter_rows: list[list[Any]] = []
    for item in mgr_metrics:
        row = [
            item["name"],
            RARITY_LABELS.get(item["rarity"], item["rarity"]),
            item["acquired_runs"],
            item["wins"],
            percent(item["win_rate"]),
            f"{percent(item['ci_low'])}–{percent(item['ci_high'])}",
            signed_percent(item["baseline_delta"]),
            item["median_floor"] if item["median_floor"] is not None else "—",
            f"{item.get('rarity_rank', '—')} / {item.get('rarity_pool', '—')}",
            f"{percent(item['a0_win_rate'])} (n={item['a0_runs']}; {item.get('a0_rarity_rank', '—')}/{item.get('a0_rarity_pool', '—')})",
            f"{percent(item['a10_win_rate'])} (n={item['a10_runs']}; {item.get('a10_rarity_rank', '—')}/{item.get('a10_rarity_pool', '—')})",
        ]
        if item["rarity"] == "Starter":
            starter_rows.append(row)
        else:
            ranked_rows.append(row)

    rarity_context_sections: list[str] = []
    for rarity in RANKED_RARITIES:
        ranked = eligible_by_rarity[rarity]
        mgr_in_rarity = {item["id"] for item in mgr_metrics if item["rarity"] == rarity}
        if not mgr_in_rarity:
            continue
        rows = []
        for rank, item in enumerate(ranked, 1):
            if item["id"] in mgr_in_rarity or rank <= 3 or rank > len(ranked) - 3:
                rows.append(
                    [
                        rank,
                        item["name"],
                        item["source"],
                        item["acquired_runs"],
                        percent(item["win_rate"]),
                        item["median_floor"] if item["median_floor"] is not None else "—",
                    ]
                )
        rarity_context_sections.append(
            f"### {RARITY_LABELS[rarity]}遗物\n\n"
            + markdown_table(["名次", "遗物", "来源", "获得对局", "胜率", "中位获得层"], rows)
        )

    generated_at = datetime.now(timezone.utc).isoformat()
    report = f"""# MGR 遗物胜率与同稀有度排名

> 生成时间：{generated_at}  
> 数据快照：`{input_path.name}`

## 结论摘要

- 有效样本为 **{total_runs} 局**，胜利 **{total_wins} 局**，总体胜率 **{percent(baseline)}**；本次只使用现存快照，没有访问PostHog。
- 主口径为“本局曾获得该遗物”的唯一对局胜率：以最终遗物列表与逐层`relic_choices.picked=true`取并集，能够保留后来被替换或移除的遗物；同一局同名遗物只计一次。
- 同稀有度排名只比较样本不少于 **{args.minimum_sample} 局**的MGR与原版遗物。无法从本地源码可靠确认稀有度的其他模组遗物不进入比较池，避免错误分类。
- 原始遗物胜率具有明显幸存者偏差：越晚取得的遗物，持有者越可能已经活到后期。中位获得层和A0/A10+拆分用于辅助判断，排名不能直接证明遗物强弱。

## MGR奖励遗物

{markdown_table(
    ["遗物", "稀有度", "获得对局", "胜利", "胜率", "95%区间", "相对全局", "中位获得层", "同稀有度排名", "A0", "A10+"],
    ranked_rows,
)}

## 初始遗物与升级版

初始遗物不是随机奖励，无法与普通、罕见或稀有遗物公平排名；“与我同行”又只出现在完成初始遗物升级的对局中，天然带有更强的进度筛选。

{markdown_table(
    ["遗物", "稀有度", "获得对局", "胜利", "胜率", "95%区间", "相对全局", "中位获得层", "同稀有度排名", "A0", "A10+"],
    starter_rows,
)}

## 同稀有度位置上下文

每个分组列出前三、后三及全部MGR遗物；名次按原始获得后胜率降序，再以样本量降序打破相同胜率。

{chr(10).join(rarity_context_sections)}

## 解读边界

1. 遗物并非随机分配实验。玩家到达的层数、事件路线、Boss奖励和遗物协同都会影响结果。
2. 商店遗物尤其受购买意愿与经济状况影响；黑金唱片的排名同时反映“玩家愿意买它的卡组”与遗物效果。
3. 当前快照混合了多个MGR版本。历史ID`MGR_MOD_RELIC_METRONOME`已经合并到当前“咔哒咔哒”（`ClickTrack`），但数值调整前后的样本仍混在一起。
4. A0/A10+只是方向性拆分；样本较小时应优先看95%区间，而不是一两个名次差。
"""

    report_path = output_dir / "MGR遗物胜率分析.md"
    csv_path = output_dir / "MGR遗物统计明细.csv"
    report_path.write_text(report, encoding="utf-8")
    fields = [
        "id", "name", "source", "rarity", "acquired_runs", "wins", "win_rate",
        "ci_low", "ci_high", "baseline_delta", "median_floor", "rarity_rank",
        "rarity_pool", "a0_runs", "a0_wins", "a0_win_rate", "a0_rarity_rank",
        "a0_rarity_pool", "a10_runs", "a10_wins", "a10_win_rate",
        "a10_rarity_rank", "a10_rarity_pool",
    ]
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(
            sorted(metrics, key=lambda item: (item["rarity"], -item["win_rate"], item["name"]))
        )

    print(json.dumps({
        "input": str(input_path),
        "valid_runs": total_runs,
        "wins": total_wins,
        "baseline_win_rate": baseline,
        "skipped": dict(skipped),
        "mapped_base_relics": sum(1 for item in metrics if item["source"] == "原版"),
        "mapped_mgr_relics": len(mgr_metrics),
        "report": str(report_path),
        "detail_csv": str(csv_path),
    }, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
