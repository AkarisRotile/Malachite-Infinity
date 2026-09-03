using System;
using Terraria;
using Terraria.ModLoader;

namespace 可成长的孔雀翎
{
    /// <summary>
    /// 轻量全屏震屏（客户端本地）：借 tML 的 ModSystem.ModifyScreenPosition 每帧平移镜头，强度指数衰减。
    /// 用法：命中/重击时 ScreenShakeSystem.Shake(强度px)。
    /// </summary>
    public class ScreenShakeSystem : ModSystem
    {
        private static float _strength = 0f;

        /// <summary>触发震屏（px 强度，自动衰减）。服务端/菜单静默忽略。</summary>
        public static void Shake(float strength)
        {
            if (Main.dedServ || Main.gameMenu) return;
            _strength = Math.Max(_strength, strength);
        }

        public override void ModifyScreenPosition()
        {
            if (_strength <= 0.08f)
            {
                _strength = 0f;
                return;
            }
            Main.screenPosition += Main.rand.NextVector2Circular(_strength * 0.85f, _strength * 0.85f);
            _strength *= 0.80f;
        }
    }
}