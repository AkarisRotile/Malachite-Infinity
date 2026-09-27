<div align="center">

<img src="docs/icon.png" width="150" alt="Malachite Infinity">

# 可成长的孔雀翎

**Malachite Infinity**

*一把会陪你成长的丛林之刃*

[![License: MIT](https://img.shields.io/badge/License-MIT-3DDC84?style=flat-square)](LICENSE)
[![Terraria](https://img.shields.io/badge/Terraria-tModLoader-4FD8E8?style=flat-square)](https://www.tmodloader.net/)
[![Standalone](https://img.shields.io/badge/不依赖灾厄-Standalone-FFD54F?style=flat-square)](#-兼容性)
[![Assets](https://img.shields.io/badge/美术资源-不开源-E24B4A?style=flat-square)](ASSETS.md)

[简介](#-这是什么) · [玩法](#-玩法) · [安装](#-安装) · [兼容性](#-兼容性) · [开发](#-开发) · [授权](#-授权) · [致谢](#-致谢--credits)

</div>

---

## ✦ 这是什么

砍树掉落一把会成长的武器 —— **孔雀柳刃**。

它不靠合成升级，而是**跟着你的 Boss 进度一起长大**：伤害、攻速、护甲穿透按原版梯队逐档提升，
从史莱姆王到月亮领主，一路陪你走完。

在此之上，模组加了三件"自己的东西"：

| | |
|---|---|
| **「念」伤害体系** | 一个独立的伤害类型，不吃任何职业加成，有自己的成长与兼容规则 |
| **「翎羽星网」天赋树** | 一张连通的 36 节点星网，开放式自由构筑 |
| **一位丛林刃灵** | 一条从初遇到约定的陪伴剧情线，与世纪之花有关 |

> **V2.0.0 起完全独立** —— 不再是灾厄（Calamity）的附属模组，**不需要**安装灾厄，
> 也不会与灾厄自己的「孔雀翎」互相干扰。

---

## ✦ 玩法

### 「念」伤害类

一个独立的伤害类型。加成规则刻意做得"通用"：

| 来源 | 生效比例 |
|---|---|
| 通用 / 召唤加成 | **100%** |
| 近战 / 远程 / 其它模组的任意伤害类 | **25%** |
| 魔法 / 投掷 | 0% |

也就是说：**你穿什么装备都带得动它**，但专门为「念」加点收益最高。

### 「翎羽星网」

单页的**连通星网**，不是五行并排的进度条：

```
                 ★ 星核（流派核心 · 5 点）
                 │
    ✧ 星宿 ──  ✦ 星尘 ── ✦ 星尘 ── ✦ 星尘 ── ◉ 翎心
    （质变）    （属性）  （属性）  （属性）   （根 · 免费）
```

| 节点 | 数量 | 消耗 | 作用 |
|---|:---:|:---:|---|
| ◉ **翎心** | 1 | 免费 | 网的根，初始点亮 |
| ✦ **星尘** | 20 | 1 点 | 小属性：终伤 / 穿甲 / 攻速 / 暴击 / 并发弹幕 |
| ✧ **星宿** | 10 | 2 点 | **质变**：改变机制，而不只是加数字 |
| ★ **星核** | 5 | 5 点 | **流派核心**：一个大招或一种玩法转向 |

**核心规则 —— 必须取舍：**

- 新节点必须**与某个已点亮的节点相连**，像星图一样从中心长出去；
- 全解锁需要 **65 点**，而全 Boss 首杀 + 事件里程碑只给约 **47 点**；
- 你**不可能点满** —— 每一次加点都是一次放弃。

**退点零成本**：右键单点退还、一键洗点全额返还，随便试。

### 近战形态（F 键，可改键）

按近战键进入一套**八招式状态机**：地面三连段 / 升龙 / 空战三连 / 陨石下砸 / 突进居合 /
蓄力重斩与两段式满月终结，支持**目押取消**与**跳跃取消**。

> 这是一个**可选玩法**，不是主攻击。主攻击始终是左键的翎羽飞刀。

### 碧翎念涌（V 键）

展开一扇**跟随你移动**的念羽光翼，持续 5.5 秒。

---

## ✦ 安装

1. 安装 [tModLoader](https://www.tmodloader.net/)；
2. 把 `mod本体/可成长的孔雀翎/` 整个目录放进
   `Documents/My Games/Terraria/tModLoader/ModSources/`；
3. 游戏内 `Workshop → Develop Mods → Build & Reload`。

<details>
<summary>为什么仓库里没有 <code>tModLoader.targets</code>？</summary>

该文件由 tModLoader 按**你本机的安装路径**自动生成（原文件内含开发者本机的绝对路径）。
首次构建时 tML 会自动补齐，不需要手动创建。
</details>

---

## ✦ 兼容性

- ✅ **完全独立**：编译期与运行期**零**灾厄依赖（`using CalamityMod` 计数为 0）。
- ✅ **可与灾厄共存**：同时安装时两者互不干涉。
- ⚠️ **权重提示**：本模组自身很轻，但它的主要受众常常是"全家桶"玩家。
  如果整合包把物理内存打满，游戏会出现**换页卡顿**（表现为输入迟滞、角色"走不动"），
  这通常是内存问题而不是模组 bug —— 先看 `client.log` 里的内存警告。

---

## ✦ 开发

```powershell
# 门禁：编译 + 结构审计（红线扫描）
powershell -NoProfile -File .\写法\_tools\Mod-Gate.ps1
```

接手开发前建议先读：

| 文档 | 内容 |
|---|---|
| `写法/开发工作流.md` | 铁律 R1~R9、阶段 A~G、完成定义（DoD） |
| `写法/当前状态与交接总览.md` | 最浓缩的现状与下一步入口 |
| `写法/阶段4_星图3.0_翎羽星网.md` | 天赋系统的实施规格 |
| `写法/代码结构地图.md` | 逐文件职责与维护坑 |
| `写法/开源参考与合规规范.md` | 外部参考的定级与署名规则 |

**架构上值得一提的一点**：绘制层（`Core/Vfx/`）刻意做成**零 Terraria 依赖** ——
只引用 `Microsoft.Xna.Framework` 的 `SpriteBatch`，全层不出现 `using Terraria.*`。

代价是少量类型需要自己中转（例如顶点结构要定义成中立类型，由两端各自转换），
换来的是**每套特效的几何 / 调色板 / 时间轴常量只有一个出处**：
同一份 `VfxDraw` / `SlashVfx` / `StarWebVfx` 被 UI 面板与弹幕共同调用，
不存在"同一特效在两处各写一份、改了一边另一边悄悄漂移"的情况。

---

## ✦ 授权

| 内容 | 授权 |
|---|---|
| **源代码** | **MIT** —— 见 [LICENSE](LICENSE)，可自由使用、修改、分发 |
| **角色立绘** | **不开源** ❌ 版权归画师，禁止再分发与商用 —— 见 [ASSETS.md](ASSETS.md) |

简单说：**代码随便用，立绘别乱拿。**

> Terraria © Re-Logic。本模组是非官方、非商业的粉丝作品，与 Re-Logic 无隶属或背书关系。

---

## ✦ 致谢 · Credits

| 职责 | 成员 |
|---|---|
| 代码 / 数值 / 文本 | **始弦** · [BiliBili](https://space.bilibili.com/85060308) |
| Bug 修复 | 哈基米 3.1P |
| 角色立绘 | BiliBili [@苗库里](https://space.bilibili.com/152309938) · X [@takeez3](https://x.com/takeez3) |

**思路参考**（均为自主实现，未复制源码）

| 来源 | 许可 | 参考内容 |
|---|---|---|
| [CalamityModPublic](https://github.com/CalamityTeam/CalamityModPublic) | Azafure, LLC 专有 · **仅参考** | Malachite 潜伏齐射 / 攻速 / 右键范式、弹幕残影 Dust 范式、挥动状态机思路 |
| [CalamityOverhaul](https://github.com/hocha113/CalamityOverhaul) | MIT, © hocha113 | 三层光带配色、领域展开灵感、挥动曲线的"爆发过冲 → 回坐"思路 |
| [CalamityEntropy](https://github.com/hocha113/CalamityEntropy) | 社区开源 | BaseSwing 弧光环带、切线火花等机制参考 |

> **原始「Malachite」武器设计归属：Calamity Mod Team。**
> 本模组不重新打包、不分发灾厄模组本体。

---

<div align="center">

**欢迎把天赋 / 数值 / 手感的任何建议直接开 [Issue](https://github.com/AkarisRotile/Malachite-Infinity/issues)。**

<sub>Made with ❤ for Terraria</sub>

</div>
