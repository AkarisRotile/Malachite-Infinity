using System;
using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.DataStructures;
using Microsoft.Xna.Framework;

namespace 可成长的孔雀翎
{
    public class MalachiteGlobalNPC : GlobalNPC
    {
        // 记录所有 Boss 击杀（任意模组），供进度系统与未来的技能点体系使用
        public override void OnKill(NPC npc)
        {
            if (npc.boss && !Main.dedServ)
            {
                MalachiteProgress.Instance.RegisterDefeat(npc);
            }
        }

        public override void PostAI(NPC npc)
        {
            if (!ProgressSystem.DownedPlaguebringer)
            {
                int plagueBuff = MalachiteCache.PlagueBuff;
                if (plagueBuff != 0 && npc.HasBuff(plagueBuff))
                {
                    bool anyMalachite = false;
                    for (int i = 0; i < Main.maxPlayers; i++)
                    {
                        if (Main.player[i].active && !Main.player[i].dead && MalachiteCache.IsMalachiteItem(Main.player[i].HeldItem))
                        {
                            anyMalachite = true;
                            break;
                        }
                    }

                    if (anyMalachite)
                    {
                        int buffIndex = npc.FindBuffIndex(plagueBuff);
                        if (buffIndex != -1)
                        {
                            npc.DelBuff(buffIndex);
                            npc.AddBuff(BuffID.Poisoned, 180);
                        }
                    }
                }
            }
        }
    }

    public class TreeDropModifier : GlobalTile
    {
        public override void Drop(int i, int j, int type)
        {
            if (TileID.Sets.IsATreeTrunk[type] || type == TileID.Trees || type == TileID.PalmTree)
            {
                int playerIndex = Player.FindClosest(new Vector2(i * 16, j * 16), 16, 16);
                if (playerIndex != -1)
                {
                    Player closestPlayer = Main.player[playerIndex];
                    if (closestPlayer.active && !closestPlayer.dead && closestPlayer.DistanceSQ(new Vector2(i * 16, j * 16)) < 800f * 800f)
                    {
                        MalachitePlayer modPlayer = closestPlayer.GetModPlayer<MalachitePlayer>();
                        int malachiteType = MalachiteCache.NativeMalachiteItem;

                        bool itemInWorld = false;
                        for (int k = 0; k < Main.maxItems; k++)
                        {
                            if (Main.item[k].active && Main.item[k].type == malachiteType)
                            {
                                itemInWorld = true;
                                break;
                            }
                        }
                        
                        bool hasItemAnywhere = closestPlayer.HasItem(malachiteType);
                        
                        if (malachiteType != 0 && !hasItemAnywhere && !itemInWorld && modPlayer.treeDropCooldown <= 0)
                        {
                            modPlayer.treeDropCooldown = 60; 
                            closestPlayer.QuickSpawnItem(new EntitySource_TileBreak(i, j), malachiteType);
                            
                            if (!modPlayer.hasObtainedMalachite)
                            {
                                modPlayer.hasObtainedMalachite = true;
                                modPlayer.currentMalachiteStage = PeacockModifier.GetMalachiteStage();
                                modPlayer.hasGreetedLogin = true;
                                modPlayer.dailyChatTimer = 18000;
                                
                                if (closestPlayer.whoAmI == Main.myPlayer && !Main.dedServ)
                                {
                                    Main.NewText(MalachiteData.Loc("你从茂密的树叶间发现了一把神秘的孔雀翎...", "You found a mysterious Malachite hidden among the dense leaves..."), MalachitePalette.PrimaryGreen);
                                    if (MalachiteData.IsEnglish)
                                    {
                                        MalachiteUISystem.ShowDialog(new List<(int, string)> {
                                            (2, "Who... awakened me?"),
                                            (0, "Hello there, traveler..."),
                                            (1, "My body was shattered in the jungle catastrophe of the past."),
                                            (2, "Would you... be willing to take me with you?")
                                        });
                                    }
                                    else
                                    {
                                        MalachiteUISystem.ShowDialog(new List<(int, string)> {
                                            (2, "是谁...唤醒了我？"),
                                            (0, "你好呀，旅人..."),
                                            (1, "我的躯体在过去那场丛林的浩劫中破碎了。"),
                                            (2, "你...愿意带着我一起走吗？")
                                        });
                                    }
                                }
                            }
                            else
                            {
                                if (closestPlayer.whoAmI == Main.myPlayer && !Main.dedServ)
                                {
                                    Main.NewText(MalachiteData.Loc("树叶沙沙作响，孔雀翎重新落回了你的手中。", "The leaves rustle, and Malachite falls back into your hands."), MalachitePalette.PrimaryGreen);
                                    if (MalachiteData.IsEnglish)
                                    {
                                        MalachiteUISystem.ShowDialog(new List<(int, string)> {
                                            (1, "I finally found you again..."),
                                            (2, "Please don't lose me anymore, I will always stay by your side.")
                                        });
                                    }
                                    else
                                    {
                                        MalachiteUISystem.ShowDialog(new List<(int, string)> {
                                            (1, "终于又找到你了..."),
                                            (2, "请不要再把我弄丢了哦，我会一直陪着你的。")
                                        });
                                    }
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}