using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    public class 可成长的孔雀翎 : Mod 
    {
        // 模组的数据包接收中心
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            byte msgType = reader.ReadByte(); // 读取消息类型
            
            if (msgType == 1) // 1号指令:同步玩家的孔雀翎天赋和阶段
            {
                byte playerIndex = reader.ReadByte();
                MalachitePlayer mp = Main.player[playerIndex].GetModPlayer<MalachitePlayer>();
                
                // 读取发来的数据
                mp.currentMalachiteStage = reader.ReadInt32();
                int sigilCount = reader.ReadInt32();
                
                mp.ActiveSigils.Clear();
                for (int i = 0; i < sigilCount; i++)
                {
                    mp.ActiveSigils.Add(reader.ReadInt32());
                }

                // 如果主机收到了这个包裹，把包裹转发给其他所有客户端
                if (Main.netMode == NetmodeID.Server)
                {
                    var packet = GetPacket();
                    packet.Write((byte)1);
                    packet.Write(playerIndex);
                    packet.Write(mp.currentMalachiteStage);
                    packet.Write(mp.ActiveSigils.Count);
                    foreach (int sigil in mp.ActiveSigils) 
                    {
                        packet.Write(sigil);
                    }
                    packet.Send(-1, playerIndex); // 发给所有人，除了刚刚发来数据的那个玩家
                }
            }
        }
    }
}