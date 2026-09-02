using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ModLoader;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    public class SigilInfo
    {
        public int ID;
        public string NameZh, NameEn;
        public string DescZh, DescEn;
        public string TrackDescZh, TrackDescEn;        
        public string PierceDescZh, PierceDescEn;       
        public int Cost;                
        public Func<bool> UnlockCondition; 
        public string UnlockHintZh, UnlockHintEn;       
        public int[] Conflicts;         

        public string Name => MalachiteData.Loc(NameZh, NameEn);
        public string Desc => MalachiteData.Loc(DescZh, DescEn);
        public string TrackDesc => MalachiteData.Loc(TrackDescZh, TrackDescEn);
        public string PierceDesc => MalachiteData.Loc(PierceDescZh, PierceDescEn);
        public string UnlockHint => MalachiteData.Loc(UnlockHintZh, UnlockHintEn);
    }

    public static class MalachiteTalents
    {
        public static List<SigilInfo> Sigils = new List<SigilInfo>
        {
            new SigilInfo { ID = 0, NameZh = "轻灵", NameEn = "Agility", DescZh = "流派基础强化：", DescEn = "Base Enhancement:", TrackDescZh = "普攻弹幕的初始飞行速度提升80%。", TrackDescEn = "Normal attack projectile speed +80%.", PierceDescZh = "射线无视液体阻力，伤害提升15%。", PierceDescEn = "Beams ignore water physics, damage +15%.", Cost = 2, UnlockCondition = () => NPC.downedSlimeKing, UnlockHintZh = "击败 史莱姆王", UnlockHintEn = "Defeat King Slime", Conflicts = new int[] { } },
            new SigilInfo { ID = 1, NameZh = "巡猎", NameEn = "Hunt", DescZh = "【流派核心】普通攻击的飞刀获得自动追踪能力。", DescEn = "[Core] Normal attacks gain homing capabilities.", TrackDescZh = "", TrackDescEn = "", PierceDescZh = "", PierceDescEn = "", Cost = 4, UnlockCondition = () => NPC.downedBoss1, UnlockHintZh = "击败 克苏鲁之眼", UnlockHintEn = "Defeat Eye of Cthulhu", Conflicts = new int[] { 2 } },
            new SigilInfo { ID = 2, NameZh = "贯穿", NameEn = "Pierce", DescZh = "【流派核心】潜伏攻击变为射线，无视物块。射线每次穿透或命中会使后续伤害递增4%(最高+20%)。", DescEn = "[Core] Stealth attacks become tile-ignoring beams. Each hit/pierce increases subsequent damage by 4% (up to 20%).", TrackDescZh = "", TrackDescEn = "", PierceDescZh = "", PierceDescEn = "", Cost = 4, UnlockCondition = () => NPC.downedBoss2, UnlockHintZh = "击败 邪恶首领", UnlockHintEn = "Defeat Evil Boss", Conflicts = new int[] { 1 } },
            new SigilInfo { ID = 3, NameZh = "蛊毒", NameEn = "Venom", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻飞刀命中时，有概率生成致命的狂暴黄蜂追击敌人。", TrackDescEn = "Normal hits may spawn deadly wasps to chase enemies.", PierceDescZh = "潜伏射线暴击时，在目标位置留下一团滞留的剧毒毒云。", PierceDescEn = "Stealth beam crits leave behind lingering toxic clouds.", Cost = 3, UnlockCondition = () => NPC.downedQueenBee, UnlockHintZh = "击败 蜂王", UnlockHintEn = "Defeat Queen Bee", Conflicts = new int[] { } },
            new SigilInfo { ID = 4, NameZh = "碎骨", NameEn = "Fracture", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普通攻击获得8点固定护甲穿透。", TrackDescEn = "Normal attacks gain 8 flat Armor Penetration.", PierceDescZh = "射线攻击获得20%护甲穿透比例。", PierceDescEn = "Beams gain 20% scaling Armor Penetration.", Cost = 3, UnlockCondition = () => NPC.downedBoss3, UnlockHintZh = "击败 骷髅王", UnlockHintEn = "Defeat Skeletron", Conflicts = new int[] { } },
            new SigilInfo { ID = 5, NameZh = "汲取", NameEn = "Siphon", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻命中时恢复2点生命值 (冷却0.5秒)。", TrackDescEn = "Normal hits heal 2 HP (0.5s CD).", PierceDescZh = "射线命中时恢复25点生命并获得10%减伤，持续4秒 (冷却5秒)。", PierceDescEn = "Beam hits heal 25 HP & grant 10% DR for 4s (5s CD).", Cost = 5, UnlockCondition = () => Main.hardMode, UnlockHintZh = "击败 血肉墙", UnlockHintEn = "Defeat Wall of Flesh", Conflicts = new int[] { } },
            new SigilInfo { ID = 6, NameZh = "冰封", NameEn = "Glacier", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻飞刀附加霜火与冷冻减益。", TrackDescEn = "Normal attacks inflict Frostburn and Chilled.", PierceDescZh = "射线命中时爆发冰霜碎片，造成30%伤害并施加冰河时代。", PierceDescEn = "Beam hits erupt frost shards for 30% damage and inflict Glacial State.", Cost = 4, UnlockCondition = () => CalamityBossDowned.Cryogen(), UnlockHintZh = "击败 极地之灵", UnlockHintEn = "Defeat Cryogen", Conflicts = new int[] { } },
            new SigilInfo { ID = 7, NameZh = "狂热", NameEn = "Zeal", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "武器使用速度提升35%，基础伤害降低10%。", TrackDescEn = "Use speed +35%, Base damage -10%.", PierceDescZh = "潜伏射线伤害倍率增加0.5，武器使用速度降低15%。", PierceDescEn = "Stealth beam damage mult +0.5, Use speed -15%.", Cost = 4, UnlockCondition = () => NPC.downedMechBossAny, UnlockHintZh = "击败 任意机械首领", UnlockHintEn = "Defeat Any Mech Boss", Conflicts = new int[] { } },
            new SigilInfo { ID = 8, NameZh = "森罗", NameEn = "Sylvan", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻暴击时，从目标体内迸发4片狂暴飞叶切割周围敌人。", TrackDescEn = "Normal crits burst 4 razor leaves from the target.", PierceDescZh = "射线沿途绽放落英，命中时爆发花瓣阵并用剧毒藤蔓缠绕目标。", PierceDescEn = "Beams trail blossoms; hits burst petals and entangle targets with venom vines.", Cost = 6, UnlockCondition = () => NPC.downedPlantBoss, UnlockHintZh = "击败 世纪之花", UnlockHintEn = "Defeat Plantera", Conflicts = new int[] { } },
            new SigilInfo { ID = 9, NameZh = "重压", NameEn = "Gravity", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "距离目标越近伤害越高(最高+30%)，且附加碎甲。", TrackDescEn = "Up to +30% damage based on proximity, inflicts armor crush.", PierceDescZh = "射线命中产生引力场，牵引周围普通敌人并大幅减速Boss。", PierceDescEn = "Beam hits create a gravity field, pulling regular enemies and heavily slowing Bosses.", Cost = 4, UnlockCondition = () => NPC.downedGolemBoss, UnlockHintZh = "击败 石巨人", UnlockHintEn = "Defeat Golem", Conflicts = new int[] { } },
            new SigilInfo { ID = 10, NameZh = "爆裂", NameEn = "Detonate", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻暴击附加目标最大生命值0.5%的真实伤害 (对Boss上限60点)。", TrackDescEn = "Normal crits deal 0.5% Max HP true damage (Cap 60 on Bosses).", PierceDescZh = "射线暴击引发半径200像素的瘟疫毒爆，造成150%伤害。", PierceDescEn = "Beam crits trigger a 200-radius plague explosion, dealing 150% damage.", Cost = 7, UnlockCondition = () => ProgressSystem.DownedPlaguebringer, UnlockHintZh = "击败 瘟疫使者歌莉娅", UnlockHintEn = "Defeat Plaguebringer Goliath", Conflicts = new int[] { } },
            new SigilInfo { ID = 11, NameZh = "沧渊", NameEn = "Abyss", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "玩家移动速度越快，普攻伤害越高（最高+25%）。", TrackDescEn = "Up to +25% normal damage based on your movement speed.", PierceDescZh = "射线命中时引发治愈水波，赋予自身极速生命回复。", PierceDescEn = "Beam hits trigger healing ripples, granting Rapid Healing.", Cost = 4, UnlockCondition = () => CalamityBossDowned.Leviathan(), UnlockHintZh = "击败 利维坦与阿娜希塔", UnlockHintEn = "Defeat Leviathan and Anahita", Conflicts = new int[] { } },
            new SigilInfo { ID = 12, NameZh = "星辉", NameEn = "Astral", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻命中时，从玩家位置向目标发射2枚小型幻星碎片。", TrackDescEn = "Normal hits fire 2 small astral bolts from the player to the target.", PierceDescZh = "射线暴击时，在目标位置直接引发星光爆破，造成200%伤害。", PierceDescEn = "Beam crits trigger a direct astral explosion on target, dealing 200% damage.", Cost = 6, UnlockCondition = () => CalamityBossDowned.AstrumDeus(), UnlockHintZh = "击败 星神游龙", UnlockHintEn = "Defeat Astrum Deus", Conflicts = new int[] { } },
            new SigilInfo { ID = 13, NameZh = "月威", NameEn = "Lunar", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻暴击时，在目标上空生成一个月亮传送门倾泻激光（最多存在2个）。", TrackDescEn = "Normal crits summon a moon portal to rain lasers (Max 2).", PierceDescZh = "潜伏射线爆破时，留下持续放电的月耀磁球封锁区域。", PierceDescEn = "Stealth beam hits leave an electrifying moon sphere to lockdown the area.", Cost = 8, UnlockCondition = () => NPC.downedMoonlord, UnlockHintZh = "击败 月亮领主", UnlockHintEn = "Defeat Moon Lord", Conflicts = new int[] { } },
            new SigilInfo { ID = 14, NameZh = "神圣", NameEn = "Divine", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻附加神圣之火。", TrackDescEn = "Normal attacks inflict Holy Flames.", PierceDescZh = "射线附加神圣之火与灵液。", PierceDescEn = "Beams inflict Holy Flames and Ichor.", Cost = 6, UnlockCondition = () => ProgressSystem.DownedProvidence, UnlockHintZh = "击败 亵渎天神", UnlockHintEn = "Defeat Providence", Conflicts = new int[] { } },
            new SigilInfo { ID = 15, NameZh = "噬神", NameEn = "Devour", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "对生命值>80%的敌人，普攻伤害提升25%。", TrackDescEn = "+25% normal damage against enemies with >80% HP.", PierceDescZh = "对生命值<25%的敌人，潜伏射线伤害提升50%。", PierceDescEn = "+50% stealth beam damage against enemies with <25% HP.", Cost = 8, UnlockCondition = () => ProgressSystem.DownedDoG, UnlockHintZh = "击败 神明吞噬者", UnlockHintEn = "Defeat Devourer of Gods", Conflicts = new int[] { } },
            new SigilInfo { ID = 16, NameZh = "龙魂", NameEn = "Dragon", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "连击生成的浮游剑阵数量+1把。", TrackDescEn = "Combo generates +1 floating sword array.", PierceDescZh = "射线命中时，顺着飞行方向扇形折射3道次级射线。", PierceDescEn = "Beam hits refract 3 secondary beams in a fan shape.", Cost = 8, UnlockCondition = () => ProgressSystem.DownedYharon, UnlockHintZh = "击败 丛林龙", UnlockHintEn = "Defeat Yharon", Conflicts = new int[] { } },
            new SigilInfo { ID = 17, NameZh = "智驭万机", NameEn = "Omniscience", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻命中释放连锁闪电，电击周围3个敌人。", TrackDescEn = "Normal hits release chain lightning, striking 3 nearby enemies.", PierceDescZh = "射线护甲穿透+25，施加带电减益。", PierceDescEn = "Beam Armor Penetration +25, inflicts Electrified.", Cost = 10, UnlockCondition = () => ProgressSystem.DownedExoMechs, UnlockHintZh = "击败 星流巨械", UnlockHintEn = "Defeat Exo Mechs", Conflicts = new int[] { } },
            new SigilInfo { ID = 18, NameZh = "心胜于物", NameEn = "Ascendance", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "普攻暴击伤害倍率提升50%。", TrackDescEn = "Normal Crit Damage multiplier +50%.", PierceDescZh = "射线暴击引发半径250像素的渊火爆炸，造成150%范围伤害。", PierceDescEn = "Beam crits trigger a 250-radius brimstone explosion, dealing 150% AoE damage.", Cost = 12, UnlockCondition = () => ProgressSystem.DownedCalamitas, UnlockHintZh = "击败 至尊女巫", UnlockHintEn = "Defeat Supreme Calamitas", Conflicts = new int[] { } },
            new SigilInfo { ID = 19, NameZh = "幽魂", NameEn = "Phantom", DescZh = "流派羁绊强化：", DescEn = "Synergy Enhancement:", TrackDescZh = "攻击时若魔力不低于5点，消耗5点魔力使本次攻击获得15%独立伤害加成。普攻暴击恢复1点魔力。", TrackDescEn = "Consume 5 mana on attack for a 15% independent damage boost. Normal crits restore 1 mana.", PierceDescZh = "射线命中时，撕裂出2个追踪怨灵打击附近敌人，造成30%伤害。", PierceDescEn = "Beam hits tear out 2 homing phantoms to strike nearby enemies, dealing 30% damage.", Cost = 7, UnlockCondition = () => ProgressSystem.DownedPolterghast, UnlockHintZh = "击败 噬魂幽花", UnlockHintEn = "Defeat Polterghast", Conflicts = new int[] { } }
        };

        public static int GetMaxCapacity(int stage) => 5 + stage * 3; 

        public static readonly Vector2[] NodePositions = new Vector2[]
        {
            new Vector2(0, 210), new Vector2(-55, 135), new Vector2(55, 135), new Vector2(0, 35), new Vector2(-125, 95),   
            new Vector2(125, 95), new Vector2(45, 75), new Vector2(-45, 75), new Vector2(-135, 15), new Vector2(135, 15),   
            new Vector2(-175, -60), new Vector2(175, -60), new Vector2(65, -15), new Vector2(-65, -15), new Vector2(0, -65),     
            new Vector2(-60, -115), new Vector2(60, -115), new Vector2(-25, -185), new Vector2(25, -185), new Vector2(0, -140)
        };

        public static readonly (int, int)[] Connections = new (int, int)[] {
            (0, 1), (0, 2), (1, 4), (1, 7), (2, 5), (2, 6), (7, 3), (6, 3), (4, 8), (7, 8),                  
            (5, 9), (6, 9), (8, 10), (8, 13), (9, 11), (9, 12), (3, 14), (13, 14), (12, 14),                
            (13, 15), (12, 16), (14, 15), (14, 16), (15, 17), (16, 18), (17, 18), (14, 19)
        };
    }
}