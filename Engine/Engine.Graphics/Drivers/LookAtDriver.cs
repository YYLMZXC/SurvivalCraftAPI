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

        // 角度限制（弧度）
        public float MaxAngleX { get; set; } = MathUtils.DegToRad(65f);  // 左右
        public float MaxAngleY { get; set; } = MathUtils.DegToRad(55f);  // 上下

        private float _lookAngleX;  // 弧度
        private float _lookAngleY;  // 弧度

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            // 参数是弧度
            _lookAngleX = Math.Clamp(parameters.GetFloat(LookAngleXParam), -MaxAngleX, MaxAngleX);
            _lookAngleY = Math.Clamp(parameters.GetFloat(LookAngleYParam), -MaxAngleY, MaxAngleY);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            var targetBone = model.FindBone(TargetBoneName);
            if (targetBone == null) return;

            // 角度已经是弧度
            // lookAngleY 是俯仰（上下），lookAngleX 是偏航（左右）
            // 原始代码：SetBoneTransform(m_headBone.Index, Matrix.CreateRotationX(vector2.Y) * Matrix.CreateRotationZ(0f - vector2.X));
            boneTransforms[targetBone.Index] =
                Matrix.CreateRotationX(_lookAngleY) *
                Matrix.CreateRotationZ(-_lookAngleX);
        }
    }
}
