# 参考_开源mod库研读与模式提炼（2026-09-05 调研）

> 调研方式：web_search / web_fetch / github_file_read 实读 LICENSE 与源码（两个只读调研子代理，未写工作区外任何文件）。
> 合规口径：A=可复用（MIT/BSD/Apache-2.0，保留版权声明与 LICENSE 文本）；G=传染性（GPL/AGPL 系，标红，只读思路禁搬码）；C=仅参考（专有/ARR/无许可，研读+署名+链接，需人工确认）。
> 与既有规范关系：本文档是 `写法\开源参考与合规规范.md` 的调研扩展；执行仍以规范 + 本文档定级为准。

---

## 一、定级总表（实读取证）

| 库 | 仓库 | 许可（实读） | 级别 | 一句话口径 |
|---|---|---|---|---|
| tModLoader（含 ExampleMod） | github.com/tModLoader/tModLoader | MIT（根 LICENSE 1056B） | **A** | 官方示例=权威写法来源，可直接照做/复用 |
| Luminance | github.com/LucilleKarma/Luminance（原 RandomDoggo 已 404） | MIT（Copyright (c) 2026 Lucille） | **A** | 现代框架层最佳参照物（活跃 2026-08） |
| SpiritMod（现任官方仓） | github.com/GabeHasWon/SpiritMod（旧仓 PhoenixBladez 停更且无 LICENSE） | MIT（Copyright (c) 2023 Team Spirit） | **A** | 大型内容 mod 中极少数 MIT 之一，Prim/粒子/UI 可复用 |
| SubworldLibrary | github.com/jjohnsnaill/SubworldLibrary | 无 LICENSE；README 自定义互惠条款 + 非维度用途落 GPL3 | **G** | 只可学模式（ModType 化/门面/数据交接），禁止搬码 |
| TerrariaOverhaul | github.com/Mirsario/TerrariaOverhaul | GPL-3.0（LICENSE.md 34.9KB，构建时自动注入 licenseheader） | **G** | 只读架构（ItemOverhaul/NetPacket），禁复制任何代码 |
| StructureHelper | github.com/ScalarVector1/StructureHelper | 全仓 123 文件无任何许可声明 | **C+需确认** | 工具链模式极实用（资产格式/门面缓存），引用前向 ScalarVector 确认 |
| Wrath of the Gods（原 NoxusBoss） | github.com/TheFifthCircle/WrathOfTheGodsPublic | 无 LICENSE，授权身份需确认 | **C** | 只学思路自实现；依赖 Luminance 需单独核实许可 |
| The Stars Above | github.com/ThePaperLuigi/The-Stars-Above | LICENSE=AGPL-3.0 与 README"个人使用+署名"声明矛盾 | **矛盾，暂不引用** | 排除 |
| StarlightRiver / ThoriumMod | 无官方源码仓（仅第三方反编译镜像） | 无法读 LICENSE | **C** | 仅反编译研读，不搬代码/资产 |

> 生态观察：大型内容 mod 极少宽松许可——候选 8 个仅 ExampleMod、SpiritMod 为 A 级。孔雀翎「抄写法不抄许可」的最安全知识源 = **ExampleMod + SpiritMod**。

---

## 二、A 级可复用模式卡片（可直接照做/复用，保留版权声明）

### P1 自定义伤害类·五维继承权重（ExampleMod/Content/DamageClasses/ExampleDamageClass.cs）
```csharp
if (damageClass == DamageClass.Generic) return StatInheritanceData.Full;
return new StatInheritanceData(damageInheritance: 0f, critChanceInheritance: 0f,
    attackSpeedInheritance: 0f, armorPenInheritance: 0f, knockbackInheritance: 0f);
```
要点：五维各自 0~1 倍率（可负、可 >1）；Generic 必须单独 Full；官方 L26-52 有混合权重反例与无上限警示。
**对照孔雀翎**：MindDamageClass 已同 API 且更细（近远 0.25/召唤 1.0/其余模组类 0.25 + GetPrefixInheritance）。补两点：SetDefaultStats 统一默认白值（现散在武器 SetDefaults）；负权重与 UseStandardCritCalcs=false 留作扩展选项。

### P2 效果/词缀继承 + SetDefaultStats（ExampleDamageClass.cs L55-73）
GetEffectInheritance 白名单控制触发类装备效果（岩浆石/幽灵弹等）；SetDefaultStats 用 GetCritChance<T>()/GetArmorPenetration<T>() 给默认属性。

### P3 武器挂接自定义伤害类（ExampleCustomDamageWeapon.cs）
唯一入口 `Item.DamageType = ModContent.GetInstance<MindDamageClass>()`；官方坑：**无 mana 消耗时除 Deranged 外的魔法前缀不可用**——念系武器前缀池按此复查。

### P4 射击钩子族（ExampleGun.cs）
分层：ModifyShootStats（改 position/velocity/type/damage/knockback）→ Shoot（return false 阻止原版发射，手动 NewProjectile 扇形）→ CanUseItem/UseItem/HoldoutOffset。官方注释自带 6 模板（乌兹换弹/Vampire 扇形/枪口偏移/三连发/双弹种/随机弹种）。

### P5 长枪类投射物复用（ExampleSpear.cs）
`ItemID.Sets.Spears[Type]=true` + noUseGraphic/noMelee + `CanUseItem => player.ownedProjectileCounts[Item.shoot] < 1` + UseItem 内 `Main.dedServ` 守卫播音效。

### P6 Primitive 顶点绘制闭环（SpiritMod/Prim/PrimTrail.cs）
Draw() 四步：按 PointCount 重建 VertexPositionColorTexture[] → PrimStructure 填顶点 → SetShaders → DrawUserPrimitives(TriangleList)；构造时 dedServ 直接 Dispose。配套 PrimTrailManager 半分辨率 RT 像素化 + ITrailShader。
**对照孔雀翎**：AdditiveLayer 之上可叠此类顶点带生成器，把 MalachiteMelee 挥动曲线离散成三角带。

### P7 粒子池三闸门（SpiritMod/Particles/ParticleHandler.cs）
三闸门：netMode==Server 直接丢弃 / 硬上限 500 / 玩家配置开关；反射+FormatterServices 注册全部子类（类名=纹理路径）；按 UseAdditiveBlend 分两批次绘制。
**对照孔雀翎**：EffectLimiterSystem 同思路；可借鉴「预分配数组池+双混合批次」替代逐次 NewDust，SpawnChance 下沉到粒子类型。

### P8 UI 生命周期五件套（ExampleMod/Common/UI/ExampleCoinsUI/）
[Autoload(Side=ModSide.Client)] → PostSetupContent 建 UserInterface+UIState+Activate → UpdateUI 转发 → ModifyInterfaceLayers 插到 "Vanilla: Mouse Text" 前（InterfaceScaleType.UI）→ SetState(state/null) 开关。进阶：SpiritMod UIShaderImage（Immediate+UIScaleMatrix+ScissorTest）。

### P9 服务器权威 ModPlayer 同步三件套（ExampleStatIncreasePlayer.cs + Networking.cs）
①SyncPlayer 写包 Send(toWho, fromWho)；②HandlePacket 按 MessageType 枚举分发，Server 分支再 SyncPlayer(-1, whoAmI, false) 转发；③SendClientChanges/CopyClientState 客户端上报服务器裁决。量大时只发变化量。
**对照孔雀翎**：MalachitePlayer 的 SkillPoints/TrackLevel/SavedDialogGroups 等进多人前补全三件套（D20 波次直接采用此模板）。

### P10~P15 框架层与工具链（Luminance，MIT）
- **类型安全 ModCall**（Core/ModCalls/ModCall.cs）：每个调用 = ILoadable 子类（GetCallCommands/GetInputTypes/SafeProcess），按 mod 名路由 + 参数个数/类型校验，DefaultObject 哨兵代替 null。
- **ManagedShader 参数缓存**（Core/Graphics/Shaders/ManagedShader.cs）：值未变不上传 + 服务器早退 + IDisposable。
- **MemberJit 版本门控**（Core/VersionAwareModJitAttribute.cs）：[JITWhenModsEnabled] 扩展为「mod 存在/版本≥x.y.z 才编译」，弱依赖编译期隔离。
- **Pushdown 状态机**（Common/StateMachines/PushdownAutomata.cs）：转换表+状态栈+TransitionHijack，收敛「if 满天飞」的 AI/技能阶段。
- **Detour/IL 集中注册+统一卸载**（Core/Hooking/HookHelper.cs）：静态注册表 + UnloadHooks 全撤，防 reload 泄漏（现已标 Obsolete 但模式成立）。
- **Shader 目录约定装载**（Core/Graphics/Shaders/ShaderManager.cs）：GetFileNames 扫描 AutoloadedEffects/Shaders + mod 名前缀，零样板注册。

### P16 本地化 hjson 组织惯例（ExampleMod/SpiritMod/WotG）
根键 Mods.{ModName}.*；键由 tML 按代码路径自动生成补新；自定义伤害类必须 DamageClasses.X.DisplayName（孔雀翎已正确）；公共词条放 CommonItemTooltip + {$...} 引用；大模组按类别拆 Localization/{lang}/ 多文件（SpiritMod 14 个 / WotG 26 个）。

### P17 库引用写法实录（供框架工程照抄）
```xml
<ItemGroup>
  <Reference Include="Luminance"><HintPath>ModReferencesLuminance.dll</HintPath></Reference>
</ItemGroup>
```
```
# build.txt
modReferences = CalamityMod, Luminance@1.0.9   # 版本锁定语法
weakReferences = InfernumMode
dllReferences = StructureHelper                # dll 直引（不强制装载）
```
Luminance 发布物 = dll + pdb + xml 三件套（xml 给 IntelliSense）；build.txt 有 includeSource/hideCode 开关（源码随 mod 分发）。

---

## 三、G/C 级「只学模式」卡片（禁搬码，署名+链接）

### G 级：SubworldLibrary（5 条，模式可学代码不可抄）
1. ModType 化扩展点：抽象 Subworld : ModType, ICopyWorldData, ILocalizedModType，sealed 掉 Register/SetupContent 防子类破坏注册（=框架扩展点标准做法）。
2. 静态门面双入口：泛型 Enter<T>()（编译期安全）与字符串 Enter("Mod/Subworld")（跨 mod）并存；查询态（IsActive）与写入态（Enter/Exit）分离；失败 return false 不抛异常。
3. 跨世界数据拷贝协议：ICopyWorldData.CopyMainWorldData()/ReadCopiedMainWorldData() + key→object 字典，接口默认方法全类型自动挂钩。
4. 适配器式跨模组扩展：CrossModSubworld 用只读属性+Action 委托转发，外部 mod 经 Mod.Call 注册而不继承内部类。
5. ServerSide 配置门控重型行为（ConfigScope.ServerSide + bool 开关）。

### G 级：TerrariaOverhaul（只读架构，GPL-3.0 严禁复制）
ItemOverhaul 单选 GlobalItem 组件化、ItemComponent+ConfigEntry 配置驱动、NetPacket 单字节 Id 包协议、sln.licenseheader 构建期自动注入版权头（合规自动化思路可学）。

### C 级：StructureHelper（5 条，用前需作者确认）
1. 静态门面+惰性缓存（mod.Name+path 作缓存键，Unload 清空）。
2. GZip 二进制资产三件套：魔数头+格式 Version 可迁移、modded ID 存 FQN 重映射、文件名冲突自动 (2) 后缀。
3. 多态 TagCompound 序列化：Type 判别字段 + switch 工厂（tML TagCompound 无多态，数据驱动配置范式）。
4. 反射 IoC UI 装配：继承 SmartUIState 即被扫描自动建 UserInterface + 插层。
5. RenderTarget 离线预览队列：保存/恢复旧 RT 绑定、4096 上限裁切、异步生成。

### C 级：Wrath of the Gods（只学思路自实现）
PacketManager.ResendFromServer 语义（客户端只发意图→服务器权威化→重发，防客户端自算伤害作弊）；降采样 RT 粒子批渲染（半分辨率 Matrix.CreateScale(0.5f) 画粒子再 PointClamp 贴回，1/4 填充开销）。

---

## 四、对孔雀翎的直接建议（按优先级）

1. **MindDamageClass**（已成熟）：补 SetDefaultStats 白值 + hjson CommonItemTooltip 公共词条（A 级，可直接照做）。
2. **MalachitePlayer 多人**（D20 波次）：直接采用 P9 三件套（SyncPlayer/HandlePacket 转发/SendClientChanges），招式中断/命中确认用 P10-Packet 的 ResendFromServer 语义。
3. **AdditiveLayer**：叠 SpiritMod PrimTrail 式顶点带生成器（A 级可复用，保留 Team Spirit 版权声明），把挥动曲线离散成三角带。
4. **EffectLimiterSystem**：借鉴 P7 预分配池+双混合批次，预算检查下沉到批量渲染入口。
5. **粒子批渲染**：WotG 降采样 RT 思路（C 级自实现）与 AdditiveLayer 组合；若想直接引 Luminance 库需先核实其独立许可（当前 MIT 判定已实读）。
6. **Overhaul**：仅架构笔记化（ItemOverhaul 组件化/NetPacket/licenseheader 自动化），禁止复制代码。

---

## 五、合规执行细则（配合 开源参考与合规规范.md）

- **A 级复用**：文件头按规范模板署名——`// 参考自：<仓库>（MIT License, Copyright (c) <年份> <版权人>）—— <路径>\n// <仓库链接>`；MIT 需保留版权声明与 LICENSE 文本。
- **G 级**：任何代码搬运 → 整体 GPL 开源（孔雀翎不可接受）；只产出「思路笔记」，笔记同样标注来源。
- **C 级**：仅研读+署名+链接；StructureHelper/WotG 若需引用片段，先向作者确认。
- **矛盾库**（Stars Above AGPL vs README）：暂不引用。
- 每次新增参考仓库 → 同步 description.txt Credits + 本文档定级表 + 文件头署名。

## 六、来源链接清单

- https://github.com/tModLoader/tModLoader（ExampleMod/）
- https://github.com/LucilleKarma/Luminance
- https://github.com/GabeHasWon/SpiritMod
- https://github.com/jjohnsnaill/SubworldLibrary
- https://github.com/Mirsario/TerrariaOverhaul
- https://github.com/ScalarVector1/StructureHelper
- https://github.com/TheFifthCircle/WrathOfTheGodsPublic
