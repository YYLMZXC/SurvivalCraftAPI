#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 动画来源接口 - 关键帧动画和驱动器都实现此接口
    /// </summary>
    public interface IAnimationSource
    {
        /// <summary>
        /// 来源名称
        /// </summary>
        string Name { get; }

        /// <summary>
        /// 更新动画状态
        /// </summary>
        void Update(float deltaTime, AnimationParameters parameters);

        /// <summary>
        /// 采样骨骼变换
        /// </summary>
        void SampleTransforms(Matrix?[] boneTransforms, Model model);
    }

    /// <summary>
    /// 动画配置
    /// </summary>
    public class AnimationSourceConfig
    {
        public string Source { get; set; }
        public float Speed { get; set; } = 1.0f;
        public string SpeedParameter { get; set; }
        public bool Loop { get; set; } = true;
        public float InitialPhase { get; set; } = 0f;
        public float BlendDuration { get; set; } = 0.3f;
        public bool Mirror { get; set; } = false;
        public Dictionary<string, object> DriverArgs { get; set; }
        public List<AnimationEventConfig> Events { get; set; }

        /// <summary>
        /// 骨骼重映射配置 - 用于交换骨骼变换
        /// 例如 {"HandL": "HandR", "HandR": "HandL"} 会交换左右手的变换
        /// </summary>
        public Dictionary<string, string> BoneRemapping { get; set; }
    }

    /// <summary>
    /// 动画事件配置
    /// </summary>
    public class AnimationEventConfig
    {
        public float Time { get; set; }
        public string Name { get; set; }
        public string Data { get; set; }
    }
}
