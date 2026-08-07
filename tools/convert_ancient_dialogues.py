"""Convert the human-authored MGR Ancient dialogue template to runtime loc keys."""

from __future__ import annotations

import json
from pathlib import Path


REPO_ROOT = Path(__file__).resolve().parents[1]
SOURCE = REPO_ROOT / "docs" / "MGR_先古对话人类填写模板.json"
TARGET = (
    REPO_ROOT
    / "SlayTheSpire2MGRMod"
    / "localization"
    / "zhs"
    / "ancients.json"
)
EXPECTED_ANCIENTS = {
    "NEOW",
    "DARV",
    "NONUPEIPE",
    "OROBAS",
    "PAEL",
    "TANX",
    "TEZCATARA",
    "VAKUU",
    "THE_ARCHITECT",
}


def require(condition: bool, message: str) -> None:
    if not condition:
        raise ValueError(message)


def convert(source: dict) -> dict[str, str]:
    character_id = source["角色信息"]["角色注册ID_请勿修改"]
    require(bool(character_id), "角色注册ID不能为空。")

    ancients = source["先古对话"]
    ids = [ancient["先古ID_请勿修改"] for ancient in ancients]
    require(len(ids) == len(set(ids)), "先古ID存在重复。")
    require(set(ids) == EXPECTED_ANCIENTS, "先古ID集合与当前游戏接入表不一致。")

    output: dict[str, str] = {}
    for ancient in ancients:
        ancient_id = ancient["先古ID_请勿修改"]
        enabled_groups = [
            group for group in ancient["对话组"] if group.get("启用") == 1
        ]
        require(enabled_groups, f"{ancient_id}没有任何启用的对话组。")

        for dialogue_index, group in enumerate(enabled_groups):
            human_visit = group["首次出现于第几次相遇"]
            repeating = group["之后是否允许随机重复"]
            lines = group["台词"]
            require(
                isinstance(human_visit, int) and human_visit >= 1,
                f"{ancient_id}第{dialogue_index + 1}组的相遇次数无效。",
            )
            require(
                repeating in (0, 1),
                f"{ancient_id}第{dialogue_index + 1}组的重复标记必须为0或1。",
            )
            require(lines, f"{ancient_id}第{dialogue_index + 1}组没有台词。")

            dialogue_prefix = f"{ancient_id}.talk.{character_id}.{dialogue_index}"
            output[f"{dialogue_prefix}-visit"] = str(human_visit - 1)
            repeat_suffix = "r" if repeating == 1 else ""

            expected_order = list(range(1, len(lines) + 1))
            actual_order = [line["顺序"] for line in lines]
            require(
                actual_order == expected_order,
                f"{ancient_id}第{dialogue_index + 1}组台词顺序不连续。",
            )

            for line_index, line in enumerate(lines):
                speaker = line["说话者"]
                require(
                    speaker in ("先古", "MGR"),
                    f"{ancient_id}第{dialogue_index + 1}组第{line_index + 1}句说话者无效。",
                )
                text = line["文本"]
                require(
                    isinstance(text, str) and bool(text.strip()),
                    f"{ancient_id}第{dialogue_index + 1}组第{line_index + 1}句文本为空。",
                )

                is_last = line_index == len(lines) - 1
                next_text = line["点击后按钮文字"]
                require(
                    is_last or bool(next_text.strip()),
                    f"{ancient_id}第{dialogue_index + 1}组第{line_index + 1}句缺少按钮文字。",
                )
                require(
                    not is_last or not next_text.strip(),
                    f"{ancient_id}第{dialogue_index + 1}组最后一句不应填写按钮文字。",
                )

                line_prefix = (
                    f"{dialogue_prefix}-{line_index}{repeat_suffix}"
                )
                speaker_suffix = "ancient" if speaker == "先古" else "char"
                full_line_key = f"{line_prefix}.{speaker_suffix}"
                output[full_line_key] = text

                sfx = line["播放音效"]
                if sfx:
                    output[f"{full_line_key}.sfx"] = sfx
                if next_text:
                    output[f"{line_prefix}.next"] = next_text

    return output


def main() -> None:
    source = json.loads(SOURCE.read_text(encoding="utf-8-sig"))
    converted = convert(source)
    TARGET.write_text(
        json.dumps(converted, ensure_ascii=False, indent=2) + "\n",
        encoding="utf-8",
    )
    print(f"Converted {len(source['先古对话'])} Ancients and {len(converted)} loc entries.")
    print(TARGET)


if __name__ == "__main__":
    main()
