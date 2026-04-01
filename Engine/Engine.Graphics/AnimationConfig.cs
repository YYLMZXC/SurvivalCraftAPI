#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 动画引用配置
    /// </summary>
    public class AnimationReference
    {
        /// <summary>
        /// 动画来源（animation://名称、driver:驱动器名 或 文件路径）
        /// </summary>
        public string Source { get; set; }

        /// <summary>
        /// 播放速度倍率
        /// </summary>
        public float Speed { get; set; } = 1f;

        /// <summary>
        /// 是否循环
        /// </summary>
        public bool Loop { get; set; } = true;

        /// <summary>
        /// 初始相位 (0-1)
        /// </summary>
        public float InitialPhase { get; set; } = 0f;

        /// <summary>
        /// 过渡时长（秒）
        /// </summary>
        public float BlendDuration { get; set; } = 0.3f;

        /// <summary>
        /// 驱动器参数（当 Source 为 driver: 时使用）
        /// </summary>
        public Dictionary<string, object> DriverArgs { get; set; }
    }

    /// <summary>
    /// 层配置
    /// </summary>
    public class LayerConfig
    {
        /// <summary>
        /// 混合模式：override 或 additive
        /// </summary>
        public string BlendMode { get; set; } = "override";

        /// <summary>
        /// 影响的骨骼名称列表
        /// </summary>
        public string[] Bones { get; set; }

        /// <summary>
        /// 层驱动器配置
        /// </summary>
        public DriverConfig Driver { get; set; }
    }

    /// <summary>
    /// 状态规则配置（新格式）
    /// </summary>
    public class StateRuleConfig
    {
        /// <summary>
        /// 条件表达式（NCalc 语法）
        /// </summary>
        public string Condition { get; set; }

        /// <summary>
        /// 目标动画配置
        /// </summary>
        public AnimationReference Animation { get; set; }
    }

    /// <summary>
    /// 状态轨道配置（新格式）
    /// </summary>
    public class StateTrackConfig
    {
        /// <summary>
        /// 所属层名称
        /// </summary>
        public string Layer { get; set; }

        /// <summary>
        /// 规则列表
        /// </summary>
        public List<StateRuleConfig> Rules { get; set; } = new();
    }

    /// <summary>
    /// 驱动器配置
    /// </summary>
    public class DriverConfig
    {
        public string Type { get; set; }
        public Dictionary<string, object> Properties { get; set; }
    }

    /// <summary>
    /// 动画配置
    /// </summary>
    public class AnimationConfig
    {
        /// <summary>
        /// 模板名称
        /// </summary>
        public string Template { get; set; } = "Simple";

        /// <summary>
        /// 层配置
        /// </summary>
        public Dictionary<string, LayerConfig> Layers { get; set; } = new();

        /// <summary>
        /// 状态配置
        /// </summary>
        public Dictionary<string, StateTrackConfig> States { get; set; } = new();

        /// <summary>
        /// 动画引用映射（别名 -> 引用）
        /// </summary>
        public Dictionary<string, AnimationReference> Animations { get; set; } = new();

        /// <summary>
        /// 初始参数值
        /// </summary>
        public Dictionary<string, object> Parameters { get; set; } = new();

        /// <summary>
        /// 从 JSON 文件加载配置
        /// </summary>
        public static AnimationConfig LoadFromJson(string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<AnimationConfig>(json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    ReadCommentHandling = System.Text.Json.JsonCommentHandling.Skip
                });
        }

        /// <summary>
        /// 从文件加载配置
        /// </summary>
        public static AnimationConfig LoadFromFile(string path)
        {
            var json = File.ReadAllText(path);
            return LoadFromJson(json);
        }
    }
}
