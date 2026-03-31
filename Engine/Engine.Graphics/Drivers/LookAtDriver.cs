#nullable disable

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 头部追踪驱动器
    /// </summary>
    public class LookAtDriver : IAnimationDriver
    {
        public string Name => "LookAt";
        public BlendMode BlendMode => BlendMode.Override;

        // 可配置的目标骨骼名称
        private string _targetBoneName = "Head";
        public string TargetBoneName
        {
            get => _targetBoneName;
            set
            {
                _targetBoneName = value;
                _cachedTargetBones = null;
            }
        }

        // IAnimationDriver 接口实现
        public string[] TargetBones => _cachedTargetBones ??= new[] { TargetBoneName };
        private string[] _cachedTargetBones;

        // 配置参数
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";

        // 角度限制（度数）
        public float MinAngleX { get; set; } = -80f;
        public float MaxAngleX { get; set; } = 80f;
        public float MinAngleY { get; set; } = -45f;
        public float MaxAngleY { get; set; } = 45f;

        private float _lookAngleX;
        private float _lookAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _lookAngleX = Math.Clamp(parameters.GetFloat(LookAngleXParam), MinAngleX, MaxAngleX);
            _lookAngleY = Math.Clamp(parameters.GetFloat(LookAngleYParam), MinAngleY, MaxAngleY);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            var targetBone = model.FindBone(TargetBoneName);
            if (targetBone == null) return;

            // 角度转弧度
            float radX = _lookAngleY * MathF.PI / 180f;  // 俯仰
            float radZ = -_lookAngleX * MathF.PI / 180f; // 偏航

            boneTransforms[targetBone.Index] =
                Matrix.CreateRotationX(radX) *
                Matrix.CreateRotationZ(radZ);
        }
    }
}
