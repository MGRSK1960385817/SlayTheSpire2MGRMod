# MGR 角色 Spine 动画接入记录

更新日期：2026-07-31

## 1. 最终决定

MGR 已放弃 Spine 骨骼动画接入，后续统一使用现有 PNG 序列实现角色动画。除非用户将来明确重新启动该方向，否则不再进行 Spine 版本转换、运行库替换或角色场景 Spine 化工作。

现有 PNG 序列版本是正式实现，应继续保留并在此基础上调整帧率、尺寸、位置、亮度、阴影及其他角色表现。

本次收到的 Spine 资源由 Spine 4.3.23 导出，而《杀戮尖塔 2》及现有可运行模组使用 Spine 4.2.43 运行库。4.3 骨骼不能直接交给 4.2 运行库使用。

直接修改 `.skel` 中的版本字符串不是有效方案：临时伪装成 4.2 后虽然能够进入资源导入阶段，但实例化 `SpineSprite` 时会发生原生层崩溃（signal 11）。

## 2. 原始资源

当前外部资源目录：

`C:\Users\19603\Desktop\spine`

主要文件：

- `260727output.spine`：Spine 工程源文件。
- `260727.skel`：Spine 4.3.23 二进制骨骼。
- `260727output.atlas`：五页图集描述。
- `260727output.png` 至 `260727output_5.png`：图集贴图。
- `images/`：原始拆分图片。

图集贴图均可被 Godot 正常读取，当前阻碍来自 `.skel` 骨骼版本，而不是 PNG 或 atlas 路径。

## 3. 已解析的骨骼内容

通过修正第三方 4.3 读取器后，已确认该骨骼包含：

- 204 根骨骼；
- 65 个插槽；
- 14 个 IK Constraint；
- 30 个 Transform Constraint；
- 1 个 Spine 4.3 独有的 Slider Constraint，名称为 `eye`；
- 两个动画：`eye` 与 `idle1`。

30 个 Transform Constraint 主要是简单的 X→X、Y→Y 映射，理论上可以转换为 4.2 约束。但 `eye` Slider Constraint 无法直接由 4.2 表达。

`idle1` 会改变 `eye` 骨骼的旋转值，再由 Slider Constraint 驱动 `eye` 动画中的 14 条眼部骨骼时间线。因此直接删除 Slider 会损失眨眼或眼部变化。

## 4. 推荐的重新导出流程

应优先由持有 Spine 正式授权的动画制作者执行以下流程：

1. 使用 Spine 4.3.23 打开 `260727output.spine`。
2. 选择数据导出并输出 JSON。
3. JSON 的 `Version` 选择 `4.2`。
4. 勾选 `Nonessential data`；建议同时启用 `Pretty print`，便于检查。
5. 使用 Spine 4.2.43 的 `Import Data` 导入该 JSON。
6. 将 Images Path 指向原始 `images/` 目录，并确认正确启用角色使用的 Skin。
7. 在 Spine 4.2.43 中检查 `idle1` 的身体、头发、四肢、眼睛和蒙皮是否正常。
8. 处理 4.3 Slider Constraint：将眼部最终效果烘焙为普通骨骼关键帧，或在 4.2 中重新制作相同眼部动画。
9. 最终使用 Spine 4.2.43 重新导出 `.skel`、`.atlas` 和配套 PNG 图集。

Spine Trial 不能保存工程或导出动画数据，因此这一步需要正式授权版本或由原动画制作者完成。

## 5. 建议交付规格

重新导出的运行时资源建议统一命名：

- `Mgr_character.skel`
- `Mgr_character.atlas`
- `Mgr_character_atlas.png`、`Mgr_character_atlas_2.png` 等图集页

要求：

- `.skel` 头部版本必须是 `4.2.x`，推荐精确使用 4.2.43；
- atlas 中的页名必须与实际 PNG 文件名完全一致；
- 保持 Premultiplied Alpha（PMA）设置一致；
- 至少提供一个循环待机动画；
- 待机动画推荐命名为 `idle_loop`。如果仍命名为 `idle1`，接入代码可以显式映射；
- 若后续提供战斗动作，推荐使用 `attack`、`cast`、`hurt`、`die` 等标准名称。

## 6. 历史备用接入步骤（当前不执行）

1. 先在隔离 Godot 工程中使用现有 Spine-Godot 4.2 扩展导入资源。
2. 创建 `SpineSkeletonDataResource`，连接 atlas 与 skel。
3. 实例化 `SpineSprite` 并循环播放待机动画，确认不会崩溃。
4. 检查贴图缺失、蒙皮错位、约束变形、PMA 黑边和动画循环接缝。
5. 将 MGR 角色场景的 `Visuals` 从 `Sprite2D` 替换为 `SpineSprite`。
6. 使用 RitsuLib/STS2 的 Spine 状态机接入待机及战斗状态。
7. 保留并重新校准现有程序化地面阴影、人物位置、缩放、意图点和对话点。
8. 完成编译和 PCK 导出；由用户启动游戏进行人物选择、战斗和商店页面测试。
9. 验证稳定后再删除旧 PNG 序列资源和对应代码。

## 7. 验收清单

- 进入战斗不会崩溃，控制台没有 Spine 版本错误。
- 待机动画连续循环，没有明显跳帧。
- 眼睛仍能正常眨动，眼部附件没有丢失。
- 头发、四肢及身体约束未发生拉伸或错位。
- 所有 atlas 页均被使用，没有紫黑缺失贴图。
- 人物亮度、大小和落点与当前 PNG 版本接近。
- 地面阴影、意图图标和对话位置仍然正确。
- 人物选择、战斗、商店等页面不会出现尺寸异常。

## 8. 已放弃的备用方案

如果无法取得 Spine 4.2.43 重新导出资源，可以编写定制烘焙转换器：用 4.3 运行库采样 `idle1`，将 Slider 与其他约束的最终骨骼姿态烘焙为普通关键帧，再输出 4.2 JSON 和 skel。

该方案技术上可行，但文件会变大，曲线可能失真，且必须逐帧验证；优先级低于官方编辑器降级流程。

## 9. 相关资料

- STS2 模组角色动画教程：<https://tutorials.sts2modding.com/docs/04-ritsulib/04-15-2-character-animation/>
- Spine 版本兼容说明：<https://esotericsoftware.com/spine-versioning>
- Spine 数据导出说明：<https://esotericsoftware.com/spine-export>
- Spine 4.3→4.2 约束差异讨论：<https://esotericsoftware.com/forum/d/29709-downgrading-spine-43-data-to-42>
- Spine Slider 说明：<https://en.esotericsoftware.com/spine-sliders>

## 10. 当前代码状态

本次兼容性调查没有修改 MGR 正式角色场景和角色动画代码。后续正式方案继续由以下文件管理 PNG 序列版角色：

- `Scripts/Characters/MgrCharacterAnimation.cs`
- `Scripts/Characters/MgrCharacter.cs`
- `SlayTheSpire2MGRMod/scenes/characters/Mgr_character.tscn`

后续角色动画修改应直接围绕上述 PNG 序列实现展开，不应为了 Spine 接入而重构或删除上述实现。
