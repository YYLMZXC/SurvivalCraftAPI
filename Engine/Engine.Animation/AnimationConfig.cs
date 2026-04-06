#nullable disable

namespace Engine.Animation
{
    /// <summary>
    /// 动画完成时执行的动作
    /// </summary>
    public class OnCompleteAction
    {
        /// <summary>
        /// 动作类型：setState 或 trigger
        /// </summary>
        public string Type { get; set; }

        /// <summary>
        /// 目标状态轨道名称（setState 使用）
        /// </summary>
        public string State { get; set; }

        /// <summary>
        /// 要设置的值（setState 使用）
        /// </summary>
        public object Value { get; set; }

        /// <summary>
        /// 事件名称（trigger 使用）
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 事件数据（trigger 使用）
        /// </summary>
        public object Data { get; set; }
    }

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

        /// <summary>
        /// 动画完成时执行的动作（非循环动画）
        /// </summary>
        public OnCompleteAction OnComplete { get; set; }
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
        /// 根骨骼旋转角度（度），用于修正模型朝向
        /// 某些 glTF 模型的前方方向可能与游戏不一致，需要旋转修正
        /// 例如：Fox 模型的前方是 +X，需要旋转 90 度使其面向 +Z
        /// </summary>
        public float RootBoneRotation { get; set; } = 0f;

        /// <summary>
        /// 模型缩放比例
        /// 用于调整模型大小，例如：
        /// - 0.01：厘米单位模型（Fox 等从小型建模软件导出的模型）
        /// - 1.0：米单位模型（默认，大多数 glTF 模型）
        /// - 10.0：放大 10 倍（大型建筑模型可能需要）
        /// </summary>
        public float ModelScale { get; set; } = 1f;

        /// <summary>
        /// 层配置（在此配置驱动器及其属性）
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
    }
}
