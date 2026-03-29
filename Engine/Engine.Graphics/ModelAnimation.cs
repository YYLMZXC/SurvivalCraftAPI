namespace Engine.Graphics {
    /// <summary>
    /// 动画数据
    /// </summary>
    public class ModelAnimation {
        public string Name { get; set; } = string.Empty;
        public float Duration { get; set; }
        public List<AnimationChannel> Channels { get; set; } = [];

        /// <summary>
        /// 动画通道，对应一个骨骼的某个属性
        /// </summary>
        public class AnimationChannel {
            public string TargetBoneName { get; set; } = string.Empty;
            public AnimationProperty Property { get; set; }
            public AnimationSampler Sampler { get; set; } = new();
        }

        public enum AnimationProperty {
            Translation,
            Rotation,
            Scale,
            Weights
        }

        /// <summary>
        /// 动画采样器
        /// </summary>
        public class AnimationSampler {
            public float[] KeyTimes { get; set; } = [];
            public Vector3[] Translations { get; set; } = [];
            public Quaternion[] Rotations { get; set; } = [];
            public Vector3[] Scales { get; set; } = [];
            public InterpolationType Interpolation { get; set; }
        }

        public enum InterpolationType {
            Step,
            Linear,
            CubicSpline
        }
    }
}
