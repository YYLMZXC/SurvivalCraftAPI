#nullable disable

namespace Engine.Graphics
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
            // Humanoid 模板
            Register("Humanoid", new AnimationTemplate(
                "Humanoid",
                new LayerDefinition[]
                {
                    new("Base", 0, BlendMode.Override),
                    new("UpperBody", 1, BlendMode.Additive, new[] { "Body", "Arm1", "Arm2" }),
                    new("Head", 2, BlendMode.Override, new[] { "Head" })
                },
                new StateTrackDefinition[]
                {
                    new("Gait", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle", "Walk", "Run" } },
                    new("Posture", StateTrackType.Enum, "Standing") { EnumValues = new[] { "Standing", "Crouching", "Lying" } },
                    new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Attack", "Row", "Eat" } },
                    new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                },
                new BuiltInDriverDefinition[]
                {
                    new("LookAt", "Head")
                }
            ));

            // FourLegged 模板
            Register("FourLegged", new AnimationTemplate(
                "FourLegged",
                new LayerDefinition[]
                {
                    new("Base", 0, BlendMode.Override),
                    new("UpperBody", 1, BlendMode.Additive, new[] { "Neck", "Head" }),
                    new("Head", 2, BlendMode.Override, new[] { "Head" })
                },
                new StateTrackDefinition[]
                {
                    new("Gait", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle", "Walk", "Trot", "Canter" } },
                    new("Activity", StateTrackType.Enum, "None") { EnumValues = new[] { "None", "Feed", "Attack" } },
                    new("Death", StateTrackType.Float, 0f) { MinValue = 0f, MaxValue = 1f }
                },
                new BuiltInDriverDefinition[]
                {
                    new("LookAt", "Head")
                }
            ));

            // Simple 模板（用于简单模型）
            Register("Simple", new AnimationTemplate(
                "Simple",
                new LayerDefinition[]
                {
                    new("Base", 0, BlendMode.Override)
                },
                new StateTrackDefinition[]
                {
                    new("Gait", StateTrackType.Enum, "Idle") { EnumValues = new[] { "Idle" } }
                }
            ));
        }

        static AnimationTemplateRegistry()
        {
            InitializeBuiltInTemplates();
        }
    }
}
