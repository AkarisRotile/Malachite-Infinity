# 可成长的孔雀翎 · Malachite Infinity

> 一把会陪你成长的丛林之刃 —— 孔雀柳刃。
> A jungle blade that grows with you — the Peacock Willow Blade.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Terraria](https://img.shields.io/badge/Terraria-tModLoader-blue.svg)](https://www.tmodloader.net/)

---

## 这是什么

一个 **Terraria / tModLoader** 模组。砍树掉落一把会成长的武器「孔雀柳刃」，它沿着贴合原版节奏的
成长曲线（按 Boss 推进逐档变强）陪你走完整场冒险，并配有一套独立的 **「念」伤害体系**、
**「翎羽星网」天赋树** 与一条 **丛林刃灵 × 世纪之花** 的陪伴剧情线。

**V2.0.0 起完全独立**：不再是灾厄（Calamity）的附属模组，不要求安装灾厄。

---

## 玩法速览

| 系统 | 内容 |
|---|---|
| **成长曲线** | `WillowGrowth` 按原版 Boss 梯队 0~9 分档，伤害 / 攻速 / 护甲穿透逐档提升 |
| **「念」伤害类** | 独立伤害类型。通用/召唤加成全额生效，近战/远程/其它模组按 25% 兼容 |
| **翎羽星网** | 单页连通的 36 节点天赋网：翎心（根）+ 20 星尘 + 10 星宿 + 5 星核 |
| **自由构筑** | 新节点必须与已点亮节点相连；全额免费退点；一键洗点。**全解锁需 65 点，而全流程只给约 47 点** |
| **近战形态** | 按 F 进入八招式状态机：地面三连段 / 升龙 / 空战三连 / 陨石下砸 / 突进居合 / 满月终结…… |
| **碧翎念涌** | 按 V 展开一对（默认单翼）跟随你的念羽光翼 |
| **技能点** | Boss 首杀发放（全图玩家有份）+ 事件/里程碑补点 |
| **剧情** | 丛林刃灵 × 世纪之花，含「花下败仗」事件与「月下的坦白」真相线 |

详细设计见 `写法/` 目录下的设计文档（`阶段4_星图3.0_翎羽星网.md` 是天赋系统的实施规格）。

---

## 安装

1. 安装 [tModLoader](https://www.tmodloader.net/)（需 **1.4.4 / 2026 版**或更新）；
2. 下载本仓库后将 `mod本体/可成长的孔雀翎/` 整个目录复制到
   `Documents/My Games/Terraria/tModLoader/ModSources/`；
3. 游戏内 `Workshop → Develop Mods → Build & Reload`。

> 仓库中不含 `tModLoader.targets`（它由 tModLoader 按你的安装路径自动生成，
> 且原文件含作者本机绝对路径）。首次构建时 tML 会自动补齐，无需手动创建。

> 也可以直接从 Steam 创意工坊订阅（若已发布）。

---

## 仓库结构

```
mod本体/可成长的孔雀翎/      模组源码（C#）
  Core/                      系统层：星网数据与逻辑、成长曲线、伤害类、调色板、程序化图元
  Core/Vfx/                  零 Terraria 依赖的绘制层（游戏与离线预览台共用）
  Items/ Projectiles/ Players/ World/ UI/
  Textures/ Localization/
写法/                        设计文档、开发工作流、门禁脚本、AI 调用纪律
  _tools/Mod-Gate.ps1        门禁：编译 + 结构审计
  _tools/ask-agy.mjs         视觉设计咨询调用器
```

---

## 开发

```powershell
# 门禁（编译 + 红线审计）
powershell -NoProfile -File .\写法\_tools\Mod-Gate.ps1

```

接手开发前请先读 `写法/开发工作流.md`（铁律 R1~R9、阶段 A~G、DoD）
与 `写法/当前状态与交接总览.md`。

---

## 授权

- **源代码：MIT** —— 见 [LICENSE](LICENSE)，可自由使用、修改、分发。
- **美术资源：不适用 MIT** —— 角色立绘等第三方资源的权利归原作者，
  **不属于开源内容**，禁止再分发与商用。
    完整清单与出处见 **[ASSETS.md](ASSETS.md)**（该文件优先于 LICENSE 中关于美术资源的表述）。
- **本仓库不包含任何第三方源代码** —— 见 [THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)。

Terraria © Re-Logic。本模组为非官方、非商业的粉丝作品，与 Re-Logic 无隶属关系。

---

## 致谢 · Credits

**开发团队**

| 职责 | 成员 |
|---|---|
| 代码 / 数值 / 文本 | 始弦 |
| Bug 修复 | 哈基米 3.1P |
| 角色立绘 | BiliBili [@苗库里](https://space.bilibili.com/) · Pixiv [@takeez3](https://www.pixiv.net/users/takeez3) |

**思路参考（均为自主实现，未直接复制源码，除注明外）**

| 来源 | 许可 | 参考内容 |
|---|---|---|
| [CalamityModPublic](https://github.com/CalamityTeam/CalamityModPublic) | Azafure, LLC **专有许可** · 仅参考 | Malachite 潜伏齐射/攻速/右键范式、弹幕残影 Dust 范式、挥动状态机思路 |
| [CalamityOverhaul](https://github.com/hocha113/CalamityOverhaul) | MIT, © hocha113 | 三层光带配色、领域展开灵感、OniSlashRenderer 挥动曲线；<br/>`工具/VfxPreview` 中两份文件为其着色器**逐行移植**（已保留版权声明） |
| [CalamityEntropy](https://github.com/hocha113/CalamityEntropy) | 社区开源 | BaseSwing 弧光环带、切线火花等机制参考 |

**原始 Malachite 武器设计归属**：Calamity Mod Team。

> 本模组不重新打包、不分发灾厄（Calamity）模组本体。

---

## 反馈

欢迎把天赋 / 数值 / 手感的任何建议直接开 Issue。
