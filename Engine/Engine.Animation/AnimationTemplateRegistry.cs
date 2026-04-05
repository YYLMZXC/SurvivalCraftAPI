#nullable disable

namespace Engine.Animation
{
    /// <summary>
    /// 动画模板注册表
    /// </summary>
    public static class AnimationTemplateRegistry
    {
        private static readonly Dictionary<string, AnimationTemplate> _templates = new();

        /// <summary>
        /// 注册动画模板
        /// </summary>
        public static void Register(string name, AnimationTemplate template)
        {
            _templates[name] = template;
        }

        /// <summary>
        /// 获取动画模板
        /// </summary>
        public static AnimationTemplate Get(string name)
        {
            if (_templates.TryGetValue(name, out var template))
                return template;
            return null;
        }

        /// <summary>
        /// 检查模板是否存在
        /// </summary>
        public static bool Exists(string name) => _templates.ContainsKey(name);

        /// <summary>
        /// 初始化内置模板
        /// </summary>
        public static void InitializeBuiltInTemplates()
        {
            // FourLegged 模板（四足动物）
            // Base 层：行走动画（Body + Legs + Head 摆动）
            // Head 层：进食/攻击动画（Head + Neck）
            // Death 层：死亡动画（全身，最高优先级）
            Register("FourLegged", new AnimationTemplate(
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
            Register("Simple", new AnimationTemplate(
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
            Register("Human", new AnimationTemplate(
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
            Register("Bird", new AnimationTemplate(
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
            Register("Fish", new AnimationTemplate(
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
            Register("FlightlessBird", new AnimationTemplate(
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
        }

        static AnimationTemplateRegistry()
        {
            InitializeBuiltInTemplates();
        }
    }
}
