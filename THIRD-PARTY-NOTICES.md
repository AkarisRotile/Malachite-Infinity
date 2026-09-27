# 第三方代码声明 · THIRD-PARTY NOTICES

## 结论（一句话）

**本仓库不包含任何第三方源码。** 全部 C# 代码均为本项目自主实现。

---

## 一、已做的核验

### 1.1 逐行比对

开发过程中曾将若干公开模组的源码下载到本地 `写法/_ref/` 用于研读。
发布前把本项目全部 C# 源码与这些参考源码做了**逐行比对**，结果：

> 命中的重合行**全部是 tModLoader 的框架方法签名**
> （如 `public override bool PreDraw(ref Color lightColor)`、
> `public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)`、
> `ProjectileID.Sets.TrailingMode[Type] = 0;` 这类任何模组都完全相同的行），
> **不存在任何一行重合的实现逻辑。**

### 1.2 已从仓库排除的目录

| 已排除 | 内容 | 原因 |
|---|---|---|
| `写法/_ref/` | 第三方模组源码（含 24 份逐行注释复刻笔记） | 见下节 |
| `工具/VfxPreview/` | 离线特效预览台（开发工具，模组本体不依赖） | 其中两个文件为 CalamityOverhaul 的逐行移植 |
| `工具/VfxPreview/out/vanilla/` | 由原版 `.xnb` 导出的贴图 | 版权属 **Re-Logic** |
| `mod本体/tModLoader.targets` | tModLoader 自动生成的构建入口 | 内含本机绝对路径 |

以上均由 `.gitignore` 排除，**不在本仓库中**。

---

## 二、仅参考思路、未复制代码的来源

| 项目 | 许可 | 参考内容 |
|---|---|---|
| [CalamityModPublic](https://github.com/CalamityTeam/CalamityModPublic) | **Azafure, LLC 专有许可**（官方明确允许将源码作为泰拉瑞亚模组开发的参考） | Malachite 潜伏齐射 / 攻速 / 右键范式、弹幕残影 Dust 范式、挥动状态机思路 |
| [CalamityOverhaul](https://github.com/hocha113/CalamityOverhaul) | MIT, © hocha113 | 三层光带配色、领域展开灵感、OniSlashRenderer 的"爆发过冲→回坐"挥动曲线与弧光带宽思路 |
| [CalamityEntropy](https://github.com/hocha113/CalamityEntropy) | 社区开源 | BaseSwing 弧光环带、切线火花等机制参考 |

本项目**未**重新打包、**未**分发灾厄（Calamity）模组本体，
也**未**包含 `CalamityModPublic` 的任何源码（其为专有许可，仅授权"参考"用途，公开转载属于二次分发）。

---

## 三、关于被排除的 `工具/VfxPreview/`

该目录是本项目自用的**离线特效预览台**（用 tModLoader 自带 FNA 离屏渲染特效为 PNG，不必启动游戏）。
其中两个文件是 CalamityOverhaul（MIT, © hocha113）的着色器 / 渲染器**逐行移植**：

- `OniGateRiftCpu.cs` ← `Assets/Effects/OniGateRift.fx`
- `OniRiftRepro.cs` ← `Content/LegendWeapon/OnikiriLegend/OniSlashs/OniSlashRenderer.cs`

**这在 MIT 下是合规的**（MIT 允许修改与再发布，只要保留版权声明 —— 这两个文件的文件头已保留）。
本项目仍选择**整目录不发布**，以做到版权上零争议。该工具不参与模组本体编译，也不进入发布的 `.tmod`。

---

## 四、Terraria 与 tModLoader

- **Terraria** © Re-Logic。本模组是非官方、非商业的粉丝作品，与 Re-Logic 无隶属或背书关系。
- **tModLoader** 为 Terraria 的官方模组加载器。
- 本仓库不重新分发任何 Terraria 游戏本体资源。

---

## 五、若你是权利人

若你是上述任一项目的作者，认为本仓库的使用方式超出授权范围，
请通过 Issue 告知，我们会立即处理。
