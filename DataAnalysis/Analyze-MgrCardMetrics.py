#!/usr/bin/env python3
"""Generate privacy-safe MGR card win-rate and pick-rate reports."""

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


COMBAT_ROOM_TYPES = {"Monster", "Elite", "Boss"}
NORMAL_RARITIES = {"Common", "Uncommon", "Rare"}
RARITY_LABELS = {"Common": "白卡", "Uncommon": "蓝卡", "Rare": "金卡"}
TYPE_LABELS = {"Attack": "攻击", "Skill": "技能", "Power": "能力"}


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--input", type=Path, help="Downloaded .jsonl.gz snapshot")
    parser.add_argument("--output-dir", type=Path)
    parser.add_argument("--minimum-schema", type=int, default=7)
    parser.add_argument("--minimum-win-sample", type=int, default=20)
    parser.add_argument("--minimum-offers", type=int, default=30)
    parser.add_argument("--top", type=int, default=15)
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


def load_card_catalog(repo_root: Path) -> dict[str, dict[str, Any]]:
    registry = json.loads(
        (repo_root / "docs/tools/MGR_content_registry.json").read_text(encoding="utf-8-sig")
    )
    localization = json.loads(
        (repo_root / "MGRMod/localization/zhs/cards.json").read_text(encoding="utf-8-sig")
    )
    localized_titles = {
        key.removesuffix(".title"): value
        for key, value in localization.items()
        if key.endswith(".title")
    }
    ids_by_title: dict[str, list[str]] = defaultdict(list)
    for card_id, title in localized_titles.items():
        ids_by_title[str(title)].append(card_id)

    catalog: dict[str, dict[str, Any]] = {}
    for card in registry["cards"]:
        if card.get("status") != 1:
            continue
        if card.get("multiplayerOnly") == 1:
            continue
        if card.get("rarity") not in NORMAL_RARITIES:
            continue

        expected_id = f"MGR_MOD_CARD_{pascal_to_upper_snake(card['codeName'])}"
        if expected_id in localized_titles:
            card_id = expected_id
        else:
            matches = ids_by_title.get(str(card["name"]), [])
            if len(matches) != 1:
                raise ValueError(
                    f"Cannot uniquely map registry card {card['codeName']} ({card['name']})"
                )
            card_id = matches[0]

        catalog[card_id] = {
            "id": card_id,
            "code_name": card["codeName"],
            "name": localized_titles.get(card_id, card["name"]),
            "rarity": card["rarity"],
            "type": card["type"],
        }
    return catalog


def read_snapshot(path: Path) -> Iterable[dict[str, Any]]:
    with gzip.open(path, "rt", encoding="utf-8") as stream:
        for line_number, line in enumerate(stream, 1):
            if not line.strip():
                continue
            try:
                yield json.loads(line)
            except json.JSONDecodeError as error:
                raise ValueError(f"Invalid JSON on line {line_number} of {path}") from error


def wilson_interval(successes: int, trials: int, z: float = 1.959963984540054) -> tuple[float, float]:
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
    if value is None:
        return "—"
    return f"{value * 100:.{digits}f}%"


def signed_percent(value: float | None, digits: int = 1) -> str:
    if value is None:
        return "—"
    return f"{value * 100:+.{digits}f}pp"


def escape_md(value: Any) -> str:
    return str(value).replace("|", "\\|").replace("\n", " ")


def markdown_table(headers: list[str], rows: list[list[Any]]) -> str:
    output = [
        "| " + " | ".join(escape_md(item) for item in headers) + " |",
        "| " + " | ".join("---" for _ in headers) + " |",
    ]
    output.extend("| " + " | ".join(escape_md(item) for item in row) + " |" for row in rows)
    return "\n".join(output)


def parse_timestamp(value: str) -> datetime:
    return datetime.fromisoformat(value.replace("Z", "+00:00"))


def validate_payload(payload: dict[str, Any]) -> list[str]:
    """Mirror the card-relevant consistency rules used by the PS validator."""
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
        for card in floor.get("cards_gained", []):
            if card.get("floor_added") is None:
                issues.append("gained_card_floor")
                break
    if floors and int(floors[0].get("hp_healed", 0)) != 0:
        issues.append("initial_setup_healing")
    if floors and int(floors[-1].get("current_hp", 0)) != int(
        payload.get("final_player", {}).get("current_hp", 0)
    ):
        issues.append("final_hp")
    return issues


def main() -> None:
    args = parse_args()
    repo_root = Path(__file__).resolve().parent.parent
    data_dir = repo_root / "DataAnalysis/Data"
    input_path = args.input.resolve() if args.input else latest_snapshot(data_dir)
    output_dir = (args.output_dir or repo_root / "DataAnalysis/Reports").resolve()
    output_dir.mkdir(parents=True, exist_ok=True)
    catalog = load_card_catalog(repo_root)

    runs: dict[str, dict[str, Any]] = {}
    schema_counts: Counter[int] = Counter()
    skipped_legacy = 0
    skipped_incomplete = 0
    skipped_invalid = 0
    invalid_reasons: Counter[str] = Counter()
    duplicate_event_ids = 0
    version_counts: Counter[str] = Counter()
    version_wins: Counter[str] = Counter()
    ascension_counts: Counter[int] = Counter()
    ascension_wins: Counter[int] = Counter()
    latest_timestamp: datetime | None = None
    latest_version = ""

    for envelope in read_snapshot(input_path):
        payload = envelope["payload"]
        schema = int(payload.get("schema_version", 0))
        schema_counts[schema] += 1
        if schema < args.minimum_schema:
            skipped_legacy += 1
            continue
        if not bool(payload.get("mgr_mechanics", {}).get("tracking_complete", False)):
            skipped_incomplete += 1
            continue
        consistency_issues = validate_payload(payload)
        if consistency_issues:
            skipped_invalid += 1
            invalid_reasons.update(consistency_issues)
            continue
        event_id = str(payload.get("event_id", ""))
        if not event_id:
            raise ValueError("A current-schema telemetry record has no event_id")
        if event_id in runs:
            duplicate_event_ids += 1
            continue

        timestamp = parse_timestamp(str(envelope["timestamp"]))
        version = str(payload.get("mod_version", "unknown"))
        victory = bool(payload.get("victory", False))
        ascension = int(payload.get("ascension", 0))
        runs[event_id] = {
            "event_id": event_id,
            "timestamp": timestamp,
            "version": version,
            "victory": victory,
            "ascension": ascension,
            "payload": payload,
        }
        version_counts[version] += 1
        version_wins[version] += int(victory)
        ascension_counts[ascension] += 1
        ascension_wins[ascension] += int(victory)
        if latest_timestamp is None or timestamp > latest_timestamp:
            latest_timestamp = timestamp
            latest_version = version

    if not runs:
        raise ValueError("No eligible telemetry runs remain after filtering")

    offers: Counter[str] = Counter()
    picks: Counter[str] = Counter()
    offer_floors: dict[str, list[int]] = defaultdict(list)
    pick_floors: dict[str, list[int]] = defaultdict(list)
    offered_runs: dict[str, set[str]] = defaultdict(set)
    picked_runs: dict[str, set[str]] = defaultdict(set)
    final_deck_runs: dict[str, set[str]] = defaultdict(set)
    latest_offers: Counter[str] = Counter()
    latest_picks: Counter[str] = Counter()
    latest_picked_runs: dict[str, set[str]] = defaultdict(set)
    reward_entries_total = 0
    reward_entries_mgr = 0

    for event_id, run in runs.items():
        payload = run["payload"]
        is_latest_version = run["version"] == latest_version
        for card in payload["final_player"].get("deck", []):
            card_id = str(card.get("id", ""))
            if card_id in catalog:
                final_deck_runs[card_id].add(event_id)

        for floor in payload.get("floors", []):
            if floor.get("resolved_room_type") not in COMBAT_ROOM_TYPES:
                continue
            floor_number = int(floor.get("floor", 0))
            for choice in floor.get("card_choices", []):
                reward_entries_total += 1
                card_id = str(choice.get("card", {}).get("id", ""))
                if card_id not in catalog:
                    continue
                reward_entries_mgr += 1
                offers[card_id] += 1
                offer_floors[card_id].append(floor_number)
                offered_runs[card_id].add(event_id)
                if is_latest_version:
                    latest_offers[card_id] += 1
                if bool(choice.get("picked", False)):
                    picks[card_id] += 1
                    pick_floors[card_id].append(floor_number)
                    picked_runs[card_id].add(event_id)
                    if is_latest_version:
                        latest_picks[card_id] += 1
                        latest_picked_runs[card_id].add(event_id)

    total_runs = len(runs)
    total_wins = sum(int(run["victory"]) for run in runs.values())
    baseline_win_rate = total_wins / total_runs
    latest_run_ids = {event_id for event_id, run in runs.items() if run["version"] == latest_version}
    latest_wins = sum(int(runs[event_id]["victory"]) for event_id in latest_run_ids)
    latest_baseline = latest_wins / len(latest_run_ids) if latest_run_ids else 0.0

    metrics: list[dict[str, Any]] = []
    for card_id, card in catalog.items():
        card_offers = offers[card_id]
        card_picks = picks[card_id]
        picked_ids = picked_runs[card_id]
        offered_ids = offered_runs[card_id]
        not_picked_ids = offered_ids - picked_ids
        picked_wins = sum(int(runs[event_id]["victory"]) for event_id in picked_ids)
        not_picked_wins = sum(int(runs[event_id]["victory"]) for event_id in not_picked_ids)
        deck_ids = final_deck_runs[card_id]
        deck_wins = sum(int(runs[event_id]["victory"]) for event_id in deck_ids)
        latest_picked_ids = latest_picked_runs[card_id]
        latest_picked_wins = sum(int(runs[event_id]["victory"]) for event_id in latest_picked_ids)
        pick_rate = card_picks / card_offers if card_offers else None
        pick_low, pick_high = wilson_interval(card_picks, card_offers)
        picked_win_rate = picked_wins / len(picked_ids) if picked_ids else None
        win_low, win_high = wilson_interval(picked_wins, len(picked_ids))
        not_picked_win_rate = (
            not_picked_wins / len(not_picked_ids) if not_picked_ids else None
        )
        deck_win_rate = deck_wins / len(deck_ids) if deck_ids else None
        latest_pick_rate = (
            latest_picks[card_id] / latest_offers[card_id]
            if latest_offers[card_id]
            else None
        )
        latest_win_rate = (
            latest_picked_wins / len(latest_picked_ids) if latest_picked_ids else None
        )
        metrics.append(
            {
                **card,
                "offers": card_offers,
                "picks": card_picks,
                "pick_rate": pick_rate,
                "pick_ci_low": pick_low,
                "pick_ci_high": pick_high,
                "offered_runs": len(offered_ids),
                "picked_runs": len(picked_ids),
                "picked_wins": picked_wins,
                "picked_win_rate": picked_win_rate,
                "win_ci_low": win_low,
                "win_ci_high": win_high,
                "not_picked_runs": len(not_picked_ids),
                "not_picked_wins": not_picked_wins,
                "not_picked_win_rate": not_picked_win_rate,
                "control_delta": (
                    picked_win_rate - not_picked_win_rate
                    if picked_win_rate is not None and not_picked_win_rate is not None
                    else None
                ),
                "baseline_delta": (
                    picked_win_rate - baseline_win_rate
                    if picked_win_rate is not None
                    else None
                ),
                "median_offer_floor": median(offer_floors[card_id]) if offer_floors[card_id] else None,
                "median_pick_floor": median(pick_floors[card_id]) if pick_floors[card_id] else None,
                "deck_runs": len(deck_ids),
                "deck_wins": deck_wins,
                "deck_win_rate": deck_win_rate,
                "latest_offers": latest_offers[card_id],
                "latest_picks": latest_picks[card_id],
                "latest_pick_rate": latest_pick_rate,
                "latest_picked_runs": len(latest_picked_ids),
                "latest_picked_wins": latest_picked_wins,
                "latest_win_rate": latest_win_rate,
            }
        )

    generated_at = datetime.now(timezone.utc).isoformat()
    snapshot_name = input_path.name
    version_rows = sorted(version_counts, key=lambda version: min(
        run["timestamp"] for run in runs.values() if run["version"] == version
    ))
    version_table = markdown_table(
        ["模组版本", "对局", "胜利", "胜率"],
        [
            [version, version_counts[version], version_wins[version], percent(version_wins[version] / version_counts[version])]
            for version in version_rows
        ],
    )

    ascension_groups = {
        "A0": [0],
        "A1–9": list(range(1, 10)),
        "A10+": list(range(10, 21)),
    }
    ascension_table_rows = []
    for label, levels in ascension_groups.items():
        count = sum(ascension_counts[level] for level in levels)
        wins = sum(ascension_wins[level] for level in levels)
        ascension_table_rows.append([label, count, wins, percent(wins / count if count else 0)])
    ascension_table = markdown_table(["进阶组", "对局", "胜利", "胜率"], ascension_table_rows)

    eligible_win = [item for item in metrics if item["picked_runs"] >= args.minimum_win_sample]
    win_ranked = sorted(
        eligible_win,
        key=lambda item: (item["picked_win_rate"], item["picked_runs"]),
        reverse=True,
    )
    low_win_ranked = sorted(
        eligible_win,
        key=lambda item: (item["picked_win_rate"], -item["picked_runs"]),
    )
    win_rows = []
    for rank, item in enumerate(win_ranked[: args.top], 1):
        latest_text = (
            f"{percent(item['latest_win_rate'])} (n={item['latest_picked_runs']})"
            if item["latest_picked_runs"]
            else "—"
        )
        control_text = (
            f"{percent(item['not_picked_win_rate'])} (n={item['not_picked_runs']})"
            if item["not_picked_win_rate"] is not None
            else "—"
        )
        win_rows.append(
            [
                rank,
                item["name"],
                RARITY_LABELS[item["rarity"]],
                TYPE_LABELS.get(item["type"], item["type"]),
                item["picked_runs"],
                item["picked_wins"],
                percent(item["picked_win_rate"]),
                f"{percent(item['win_ci_low'])}–{percent(item['win_ci_high'])}",
                signed_percent(item["baseline_delta"]),
                control_text,
                signed_percent(item["control_delta"]),
                item["median_pick_floor"],
                latest_text,
            ]
        )

    rarity_win_sections = []
    rarity_low_win_sections = []
    for rarity in ("Common", "Uncommon", "Rare"):
        subset = [item for item in eligible_win if item["rarity"] == rarity]
        subset.sort(key=lambda item: (item["picked_win_rate"], item["picked_runs"]), reverse=True)
        rarity_win_sections.append(f"### {RARITY_LABELS[rarity]}\n\n" + markdown_table(
            ["卡牌", "抓取对局", "胜率", "相对未选", "中位抓取层"],
            [
                [
                    item["name"],
                    item["picked_runs"],
                    percent(item["picked_win_rate"]),
                    signed_percent(item["control_delta"]),
                    item["median_pick_floor"],
                ]
                for item in subset[:5]
            ],
        ))
        subset.sort(key=lambda item: (item["picked_win_rate"], -item["picked_runs"]))
        rarity_low_win_sections.append(f"### {RARITY_LABELS[rarity]}\n\n" + markdown_table(
            ["卡牌", "抓取对局", "胜率", "相对未选", "中位抓取层"],
            [
                [
                    item["name"],
                    item["picked_runs"],
                    percent(item["picked_win_rate"]),
                    signed_percent(item["control_delta"]),
                    item["median_pick_floor"],
                ]
                for item in subset[:5]
            ],
        ))

    low_win_rows = []
    for rank, item in enumerate(low_win_ranked[:10], 1):
        latest_text = (
            f"{percent(item['latest_win_rate'])} (n={item['latest_picked_runs']})"
            if item["latest_picked_runs"]
            else "—"
        )
        control_text = (
            f"{percent(item['not_picked_win_rate'])} (n={item['not_picked_runs']})"
            if item["not_picked_win_rate"] is not None
            else "—"
        )
        low_win_rows.append(
            [
                rank,
                item["name"],
                RARITY_LABELS[item["rarity"]],
                TYPE_LABELS.get(item["type"], item["type"]),
                item["picked_runs"],
                item["picked_wins"],
                percent(item["picked_win_rate"]),
                f"{percent(item['win_ci_low'])}–{percent(item['win_ci_high'])}",
                control_text,
                signed_percent(item["control_delta"]),
                item["median_pick_floor"],
                latest_text,
            ]
        )

    deck_ranked = sorted(
        [item for item in metrics if item["deck_runs"] >= args.minimum_win_sample],
        key=lambda item: (item["deck_win_rate"], item["deck_runs"]),
        reverse=True,
    )
    deck_rows = [
        [
            rank,
            item["name"],
            RARITY_LABELS[item["rarity"]],
            item["deck_runs"],
            item["deck_wins"],
            percent(item["deck_win_rate"]),
        ]
        for rank, item in enumerate(deck_ranked[:10], 1)
    ]

    top_win_names = "、".join(item["name"] for item in win_ranked[:5])
    low_win_names = "、".join(item["name"] for item in low_win_ranked[:5])
    positive_control = sorted(
        [item for item in eligible_win if item["control_delta"] is not None],
        key=lambda item: (item["control_delta"], item["picked_runs"]),
        reverse=True,
    )
    positive_control_names = "、".join(item["name"] for item in positive_control[:5])

    win_report = f"""# MGR 卡牌胜率分析

> 生成时间：{generated_at}  
> 数据快照：`{snapshot_name}`

## 结论摘要

- 当前有效样本为 **{total_runs} 局**，其中胜利 **{total_wins} 局**，总体胜率 **{percent(baseline_win_rate)}**。
- 在至少 {args.minimum_win_sample} 个独立对局中从战斗奖励抓取过的卡牌里，原始抓取后胜率领先的卡牌为：**{top_win_names}**。
- 相同门槛下，抓取后胜率最低的卡牌为：**{low_win_names}**。它们是优先复查对象，但低胜率仍可能来自前期救急抓取、窄流派或抓取时局势较差，不应直接等同于卡牌过弱。
- 与“同样见过该牌但整局没有选择它”的对照组相比，正向差值较大的卡牌为：**{positive_control_names}**。这个差值比单纯胜率更值得关注，但仍不能证明因果。
- 最新遥测构建 `{latest_version}` 有 **{len(latest_run_ids)} 局**，胜率 **{percent(latest_baseline)}**；主表同时列出最新构建数据，便于识别旧数值混合造成的偏差。

## 统计口径

- 仅使用 schema ≥ {args.minimum_schema}、`tracking_complete=true` 且通过音符、伤害、逐层与生命一致性检查的正式单人 MGR 对局；跳过 {skipped_legacy} 条旧结构、{skipped_incomplete} 条不完整记录、{skipped_invalid} 条不一致记录，并在分析层再次去除 {duplicate_event_ids} 条重复 `event_id`。
- “抓取后胜率”按**唯一对局**计算：一局中无论抓取同名牌一张还是多张，都只计一次；该局最终胜利则记为胜利。
- 只认 `Monster`、`Elite`、`Boss` 节点的战斗卡牌奖励。排除商店、事件、先古、特殊发现、初始牌、衍生牌、先古牌和联机专属牌。
- 升级前后合并为同一张牌。报告中的95%区间采用 Wilson 二项比例区间。

## 样本结构

{ascension_table}

{version_table}

## 胜率最高的卡牌

最低门槛：至少 {args.minimum_win_sample} 个抓取对局。`相对未选` 的对照组是“本局至少见过该牌，但整局从未在战斗奖励里选它”的对局。

{markdown_table(
    ["#", "卡牌", "稀有度", "类型", "抓取对局", "胜利", "胜率", "95%区间", "相对全局", "未选胜率", "相对未选", "中位抓取层", "最新构建"],
    win_rows,
)}

## 抓取后胜率最低的卡牌

最低门槛同样是至少 {args.minimum_win_sample} 个抓取对局。`相对未选` 为负，表示在同样见过该牌的对局中，选择该牌的玩家胜率更低；这仍是相关性，不是因果结论。

{markdown_table(
    ["#", "卡牌", "稀有度", "类型", "抓取对局", "胜利", "胜率", "95%区间", "未选胜率", "相对未选", "中位抓取层", "最新构建"],
    low_win_rows,
)}

## 同稀有度前列

{chr(10).join(rarity_win_sections)}

## 同稀有度末位

{chr(10).join(rarity_low_win_sections)}

## 最终卡组存在率交叉检查

这张表会包含商店、事件等非战斗奖励来源，也会遗漏中途删除的卡，因此只用来交叉检查，不作为主排名。

{markdown_table(["#", "卡牌", "稀有度", "最终卡组对局", "胜利", "胜率"], deck_rows)}

## 如何解读

1. **高胜率不等于卡牌独立强度。** 金卡和后期牌只有先活到较高层数才有机会取得，天然带有幸存者偏差。
2. **玩家选择不是随机实验。** 玩家会在适合的卡组里抓协同牌，因此高胜率可能说明“适配成功的成型卡组”，而非盲抓也强。
3. **当前进阶分布偏低。** A0 占比较高；调平衡前应同时查看 A10+ 或以后积累更多高进阶样本。
4. **多个构建混合。** 这几天卡牌数值持续调整，主表是历史综合表现；最新构建列样本更少，但更接近当前版本。
5. **优先调查而非直接削弱。** 建议先检查“胜率高、相对未选差值也高、样本足够、最新构建仍高”的交集，再结合卡牌定位和抓取层数判断。
"""

    eligible_pick = [item for item in metrics if item["offers"] >= args.minimum_offers]
    pick_ranked = sorted(
        eligible_pick,
        key=lambda item: (item["pick_rate"], item["offers"]),
        reverse=True,
    )
    pick_rows = []
    for rank, item in enumerate(pick_ranked[: args.top], 1):
        latest_text = (
            f"{percent(item['latest_pick_rate'])} ({item['latest_picks']}/{item['latest_offers']})"
            if item["latest_offers"]
            else "—"
        )
        pick_rows.append(
            [
                rank,
                item["name"],
                RARITY_LABELS[item["rarity"]],
                TYPE_LABELS.get(item["type"], item["type"]),
                item["offers"],
                item["picks"],
                percent(item["pick_rate"]),
                f"{percent(item['pick_ci_low'])}–{percent(item['pick_ci_high'])}",
                item["median_offer_floor"],
                latest_text,
            ]
        )

    rarity_pick_sections = []
    rarity_low_pick_sections = []
    for rarity in ("Common", "Uncommon", "Rare"):
        subset = [item for item in eligible_pick if item["rarity"] == rarity]
        subset.sort(key=lambda item: (item["pick_rate"], item["offers"]), reverse=True)
        rarity_pick_sections.append(f"### {RARITY_LABELS[rarity]}\n\n" + markdown_table(
            ["卡牌", "出现", "选取", "选取率", "最新构建"],
            [
                [
                    item["name"],
                    item["offers"],
                    item["picks"],
                    percent(item["pick_rate"]),
                    percent(item["latest_pick_rate"]),
                ]
                for item in subset[:5]
            ],
        ))
        subset.sort(key=lambda item: (item["pick_rate"], -item["offers"]))
        rarity_low_pick_sections.append(f"### {RARITY_LABELS[rarity]}\n\n" + markdown_table(
            ["卡牌", "出现", "选取", "选取率", "最新构建"],
            [
                [
                    item["name"],
                    item["offers"],
                    item["picks"],
                    percent(item["pick_rate"]),
                    percent(item["latest_pick_rate"]),
                ]
                for item in subset[:5]
            ],
        ))

    volume_ranked = sorted(metrics, key=lambda item: (item["picks"], item["offers"]), reverse=True)
    volume_rows = [
        [rank, item["name"], RARITY_LABELS[item["rarity"]], item["picks"], item["offers"], percent(item["pick_rate"])]
        for rank, item in enumerate(volume_ranked[:10], 1)
    ]
    low_pick = sorted(
        eligible_pick,
        key=lambda item: (item["pick_rate"], -item["offers"]),
    )[:10]
    low_rows = [
        [
            rank,
            item["name"],
            RARITY_LABELS[item["rarity"]],
            TYPE_LABELS.get(item["type"], item["type"]),
            item["offers"],
            item["picks"],
            percent(item["pick_rate"]),
            f"{percent(item['pick_ci_low'])}–{percent(item['pick_ci_high'])}",
            item["median_offer_floor"],
            f"{percent(item['latest_pick_rate'])} ({item['latest_picks']}/{item['latest_offers']})" if item["latest_offers"] else "—",
        ]
        for rank, item in enumerate(low_pick, 1)
    ]
    top_pick_names = "、".join(item["name"] for item in pick_ranked[:5])
    low_pick_names = "、".join(item["name"] for item in low_pick[:5])

    pick_report = f"""# MGR 卡牌奖励选取率分析

> 生成时间：{generated_at}  
> 数据快照：`{snapshot_name}`

## 结论摘要

- 当前有效样本为 **{total_runs} 局**；战斗节点记录了 {reward_entries_total} 个卡牌候选条目，其中 {reward_entries_mgr} 个属于当前正常单人 MGR 卡池。
- 在至少出现 {args.minimum_offers} 次的卡牌里，选取率最高的卡牌为：**{top_pick_names}**。
- 相同门槛下，选取率最低的卡牌为：**{low_pick_names}**。这些牌最常被玩家主动跳过，适合优先检查定位、文本吸引力和实际数值是否匹配。
- “选取率”表示该牌作为战斗奖励候选出现后被点击的比例，不是它占全部卡组的比例，也不包含商店购买和事件直接获得。
- 最新遥测构建为 `{latest_version}`，主表保留最新构建选取率，方便识别近期平衡修改后的方向变化。

## 统计口径

- 只统计 `Monster`、`Elite`、`Boss` 节点的 `card_choices`；明确排除 `Shop`、`Event`、`Ancient`、`RestSite` 和 `Treasure`，因为原版会把商店库存及部分特殊选择也写进同一个字段。
- 分母为该卡在合规战斗奖励中出现的条目数，分子为其中 `picked=true` 的条目数。升级版本合并。
- 只包含当前登记为启用、非联机专属、稀有度为白/蓝/金的 MGR 卡；基础牌、衍生牌和先古牌不参与排名。
- 最低门槛为出现 {args.minimum_offers} 次；95%区间采用 Wilson 二项比例区间。

## 选取率最高的卡牌

{markdown_table(
    ["#", "卡牌", "稀有度", "类型", "出现", "选取", "选取率", "95%区间", "中位出现层", "最新构建"],
    pick_rows,
)}

## 同稀有度前列

{chr(10).join(rarity_pick_sections)}

## 被选择总次数最多

总次数会同时受稀有度和出现频率影响，不能替代选取率，但能反映哪些牌最常进入真实卡组。

{markdown_table(["#", "卡牌", "稀有度", "选取", "出现", "选取率"], volume_rows)}

## 选取率最低的卡牌

最低门槛同样是至少出现 {args.minimum_offers} 次。低选取率不自动等于废卡：窄流派、对策卡和有意降低卡池质量的弱牌都可能合理地处于末位。

{markdown_table(["#", "卡牌", "稀有度", "类型", "出现", "选取", "选取率", "95%区间", "中位出现层", "最新构建"], low_rows)}

## 同稀有度末位

{chr(10).join(rarity_low_pick_sections)}

## 如何解读

1. **选取率高通常说明即时吸引力或适配面广，未必说明强度超标。** 过渡攻击、防御补口和启动牌可能经常被抓，却不会拥有最高终局胜率。
2. **稀有度必须分开看。** 金卡多出现在Boss奖励，候选环境和抓取时点都与白卡不同，所以报告提供同稀有度排名。
3. **选取率低也不等于废卡。** 窄流派核心、对策牌或有意控制卡池质量的弱牌都可能合理地低选。
4. **优先关注稳定变化。** 如果一张牌在全部历史和最新构建中都高选，同时抓取后胜率与对照组也明显偏高，才更值得进入平衡复查队列。
"""

    win_path = output_dir / "MGR卡牌胜率分析.md"
    pick_path = output_dir / "MGR卡牌选取率分析.md"
    csv_path = output_dir / "MGR卡牌统计明细.csv"
    win_path.write_text(win_report, encoding="utf-8")
    pick_path.write_text(pick_report, encoding="utf-8")

    csv_fields = [
        "id", "code_name", "name", "rarity", "type", "offers", "picks", "pick_rate",
        "picked_runs", "picked_wins", "picked_win_rate", "not_picked_runs",
        "not_picked_win_rate", "control_delta", "median_pick_floor", "deck_runs",
        "deck_win_rate", "latest_offers", "latest_picks", "latest_pick_rate",
        "latest_picked_runs", "latest_win_rate",
    ]
    with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=csv_fields, extrasaction="ignore")
        writer.writeheader()
        writer.writerows(sorted(metrics, key=lambda item: (item["rarity"], item["name"])))

    summary = {
        "input": str(input_path),
        "valid_runs": total_runs,
        "wins": total_wins,
        "baseline_win_rate": baseline_win_rate,
        "latest_version": latest_version,
        "latest_version_runs": len(latest_run_ids),
        "normal_cards": len(catalog),
        "reward_entries_total": reward_entries_total,
        "reward_entries_mgr": reward_entries_mgr,
        "schema_counts": dict(sorted(schema_counts.items())),
        "skipped_legacy": skipped_legacy,
        "skipped_incomplete": skipped_incomplete,
        "skipped_invalid": skipped_invalid,
        "invalid_reasons": dict(sorted(invalid_reasons.items())),
        "duplicate_event_ids": duplicate_event_ids,
        "win_report": str(win_path),
        "pick_report": str(pick_path),
        "detail_csv": str(csv_path),
    }
    print(json.dumps(summary, ensure_ascii=False, indent=2))


if __name__ == "__main__":
    main()
