# 资源来源与授权声明 · ASSETS.md

> 本文件**优先于** `LICENSE` 中关于美术资源的表述。
> 一句话：**代码开源（MIT），但立绘等第三方美术资源不开源。**

---

## 一、不开源资源（保留所有权利 · All Rights Reserved）

### 1.1 角色立绘

| 路径 | 说明 | 作者 |
|---|---|---|
| `mod本体/可成长的孔雀翎/Textures/Portraits/normal.png` | 角色立绘 · 平静 | BiliBili [@苗库里](https://space.bilibili.com/) ／ Pixiv [@takeez3](https://www.pixiv.net/users/takeez3) |
| `mod本体/可成长的孔雀翎/Textures/Portraits/angry.png` | 角色立绘 · 生气 | 同上 |
| `mod本体/可成长的孔雀翎/Textures/Portraits/sad.png` | 角色立绘 · 难过 | 同上 |
| `mod本体/可成长的孔雀翎/Textures/Portraits/thinking.png` | 角色立绘 · 思索 | 同上 |
| `mod本体/可成长的孔雀翎/Textures/Portraits/quest.png` | 角色立绘 · 任务 | 同上 |

- **授权状态**：经画师许可，**仅用于本模组**。
- **不在 MIT 授权范围内**。版权归画师所有。
- **禁止**：再分发、二次创作、商用、用于训练数据集、用于其它项目。
- 如需使用，请**直接联系画师**取得许可。

---

## 二、需注意来源的资源

### 2.1 武器贴图（孔雀柳刃）

`Textures/MalachiteItem.png`、`MalachiteItemGame.png`、`MalachiteItemIcon.png`

- 本模组的武器「孔雀柳刃」在**造型与命名**上沿用/致敬灾厄模组的「孔雀翎（Malachite）」。
- 原始 Malachite 武器设计归属：**Calamity Mod Team**。
- 本仓库**不主张**对这些贴图的独立著作权；使用前请自行评估。
- 本项目**未**重新打包或分发灾厄（Calamity）模组本体。

> 第三方**代码**的处理见 **[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md)**；
> 本仓库不包含任何第三方源码。

---

## 三、随 MIT 开源的资源

以下为项目原创或程序化生成，**随源代码一并按 MIT 授权**：

- `Textures/` 下除上述立绘与武器贴图外的全部贴图
  （特效贴图 `特效.png` / `MalachiteProjTex.png` / `MalachiteBoltTex.png` / `SlashTrail.png` /
  `Pixel.png` / `icon*.png` 等；多数由程序生成或为作者自绘）
- `写法/` 下全部设计文档、工作流与门禁脚本

- `mod本体/` 下全部 C# 源码

---

## 四、未包含（已从仓库排除）

本仓库**不包含**以下内容（见 `.gitignore`）：

| 已排除 | 原因 |
|---|---|
| `写法/_ref/灾厄孔雀翎/` | CalamityModPublic 源码，**Azafure, LLC 专有许可**，仅允许作为开发参考，公开转载属二次分发 |
| `写法/_ref/鬼切源码/` | 他人模组源码，未取得再分发许可 |
| `工具/VfxPreview/` | 离线预览台整目录（含由原版 `.xnb` 导出的贴图，版权属 **Re-Logic**；及少量第三方代码移植）。模组本体不依赖它 |
| `mod本体/tModLoader.targets` | tModLoader 自动生成，内含本机绝对路径 |

---

## 五、Terraria 与 tModLoader

- **Terraria** © Re-Logic。本模组是非官方、非商业的粉丝作品，与 Re-Logic 无隶属或背书关系。
- **tModLoader** 为 Terraria 的官方模组加载器，遵循其自身许可。
- 本仓库不重新分发任何 Terraria 游戏本体资源。

---

## 六、若你是权利人

若你是上述任一资源的权利人，认为本仓库的使用方式超出授权范围，
请通过 Issue 或仓库主页所列联系方式告知，我们会立即处理（下架或替换该资源）。
