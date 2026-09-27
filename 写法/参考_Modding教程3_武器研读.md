# 参考_Modding教程3_武器研读（2026-09-05 · 甄别版）

> 源帖：https://forums.terraria.org/index.php?threads/modding-tutorial-3-some-more-weapons.135285/
> （Lanugare 系列教程；本帖 2024-04-30 起，4 页，最新回复 2026-02，仍在活动；系列链接：Tutorial 2 = Basic Item Sword，thread 120094）
> 时代性：2024+ 属 tML 1.4 时代，整体可信；仍逐条过「时代性三问」并登记勘误（用户指令：过时内容不学，注意分辨）。
> 研读范围：p1 枪械段全文 + p2~p4 评论问答；**法杖段（custom projectile）落在抓取窗口外未捕获**——其内容已被 skill P4/P5 与 ExampleMod 完全覆盖，不单独补抓。

---

## 一、✅ 仍成立、已吸收（并入写法库）

| # | 知识点 | 教程写法 | 判定 |
|---|---|---|---|
| 1 | 三连发（Clockwork 式） | useTime = useAnimation/3 + consumeAmmoOnLastShotOnly=true + reuseDelay | ✅ 标准模式（P4 的完整实例） |
| 2 | Shoot 钩子签名 | Shoot(Player, EntitySource_ItemUse_WithAmmo, Vector2, Vector2, int, int, float)——无 Item 参数 | ✅ 与孔雀翎 TML_2026_07 一致 |
| 3 | 散布 | velocity.RotatedByRandom(MathHelper.ToRadians(10)) | ✅ |
| 4 | 随机弹数 | Main.rand.Next(1, 4) 上界不含（1~3） | ✅ |
| 5 | 生成弹幕 | Projectile.NewProjectileDirect(source, position, velocity, type, damage, knockback, player.whoAmI)——返回 Projectile 可直接改 | ✅ 现代写法 |
| 6 | 弹药节约 | CanConsumeAmmo(Item, Player)——**返回 true = 消耗** | ✅（方向见勘误 1） |
| 7 | 持握偏移 | HoldoutOffset() => new Vector2(-8f, 0f)（-Y 为上） | ✅ |
| 8 | 配方 | CreateRecipe().AddIngredient<T>(9).AddTile(TileID.Anvils).Register() | ✅ |
| 9 | 评论佐证 | Item.crit 默认 4%（设 0 进游戏也是 4%） | ✅（与 2026-09-05 暴击口径修复互证） |
| 10 | 评论补充 | 命中上减益：OnHitNPC 内 target.AddBuff(BuffID.CursedInferno, 120)（60 tick=1 秒） | ✅ |
| 11 | 评论补充 | 枪口出弹偏移：官方模板见 ExampleGun.cs | ✅（P4 同源） |

## 二、⚠️ 教程瑕疵与勘误（禁止照抄）

1. **CanConsumeAmmo 注释方向写反**：教程代码 `Main.rand.Next(101) <= 33` 实际是 **33% 消耗 / 67% 不消耗**（返回 true=消耗）；若想要「33% 不消耗」（Minishark 语义）应写 `Main.rand.Next(101) > 33`。
2. **1/5 换银弹无条件替换**：原码对任何弹种都 1/5 换成银弹；作者 #23 自勘——应限定 `if (type == ProjectileID.Bullet) type = ProjectileID.SilverBullet;`。
3. **consumeAmmoOnLastShotOnly × CanConsumeAmmo 组合异常**（作者 #41 自认；#42 提议 `player.itemAnimation <= player.itemTimeMax && …` 修复被 #43 反馈无效，楼内未解决）→ 孔雀翎若做「三连发 + 节弹」，用 ExampleMod 官方模板并游戏内实测，**不抄该线程方案**。
4. 评论中 ModNPC.FindFrame 示例缺帧计数器（帧推进写法不完整）→ 以 ExampleMod/官方 wiki 为准。

## 三、系列信息

- Tutorial 2：Basic Item Sword（thread 120094，帖内链接）——后续可研读；
- Tutorial 1 / 4+：本帖未链接，待发现。

## 四、结论

- 作者风格：注释详细、紧跟现代 API（EntitySource / NewProjectileDirect / 无 Item 参数签名）——**可作入门向参考**；
- 两处逻辑瑕疵 + 一处未解决问题 → 照抄前必过 skill 铁律 + 门禁；**权威仍以 ExampleMod + 官方 wiki 为准**；
- 本档按「旧资料反例清单」同款纪律维护：后续会话遇到该系列其它教程先查本档风格。
