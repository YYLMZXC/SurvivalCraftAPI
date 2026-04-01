#nullable disable

using Engine.Graphics;
using Game.Animation.Drivers;

namespace Game.Animation
{
    /// <summary>
    /// 动画驱动器注册
    /// 在游戏启动时注册所有内置驱动器类型
    /// </summary>
    public static class AnimationDriverRegistration
    {
        private static bool s_registered = false;
        private static readonly object s_lock = new();

        /// <summary>
        /// 是否已注册
        /// </summary>
        public static bool IsRegistered => s_registered;

        /// <summary>
        /// 注册所有游戏层驱动器
        /// </summary>
        public static void Register()
        {
            lock (s_lock)
            {
                if (s_registered)
                    return;

                // 注册四足动物驱动器
                AnimationDriverManager.Register("FourLeggedWalk", typeof(FourLeggedWalkDriver));
                AnimationDriverManager.Register("FourLeggedWalkDriver", typeof(FourLeggedWalkDriver));

                AnimationDriverManager.Register("FourLeggedTrot", typeof(FourLeggedWalkDriver)); // Trot 使用同一个驱动器
                AnimationDriverManager.Register("FourLeggedCanter", typeof(FourLeggedWalkDriver)); // Canter 使用同一个驱动器

                AnimationDriverManager.Register("FourLeggedAttack", typeof(FourLeggedAttackDriver));
                AnimationDriverManager.Register("FourLeggedAttackDriver", typeof(FourLeggedAttackDriver));

                AnimationDriverManager.Register("FourLeggedDeath", typeof(FourLeggedDeathDriver));
                AnimationDriverManager.Register("FourLeggedDeathDriver", typeof(FourLeggedDeathDriver));

                AnimationDriverManager.Register("FourLeggedFeed", typeof(FourLeggedFeedDriver));
                AnimationDriverManager.Register("FourLeggedFeedDriver", typeof(FourLeggedFeedDriver));

                s_registered = true;
            }
        }

        /// <summary>
        /// 重置注册状态（仅用于测试）
        /// </summary>
        public static void Reset()
        {
            lock (s_lock)
            {
                s_registered = false;
                // 清除 AnimationDriverManager 中的所有注册
                AnimationDriverManager.Clear();
            }
        }
    }
}
