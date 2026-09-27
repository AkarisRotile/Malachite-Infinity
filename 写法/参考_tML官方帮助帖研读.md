# 参考_tML官方帮助帖研读（2026-09-05 · 甄别版）

> 源帖：https://forums.terraria.org/index.php?threads/official-tmodloader-help-thread.28901/
> （Jofairden 主理；2015-07-30 起，共 304 页；主帖最后更新 **2018-09-28**；末页回复 2025-10，仍活动）
> 研读方式：抓取第 1 页（主帖 #1 全文 + 第二帖 #2 Snippets 前半）、第 2 页（2015 问答样本）、第 304 页（现状）。
> **未逐页通读 304 页**——绝大多数为 2015~2018 遗留问答，时代性风险高，逐页学习收益低。
> **甄别纪律（用户指令 2026-09-05）**：只学习仍成立的内容；过时内容一律不学，并登记为反例防后续会话误学。

---

## 一、帖子结构

- 主帖（#1）：Introduction / What is tModLoader / Useful locations and paths / FAQ / Text-Editors & IDEs / Info（sites·guides·utilities·外 mod wiki 导航）。
- 第二帖（#2，Jofairden）：Snippets（velocity 语义、essScale 呼吸光、扇形散布 evenSpread/randomSpread）+ Guides/How-to（Terraria.ID 等）。
- 第 3~304 页：长期 Q&A（2015 至今）。样本与现状见 §四。

## 二、✅ 仍成立、可学习（已并入写法库）

| # | 内容 | 出处 | 现代落地 |
|---|---|---|---|
| 1 | **提问纪律**：必须贴完整报错与代码，否则无法排障 | 主帖 FAQ 前言 | 工作流 R2 门禁文化已同源 |
| 2 | **ExampleMod 优先**：任何「怎么创建 X」先查 ExampleMod | 主帖 FAQ | 现行 tML 仓库 ExampleMod/ |
| 3 | **tile 坐标换算**：position / 16（16px=1 tile），Main.tile[x,y] 前必换算 | 主帖 FAQ（Eldrazi 解释） | 不变 |
| 4 | **velocity 语义**：负 X=左、负 Y=上（原点=世界左上角）；重力=向 +Y 累加 | 第二帖 Snippets | 不变 |
| 5 | **Terraria.ID 精确命名**：ItemID.SoulofFlight（无空格、大小写敏感），用 IntelliSense | p2 #26 | 不变（孔雀翎已守纪律：Vanilla:数字ID） |
| 6 | **扇形弹幕数学**：角度→弧度(×0.0174532925)、Atan2 基角、等分/随机角差 | 第二帖 evenSpread/randomSpread | 现代等价 velocity.RotatedBy(角度)（孔雀翎 Shoot 已在用），数学内核不变 |
| 7 | **MP 意识**：多人问题多源于代码未分客户端/服务器职责 | 主帖 FAQ | 现代权威解=SyncPlayer 三件套 + netMode 门（已入 P9 卡片） |
| 8 | **IL 编辑补引擎行为**：钩子覆盖不到时用 MonoMod IL 插桩（ILContext/ILCursor/GotoNext），如 ItemCheck_Inner 的 allowChannel | p304（2025-10 例） | 现行 tML 仍用 MonoModHooks；论坛引文有拼写错误（PLayer），只取思路 |
| 9 | **存档可视化**：NBTExplorer 可看 ModPlayer/ModWorld 存档 | 主帖 Utilities | 现代 tML 已 TagCompound 化，工具可选非必需 |
| 10 | **社区渠道**：Discord 实时求助 + GitHub wiki 文档 + 官方仓库 | 主帖 | 不变（主帖自指「new documentation」在 GitHub） |

## 三、⚠️ 过时内容（登记反例，禁止学习/照抄）

| 帖子内容 | 过时原因 → 现行替代 |
|---|---|
| XNA .dll 路径 / GAC_32 搜索串 | tML 已用 FNA，无 XNA 依赖 |
| .zip 覆盖安装 tML | 现行 Steam 官方 DLC（免费），无手动覆盖 |
| 「json 不支持，tML 只用 C#」 | 现行本地化/配置用 hjson |
| SetModInfo() 与 AutoLoad 属性 | 1.3 时代 API，已删除（现 Mod 类 + 自动加载） |
| Mac/Linux 需 precompile=true | 现行 dotnet build + tMLMod.targets 全平台一致（孔雀翎 csproj 已如此） |
| tConfig wiki / tAPI docs | 时代遗迹，勿作参考 |
| 0.10 迁移指南 / VS2015 MVS 教程（26476 帖） | 现行：tML GitHub wiki「Guide for Developers」 |
| 「tML is WIP（MP 也是）」 | 已 1.4.x 稳定多年（但多人权威同步仍需开发者自己做好，见 P9） |
| 外 mod wiki 列表（2018 版） | 以各 mod 现行页面为准 |

## 四、研读覆盖说明

- 第 1 页（10 万字抓取窗口）：主帖全文 + 第二帖 Snippets 前半已完整提取；
  第二帖后半（Guides/How-to：Terraria.ID 等）落在抓取窗口外——其内容已被 tML GitHub wiki 与 ExampleMod 完全覆盖，不再单独补抓。
- 第 2 页（样本）：典型早期问答——ID 命名大小写/空格错误、IDE IntelliSense 推荐、贴全报错文化。
- 第 304 页（现状）：帖子仍在活动（2025-10 最新），内容为现代 IL 编辑问答（§二 #8 已吸收）。

## 五、甄别方法（已并入 skill 铁律 8）——「时代性三问」

1. 内容日期 ≥ tML 1.4 时代（2020+）？
2. 涉及的 API 在本机 tML 源码 / 官方 wiki 中存在？
3. 与 ExampleMod 现行写法一致？

任何一条存疑 → 只当历史线索；写码以**本机编译 + 官方 wiki + ExampleMod** 为准。

## 六、结论

- 主帖的**社区方法论 + 通用数学/坐标知识 + 资源导航**仍成立，已全部吸收；
- 主帖的**技术细节停留在 1.3 时代**，学习优先级远低于 GitHub wiki / ExampleMod / 本工作区 skill；
- 本档作为「旧资料反例清单」长期维护：后续会话遇到任何旧教程/旧帖，先查本档 §三。
