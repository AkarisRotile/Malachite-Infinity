# 阶段 2c · ES 搓招表与近战形态设计（近战招式 v5.2）

> 建档：2026-09-03 ｜ 决策 D17~D20 ｜ 配套代码：TalentModel.cs(MalachiteMelee) / Projectiles\MeleeSlashProj.cs / Players\MalachitePlayer.cs
> 参照：《参考_开源横板动作与挥动弹幕设计.md》——挥动=特殊弹幕（曲线摆姿势+判定盒+VFX 层叠）

## 一、定位与决策记录
- **D17 输入语言**：泰拉瑞亚无格斗键面 → ES 招式适配为「状态 + F 键 + 方向修饰」，宽容判定：修饰只在出招瞬间生效、按序判定优先级（蓄力松手 > 上挑 > 段击）。
- **D18 规模批次**：7 招分两批。批1=地面核心（蓄力重斩/上挑斩/满月终结 + 保留三段段击）；批2=机动/空中（突进追斩加强、空中连段、冲刺飞空微调）。
- **D19 挂靠形态**：继续挂在「手持柳刃 + PrototypeOn」原型下调试；手感确认后再设计为「技」页第一个节点（激活/技能点/伤害并入念曲线）。
- **D20 判定权威**：连段/派生本地判定、各端各自播放（原型期可接受）；多人权威同步列入 M-待办（msgType 后续）。

## 二、招式池（7 招，用户确认的适配版）
| # | 招式 | 输入（地面） | 效果 | 批次 |
|---|---|---|---|---|
| 1 | 三段段击 | F 连点（断链窗口 26 帧） | 三段弧光斩，段 3 小突进；命中叠精准 | 现状(已交付) |
| 2 | 蓄力重斩 | 按住 F ≥16 帧（翠绿光点提示）松手 | 大横斩：刃长 ×1.30、伤害 ×1.5、击退 9 | 批1(已交付) |
| 3 | 突进斩(加强) | 双击方向（无敌帧） | 追击/接续上挑 | 批2 待做 |
| 4 | 上挑斩 | 地面上 + F（且非跳跃键） | 仰弧上挥 + 小跳跃起步；命中把目标挑飞 | 批1(已交付) |
| 5 | 空中连段 | 空中 F 连点 | 滞空段数递减，落地结算精准 | 批2 待做 |
| 6 | 满月终结 | 精准满 5 层 + 蓄力松手 | 巨弧（刃长 ×1.45、伤害 ×2.1、击退 13、额外定格 2 帧）；命中环更大 | 批1(已交付) |
| 7 | 冲刺飞空 | 双击冲刺 + 跳跃衔接 | 高机动（无敌帧） | 批2 待做 |

## 三、输入解析（MalachitePlayer.PreUpdate 近战区）
1. `JustPressed`：记按下时刻；若出招冷却中 → 置单缓冲（`_meleeTapBuffered`），冷却结束自动出招。
2. 按住 ≥ `ChargeFrames(16)` → 进入蓄力态（`_meleeCharged`），蓄力期每 6 帧发翠绿光点提示。
3. `JustReleased`：蓄力态 → `FireHeavy`（精准 ≥ `FinisherMinStacks(5)` 出满月终结，否则蓄力重斩），硬直 `HeavyGapFrames(24)`；否则快速点按 → 段击/上挑（`SwingInterval` 节奏）。
4. 段击修饰：地面上 + 上方向 + 非跳跃键 → 上挑斩；其余走三段连段（`TryMeleeStrike`）。
5. 双击方向 → 突进斩逻辑不变。

## 四、招式生成与弹幕（MalachiteMelee / MeleeSlashProj）
- `MalachiteMelee.MoveKind { Step/Charged/Upper/Finisher }` + 每招弧线（基准朝右，`FrontMirror` 按朝向镜像）/刃长/比例/额外定格，由 `SpawnMove` 生成时写入弹幕字段。
- 弹幕：`Kind != Step` 时用自定义弧线与倍率；`ExtraHold` 扩展满形定格；上挑命中一次把目标挑飞（`target.velocity.Y -= 9`）；满形闪/路径弧光/曳光/击中环按 Kind 放大（Finisher 环 ×1.9、Charged ×1.4）。
- 伤害：所有招式在生成侧已乘 `PrecisionDamageMult`（精准层数）。

## 五、待办/纪律
- 批2（#3/5/7）在批1实机反馈后做；
- 手感 OK 后接入「技」页节点模型（D19）；
- MP 权威同步（D20）列为后续 M 项；
- 调参入口全部集中在 MalachiteMelee（含招式弧线数组与倍率）。

---

## 六、v6.4 更新（2026-09-03，用户工程级 Prompt 落地，见更新日志）
- **枚举升级**：MoveKind 八招式（Step1/Step2/Step3、UpperRise、AirNeutral、AirDive、DashCut、Finisher；Charged 保留为 v5.2 蓄力重斩）。
- **输入判定树（D17 落地版）**：冲刺（dashDelay<0 ∨ 双击突进窗口 ∨ |vel.X|>8 水平疾驰）→ DashCut；空中 下/上/无方向 → AirDive / UpperRise / AirNeutral；地面 上方向 → UpperRise，否则三段 Step1/2/3；蓄力松手 > 方向修饰 > 段击。
- **取消管线（新增）**：目押取消 Gatling（SweepEnd+1~TotalFrames 窗内近战键→无缝下一段）；跳跃取消 JC（Step2 命中卡肉窗内跳跃键→vel.Y=-9.2 同步升空）。
- **空间纹章参数契约矩阵（新增）**：`EsCrestSigilProj.SetupCrest` 配置入口 + `MalachiteMelee.CrestSpecOf` 矩阵唯一出处（八招的外形长宽比/缩放/生成位置/引爆延迟/伤害倍率/命中机制）。
- **断链窗口**：26 → 45 帧（Prompt §一.1）。
- **代码位置**：MalachiteMelee 已迁至 `Core\MalachiteMelee.cs`（原 TalentModel.cs 内）。

### v6.6 补记（2026-09-03）
- **空战三连段**：AirNeutral 退役 → AirStep1/2/3（j.A 斜上 45° 快撩 10 帧 / j.B 水平 180° 大回旋·半空留置雷 / j.C 下劈·命中强制击坠 target.velocity.Y=12）；AirComboStep 流转、落地复位；目押取消覆盖空战链。
- **突刺判定严格化**：DashCut 只看双击 A/D 窗口（LastDashTick ≤ DashImmuneTime），移除移动速度/克苏鲁护盾判定。
- **AirDive 提速**：AirDiveSpeedY 16 → 24（实机反馈）。
- **刀光病灶根除**：DrawCore 删除 MeleeSlash.png 三次 Additive 叠加（死白月牙真凶），形态 100% 由顶点网格呈现；段2 tint 改回翠绿。
- **纹章 2.0 护眼版**：Extra[98]/[89] 五层神圣几何，纯白锁核心 ×0.22，时间轴按各纹章 delay 自适应。

### v6.7 补记（2026-09-03）
- **两段式终结**：满月终结重构为 Finisher1（斜上前撩飞升，SweepEnd 顶点清速冻结 8 帧 + 初阶小单翼 Tier0，后摇按普攻提前派生/结束自动切入）+ Finisher2（斜下流星贯穿 16 帧无敌，终点 10 帧极强卡肉 + 终阶巨单翼 Tier1 + 金色纹章立即全屏引爆 210% 破甲）。
- **新增 EsCrestWingsProj**：双阶单侧神圣光翼（Tier0 4 刃 95px 4 帧弹开 24 帧；Tier1 7 刃 195px 6 帧弹开 48 帧 + 星轨连线/半月圣环），锚定后肩、禁止前翻、超射弹簧曲线。
