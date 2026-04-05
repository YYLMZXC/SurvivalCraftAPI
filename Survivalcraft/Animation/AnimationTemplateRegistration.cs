#nullable disable

using Engine.Animation;

namespace Game.Animation
{
    /// <summary>
    /// 动画模板注册
    /// 在游戏启动时注册所有内置模板
    /// </summary>
    public static class AnimationTemplateRegistration
    {
        private static bool s_registered = false;
        private static readonly object s_lock = new();

        /// <summary>
        /// 是否已注册
        /// </summary>
        public static bool IsRegistered => s_registered;

        /// <summary>
        /// 注册所有内置模板
        /// </summary>
        public static void Register()
        {
            lock (s_lock)
            {
                if (s_registered)
                    return;

                // FourLegged 模板（四足动物）
                // Base 层：行走动画（Body + Legs + Head 摆动）
                // Head 层：进食/攻击动画（Head + Neck）
                // Death 层：死亡动画（全身，最高优先级）
                AnimationTemplateManager.Register("FourLegged", new AnimationTemplate(
                    "FourLegged",
                    new LayerDefinition[]
                    {
                        new("Base", 0, AnimationBlendMode.Override),
                        new("Head", 1, AnimationBlendMode.Override, new[] { "Head", "Neck" }),
                        new("Death", 2, AnimationBlendMode.Override)  // 无 targetBones 限制，影响全身
                    },
                    new StateTrackDefinition[]
                    {
                        new("Gait", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle", "Walk", "Trot", "Canter" } },
                        new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Feed", "Attack" } },
                        new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                    }
                ));

                // Simple 模板（用于简单模型）
                AnimationTemplateManager.Register("Simple", new AnimationTemplate(
                    "Simple",
                    new LayerDefinition[]
                    {
                        new("Base", 0, AnimationBlendMode.Override)
                    },
                    new StateTrackDefinition[]
                    {
                        new("Gait", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle" } }
                    }
                ));

                // Human 模板（玩家）
                AnimationTemplateManager.Register("Human", new AnimationTemplate(
                    "Human",
                    new LayerDefinition[]
                    {
                        new("Base", 0, AnimationBlendMode.Override),
                        new("Activity", 1, AnimationBlendMode.Override, new[] { "Hand1", "Hand2" }),
                        new("Ride", 2, AnimationBlendMode.Override),
                        new("Death", 3, AnimationBlendMode.Override)
                    },
                    new StateTrackDefinition[]
                    {
                        new("Locomotion", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle", "Walk", "Fly" } },
                        new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Attack", "Aim" } },
                        new("Ride", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Riding" } },
                        new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                    }
                ));

                // Bird 模板（鸟类）
                AnimationTemplateManager.Register("Bird", new AnimationTemplate(
                    "Bird",
                    new LayerDefinition[]
                    {
                        new("Base", 0, AnimationBlendMode.Override),
                        new("Head", 1, AnimationBlendMode.Override, new[] { "Head", "Neck" }),
                        new("Death", 2, AnimationBlendMode.Override)
                    },
                    new StateTrackDefinition[]
                    {
                        new("Locomotion", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle", "Walk", "Fly" } },
                        new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Peck", "Attack" } },
                        new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                    }
                ));

                // Fish 模板（鱼类）
                AnimationTemplateManager.Register("Fish", new AnimationTemplate(
                    "Fish",
                    new LayerDefinition[]
                    {
                        new("Base", 0, AnimationBlendMode.Override),
                        new("Head", 1, AnimationBlendMode.Override, new[] { "Jaw" }),
                        new("Death", 2, AnimationBlendMode.Override)
                    },
                    new StateTrackDefinition[]
                    {
                        new("Swim", StateTrackType.Float, 0f),
                        new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Bite" } },
                        new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                    }
                ));

                // FlightlessBird 模板（不能飞的鸟）
                AnimationTemplateManager.Register("FlightlessBird", new AnimationTemplate(
                    "FlightlessBird",
                    new LayerDefinition[]
                    {
                        new("Base", 0, AnimationBlendMode.Override),
                        new("Head", 1, AnimationBlendMode.Override, new[] { "Head", "Neck" }),
                        new("Death", 2, AnimationBlendMode.Override)
                    },
                    new StateTrackDefinition[]
                    {
                        new("Locomotion", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle", "Walk" } },
                        new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Feed", "Attack" } },
                        new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                    }
                ));

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
                // 清除 AnimationTemplateManager 中的所有注册
                AnimationTemplateManager.Clear();
            }
        }
    }
}
