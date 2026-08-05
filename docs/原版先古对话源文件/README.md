# 原版角色与先古对话源文件

本目录保存从本机当前版本《杀戮尖塔 2》提取的原版先古对话实现，供 MGR 模组开发时查阅。

## 目录内容

- `ancients.zhs.original.json`：简体中文原始本地化键值，共 279 条。
- `ancients.eng.original.json`：英文原始本地化键值，共 279 条，便于核对原意。
- `先古事件/`：九个实际构造 `CharacterDialogues` 的原版事件类。
- `对话框架/`：`AncientDialogueSet`、`AncientDialogue`、对话行及说话者等底层结构。

## 角色标识

- `IRONCLAD`：战士
- `SILENT`：猎人
- `DEFECT`：机器人
- `NECROBINDER`：死灵使者
- `REGENT`：储君
- `ANY`：不区分角色的通用对话池

## 先古标识

- `NEOW`：涅奥
- `DARV`：达尔夫
- `NONUPEIPE`：诺努佩佩
- `OROBAS`：奥罗巴斯
- `PAEL`：派尔
- `TANX`：坦克斯
- `TEZCATARA`：特斯卡塔拉
- `VAKUU`：瓦库
- `THE_ARCHITECT`：建筑师

## 阅读方式

先在 `先古事件/` 对应类的 `DialogueSet` 或 `CharacterDialogues` 中查看对话序号、造访条件和说话者，再用完整本地化键到 JSON 中查找实际文本。例如：

```text
NEOW.talk.IRONCLAD.0-0.ancient
│    │    │        │ │ └─ 说话者
│    │    │        │ └─── 对话中的行号
│    │    │        └───── 对话序号
│    │    └────────────── 角色
│    └─────────────────── 对话命名空间
└──────────────────────── 先古
```

键中的 `r` 表示该条属于可随机选取的重复访问对话池。

## 来源说明

事件与框架 `.cs` 文件是从本机当前游戏程序集反编译得到的开发参考；本地化 JSON 是从当前游戏包中的对应原始键值机械提取而成。它们不是 MGR 模组运行时依赖，也不会被打包进模组资源。
