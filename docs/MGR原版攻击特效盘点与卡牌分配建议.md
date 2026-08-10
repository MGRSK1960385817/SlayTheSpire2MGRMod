# MGR 原版攻击特效盘点与卡牌分配建议

## 结论

本次检查基于当前本地反编译的《杀戮尖塔 2》原版五角色卡池源码。

- 原版五角色共有 162 张实际构造为 `CardType.Attack` 的牌，其中 161 张显式指定了命中特效。
- 唯一没有显式命中特效的例外是猎人的 `EchoingSlash`，它绕过普通 `DamageCmd.Attack`，直接循环结算伤害。
- 原版并非为每张攻击牌制作独占动画。大多数攻击牌复用斩击、钝击、重击、星光命中等通用效果；少数招牌牌才使用导弹、光束、全屏预演等组合动画。
- 《随之任之》已完成基础斩杀与目标预览反馈；其命中特效可在后续继续定制。
- 其余当前攻击牌中，《时间线之东》和《终曲》本身不直接造成伤害，不应强行添加敌方命中特效。

## 原版可复用特效分类

| 分类 | 主要接口或资源 | 原版例子 | 特点与使用建议 |
|---|---|---|---|
| 普通斩击 | `WithHitFx("vfx/vfx_attack_slash")` | 五角色打击、许多普通攻击 | 最轻量、最安全，适合低费单体攻击和高频演奏牌。 |
| 普通钝击 | `vfx/vfx_attack_blunt` | Bash、Ball Lightning 等 | 适合拳击、撞击、重物和不强调刀刃的攻击。 |
| 重型钝击 | `vfx/vfx_heavy_blunt` + `WithHitVfxSpawnedAtBase()` | Bludgeon、Uppercut、Crash Landing | 冲击感强，适合高费用、高伤害单击。 |
| 细斩 / 刺击 | `NThinSliceVfx`、`NStabVfx`、`vfx_dramatic_stab` | Slice、Neutralize、Skewer、Backstab | 比普通斩击更精确，适合轻快攻击、处决和单点攻击。 |
| 巨型斩击 | `NBigSlashVfx` + `NBigSlashImpactVfx` | Perfected Strike、Sovereign Blade | 适合招牌单体重斩，不宜给普通小攻击滥用。 |
| 全体横扫 | `vfx/vfx_giant_horizontal_slash`、`NHorizontalLinesVfx` | Whirlwind、Gamma Blast | 适合明确的全体横斩；可搭配一次全屏横线，但不要每次多段命中都重复生成全屏层。 |
| 星光命中 | `vfx/vfx_starry_impact`，可加 `SpawningHitVfxOnEachCreature()` | Falling Star、Astral Pulse、Dying Star | 最适合 MGR 星空攻击。全体攻击应在每个敌人身上各生成一次。 |
| 小型星弹 | `NSmallMagicMissileVfx` | Comet、Guiding Star | 有飞行过程，适合星星、引导、彗星和单体远程攻击。调用中通常会等待导弹落地。 |
| 大型陨星 | `NLargeMagicMissileVfx` | Meteor Strike、Bombardment、End of Days | 视觉重量大且带等待，适合少量招牌牌，不适合演奏牌或高频多段攻击。 |
| 横向光束 | `NSweepingBeamVfx` | Sweeping Beam | 适合对全体敌人的光线、棱镜、横扫型攻击，可用颜色区分角色主题。 |
| 超级光束 | `NHyperbeamVfx` + `NHyperbeamImpactVfx` | Hyperbeam | 演出很强、实现更重，建议只用于真正的终结技。 |
| 投射物齐射 | `NDaggerSprayFlurryVfx` + `NDaggerSprayImpactVfx` | Dagger Spray、Dagger Throw | 适合多目标、多段、快速齐射；颜色可以自定义。 |
| 爪击 / 撕裂 | `NScratchVfx` | Claw、Maul、Rip and Tear | 适合爪痕、兽性或快速撕裂，目前并不贴合大多数 MGR 卡名。 |
| 火焰 | `NFireBurstVfx`、`NGroundFireVfx`、`NFireBurningVfx` | Cinder、Fiend Fire、Flame Barrier | 可复用，但角色主题关联较弱，应只给明确火焰语义的卡。 |
| 毒液 / 液体 / 污秽 | `NPoisonImpactVfx`、`NGaseousImpactVfx`、`NSplashVfx`、`NGoopyImpactVfx` | Poison、Bouncing Flask、Gunk Up | 可通过着色表现诅咒或黑暗冲击，但不能直接照搬绿色毒液配色。 |
| 凝视 / 咬击 / 血击 | `vfx/vfx_gaze`、`vfx/vfx_bite`、`vfx/vfx_bloody_impact` | Evil Eye、Feed、Hemokinesis | 语义非常明确，只给名称和效果真正匹配的卡。 |
| 全屏预演和终结 | `NGrandFinaleVfx` + `NGrandFinaleImpactVfx` | Grand Finale | 具有明显等待，适合一场战斗偶尔出现的终结牌，不适合演奏自动触发。 |
| 施法 / 能力辅助 | `NPowerUpVfx`、`NSmokyVignetteVfx`、全屏资源 VFX | Inflame、Nightmare、Adrenaline | 主要适合技能和能力牌，也可作为攻击前的短暂铺垫。 |

## MGR 攻击牌分配建议

以下是“视觉语言分类”，并不是要求同组卡使用完全相同的颜色和强度。

### A. 轻型斩击与快速单击

使用普通斩击、细斩或飞行斩击。共同要求是节奏短，不拖慢演奏自动结算。

- `MgrStrike / 打击`：普通斩击。
- `Encore / 安可`：普通斩击，命中后由演奏牌计数动画承担个性。
- `MaguroStrike / 金枪鱼打击`：普通斩击；重复时每次都显示，但缩短间隔。
- `Improvise / 即兴`：细斩 `NThinSliceVfx`，突出快速临场反应。
- `Dazzling / 炫目`：细斩加一次短促金白闪光，升级手牌的动画保持独立。
- `Yaaaaaa / 呀啊啊啊`：飞行斩击，颜色和尺寸可稍微夸张，但不要增加长等待。
- `LightUp / 点亮舞台`：保留现在的普通斩击；它会进入演奏，高频触发时必须轻量。

### B. 重击、巨斩与全体横扫

- `MaguroCleave / 金枪鱼横斩`：从当前普通斩击升级为全体横扫 `vfx_giant_horizontal_slash`；这是最明确的匹配项。
- `MaguroBash / 金枪鱼重击`：保留重型钝击，适合高费用高伤害。
- `MaguroAssault / 金枪鱼强袭`：重型钝击或巨斩；尾音翻倍时增强冲击，不增加额外等待。
- `MaguroReversal / 金枪鱼燕返`：双向巨斩或飞行斩击；重复时第二次反向播放最贴合“燕返”。
- `FlowerFuneral / 花葬`：飞行斩击，配淡紫或花瓣色的短促粒子；不要直接套用火焰或毒液。
- `MaguroDash / 金枪鱼冲锋`：一次横线铺垫 + 每轮全体横斩。结束多张演奏时只加快横斩，不反复播放长前摇。
- `Adios`：戏剧性刺击或巨斩，再由演奏队列自身的爆发动画完成“全体演奏”；不建议直接照搬耗时较长的 `Grand Finale` 前摇。

### C. 枪击与直线投射物

- `FinalShot / 最后一拍`：卡图表现为持枪，因此不使用斩击。原版没有完全适合的通用枪击 VFX，建议采用 MGR 自制的短促枪口闪光、直线弹道和目标命中火花；尾音翻倍时增强枪口与命中亮度，但不把弹道做得更慢。

### D. 星空、陨星与棱镜

- `TinyStarImpact / 幼星冲击`：保留星光命中。
- `MeteorShower / 流☆星☆群`：小型星弹齐射；建议一次生成弹幕，再结算多段伤害，避免每一击单独等待。
- `GuidingStars / 星辰导引`：直接参考原版 `GuidingStar`，使用小型星弹 + 星光命中。
- `Regulus / 轩辕十四`：金色小型星弹或星光刺击。14 段伤害应共享一次发射演出，不生成 14 个带等待的完整导弹。
- `Bird / 「 鳥 」`：大型陨星 + 星光冲击，适合作为少数真正使用大型导弹的 MGR 招牌牌。
- `HeatAbnormal / 热异常`：使用原版战士“自燃”伤害触发时同款 `NFireBurstVfx`；起音使基础伤害翻倍后，火焰尺寸也随当前基础伤害增加。
- `CubicPrism / 立方棱镜`：彩色 `NSweepingBeamVfx`。X 次伤害不应重复播放 X 次完整横扫动画。

### E. 诅咒、凝视与黑暗攻击

- `NightWalker / 夜行少女`：把当前不贴切的星光命中改为暗紫烟雾或气态冲击；整张牌只铺一次暗色幕，每次随机攻击只生成轻量命中。
- `ParanoiaGirl / 被害妄想携带女子`：先播放 `vfx_gaze` 或紫色凝视，再播放一次细斩，符合“先给予易伤，再造成伤害”。
- `GhostRule / 幽灵法则`：每名敌人出现黑红色气态冲击或暗色星光爆发；诅咒音符仍由音符 UI 自己表现。
- `Gaze / 注视`：直接使用 `vfx_gaze`，随后补一个很轻的普通斩击作为实际伤害反馈。

### F. 不直接命中敌人的攻击类型牌

- `EastOfTimeline / 时间线之东`：不造成伤害，只生成攻击音符；使用角色身边的音符脉冲或时间线闪光，不添加敌方命中特效。
- `Finale / 终曲`：不造成伤害，只丢弃手牌并生成大量攻击音符；适合一次短促全屏乐谱爆发，不使用 `NGrandFinaleImpactVfx` 这种敌方伤害特效。

## 技能牌的次要建议

技能和能力牌不需要像攻击牌那样逐张补命中效果，优先处理以下高辨识度动作即可：

- 大量音符生成、替换、还原：使用 MGR 自己的音符槽与线谱动画，不必套原版攻击 VFX。
- 强音、力量、敏捷等强化：可参考 `NPowerUpVfx`，但应改为 MGR 的黄白和彩色音调。
- 诅咒净化、状态转化：可参考烟雾、液体覆盖和全屏暗角的结构，不直接沿用毒绿色。
- 星空牌和星空音符：优先复用星光冲击、星弹和角色周围星座连线，避免每张都使用同一种小星星爆发。
- 选择牌、转化牌、移动牌：继续使用原版卡牌移动动画；这些不属于攻击 VFX，不应被全屏粒子遮挡。

## 推荐实施顺序

1. 先给所有直接造成伤害却没有显式 VFX 的牌补上斩击、钝击或星光命中。这一步风险最低。
2. 再处理《金枪鱼横斩》《流☆星☆群》《星辰导引》《立方棱镜》《幽灵法则》等视觉语义非常明确的牌。
3. 最后处理《Adios》《鳥》等招牌牌，为它们制作组合效果。
4. 所有演奏牌和多段攻击都要限制单次动画等待；特效可以多，但不能让每一段命中都重新播放完整前摇。

## 公共层与动态尺寸原则

`MgrAttackVfx` 应保持为轻量工具箱，而不是一套要求所有卡牌接入的庞大框架：

- 公共层只处理原版 VFX 的安全实例化、实例级颜色、统一缩放公式和最大尺寸限制。
- 只在一张牌出现的独特播放顺序仍放在那张牌内部，不为每张牌制造一个公共方法。
- 动态尺寸使用对数增长：伤害每翻倍，尺寸增加固定幅度，并设置硬上限。这样能看出成长，又不会因无限成长牌遮住整个战场。
- 《金枪鱼横斩》以当前和弦增伤后的卡牌伤害决定横斩尺寸。
- 《热异常》以本场战斗中已经成长的基础伤害决定火焰尺寸。

## 当前接入状态

本轮已经为所有会直接造成伤害、且当前纳入规划的 MGR 攻击牌补上显式 VFX：

- 基础斩击与快速攻击：打击、安可、金枪鱼打击、花葬、呀啊啊啊、即兴、炫目、Adios。
- 金枪鱼系重击与横扫：金枪鱼横斩、金枪鱼重击、金枪鱼强袭、金枪鱼燕返、金枪鱼冲锋。
- 星空与大型演出：幼星冲击、流☆星☆群、星辰导引、轩辕十四、鳥、热异常、立方棱镜。
- 诅咒与凝视：夜行少女、被害妄想携带女子、幽灵法则、注视。
- 《最后一拍》使用代码绘制的枪口闪光、瞬时弹道与命中闪光，不再归入斩击特效。
- 《时间线之东》和《终曲》不直接造成伤害，继续只依赖音符、手牌和乐谱反馈。
- 《随之任之》使用星光冲击作为当前命中特效；其斩杀预览另由目标瞄准卡面反馈承担。

高频多段牌《点亮舞台》《流☆星☆群》《轩辕十四》和《夜行少女》的主体特效被限制为每次出牌只播放一次，伤害仍按原次数完整结算。
