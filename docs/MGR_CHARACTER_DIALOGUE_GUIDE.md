# MGR 人物剧情与对话接入指南

这份指南记录塔二人物“到达节点后显示文本”的三条实际链路。此处的重点是**与先古之民交谈的事件节点**。MGR 已接入当前全部八位标准先古及终局建筑师的角色专属对话；战斗气泡仍只维护草稿。

面向人类填写的先古对话位于 [MGR_先古对话人类填写模板.json](MGR_先古对话人类填写模板.json)。其中使用“第几次相遇”“是否允许重复”“说话者”等直观字段；填写完成后由 Codex 转换为游戏实际读取的 `SlayTheSpire2MGRMod/localization/zhs/ancients.json`。内容索引与其他候选对话位于 [MGR_dialogue_draft.json](MGR_dialogue_draft.json)。

转换器位于 `tools/convert_ancient_dialogues.py`。它会校验先古ID、相遇次数、说话者、连续行号、按钮链和空台词，再覆盖生成简中运行文件；不会改动英文及其他语言：

```powershell
python tools/convert_ancient_dialogues.py
```

## 1. 角色内置文本

文本文件：`SlayTheSpire2MGRMod/localization/zhs/characters.json`。

键格式为：

```text
SLAY_THE_SPIRE2_MGR_MOD_CHARACTER_MGR_CHARACTER.<field>
```

已验证、由原版角色界面或流程自动读取的字段包括：

| 字段 | 出现时机 |
| --- | --- |
| `selectMessage` | 角色选择时 |
| `victoryMessage` / `defeatMessage` | 胜利 / 失败界面 |
| `goldMonologue` | 获得金币时 |
| `eventDeathPrevention` | 事件中的濒死保护文本 |
| `aromaPrinciple` | 芳香类事件文本 |
| `banter.alive.endTurnPing` / `banter.dead.endTurnPing` | 多人战斗结束回合提示 |

魔理沙、狐狸、工匠均使用此文件承载上述短台词；这是最轻量、无需额外 C# 代码的接入方式。

## 2. 先古之民剧情节点（每幕开始）

这不是狐狸模组的 `Story + Epoch` 时间线系统。原版在每个先古类的 `DefineDialogues()` 中按角色 ID 建立专属对话池；RitsuLib 0.5.1 则提供了面向模组角色的本地化自动注入层。

当前版本确认的全部相关条目如下：

| 实际 ID | 原版类型 | 场景 |
| --- | --- | --- |
| `NEOW` | `MegaCrit.Sts2.Core.Models.Events.Neow` | 第一幕开始的先古之民事件 |
| `DARV` | `MegaCrit.Sts2.Core.Models.Events.Darv` | 后续幕开始的先古之民事件 |
| `NONUPEIPE` | `MegaCrit.Sts2.Core.Models.Events.Nonupeipe` | 标准先古之民事件 |
| `OROBAS` | `MegaCrit.Sts2.Core.Models.Events.Orobas` | 标准先古之民事件 |
| `PAEL` | `MegaCrit.Sts2.Core.Models.Events.Pael` | 标准先古之民事件 |
| `TANX` | `MegaCrit.Sts2.Core.Models.Events.Tanx` | 标准先古之民事件 |
| `TEZCATARA` | `MegaCrit.Sts2.Core.Models.Events.Tezcatara` | 标准先古之民事件 |
| `VAKUU` | `MegaCrit.Sts2.Core.Models.Events.Vakuu` | 标准先古之民事件 |
| `THE_ARCHITECT` | `MegaCrit.Sts2.Core.Models.Events.TheArchitect` | 终局建筑师事件；使用同一套先古对话结构，但不是普通幕首房间 |

原版链路为：每个 `AncientEventModel` 在 `DefineDialogues()` 中建立 `AncientDialogueSet`；其中 `CharacterDialogues` 是按角色 ID 组织的专属对话池。进入先古之民房间时，游戏按“当前先古、当前角色、该角色此前访问次数”选择一段对话并展示。

MGR 不再自行补丁原版的 `DefineDialogues()`。RitsuLib 会在 `AncientDialogueSet.PopulateLocKeys()` 运行前完成以下工作：

1. 扫描 `ancients` 本地化表中当前先古与所有已注册模组角色的对话键。
2. 仅向对应角色 ID 的 `CharacterDialogues` 条目追加找到的对话。
3. 保留原版角色与其他模组角色已有的字典条目，不覆盖、不删除。

原版会根据条目位置自动解析本地化键；核心格式为：

```text
{ANCIENT_ENTRY}.talk.{MGR_CHARACTER_ENTRY}.{dialogueIndex}-{lineIndex}.ancient
{ANCIENT_ENTRY}.talk.{MGR_CHARACTER_ENTRY}.{dialogueIndex}-{lineIndex}.char
```

`.ancient` 表示先古之民说话，`.char` 表示 MGR 说话。若一组对话应在之后的访问中重复，第一行的 `{dialogueIndex}-{lineIndex}` 使用 `r` 后缀，例如 `2-0r.ancient`。角色首次、第二次等访问次数由原版管理，无需 MGR 自建存档计数。

可选元数据：

```text
{ANCIENT_ENTRY}.talk.{MGR_CHARACTER_ENTRY}.{dialogueIndex}-visit
{完整台词键}.sfx
```

`-visit` 显式规定此前访问次数；`.sfx` 指定该句播放的原版音效事件。MGR 将第3组重复对话的 `2-visit` 设为 `2`，使其从角色第3次访问开始进入候选池。

每位先古目前有三组 MGR 对话：索引 `0` 用于首次访问，索引 `1` 用于第二次访问，索引 `2` 带 `r` 后缀并进入之后的重复池。`THE_ARCHITECT` 使用该角色的胜场数作为访问计数，这是原版自己的逻辑。

## 3. 战斗条件气泡

工匠在符文槽满时使用：

```csharp
var line = new LocString("combat_messages", "MGR-...");
player.Creature.GetVfxContainer()?.AddChildSafely(
    NThoughtBubbleVfx.Create(line.GetFormattedText(), player.Creature, 1.0));
```

对应文本文件为 `SlayTheSpire2MGRMod/localization/zhs/combat_messages.json`。适合“音符槽已满”“首次触发和弦”“演奏队列过长”等短提示；必须由明确的游戏机制条件调用，并设置冷却或一次性标记，避免反复刷屏。

## 维护规则

- 先古之民中文台词优先修改 `docs/MGR_先古对话人类填写模板.json`，再由 Codex 转换到 `localization/zhs/ancients.json`，无需手写底层键或修改 C#。
- 新增对话时，对话组与组内台词必须从 `0` 开始连续编号；RitsuLib 遇到缺号就会停止扫描后续内容。
- 音效直接使用对应台词键的 `.sfx` 元数据；没有该字段时该句保持静音。
- 英文台词位于 `localization/eng/ancients.json`；日文等翻译后续统一处理。
- 已投入使用的本地化键、先古条目 ID 和对话序号应保持稳定；改台词不影响存档，改稳定标识可能改变原版的访问/重复对话判定。
