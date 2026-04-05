#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 人类瞄准驱动器 - 处理瞄准时手臂抬起动画
    /// </summary>
    public class HumanAimDriver : IAnimationDriver
    {
        public string Name => "HumanAim";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Additive;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Hand1", "Hand2" };

        // 参数名称
        public string AimHandAngleParam { get; set; } = "AimHandAngle";

        // 可配置属性
        public float AimAngleMultiplier { get; set; } = 1.5f;
        public float AimAngleY { get; set; } = -0.7f;
        public float SmoothSpeed { get; set; } = 12f;

        private float _aimHandAngle;
        private float _currentAimAngle = 0f;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _aimHandAngle = parameters.GetFloat(AimHandAngleParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_aimHandAngle == 0f) return;

            // 平滑过渡
            float smoothFactor = MathUtils.Min(SmoothSpeed * 0.016f, 1f);
            _currentAimAngle += smoothFactor * (_aimHandAngle - _currentAimAngle);

            // Hand1 (左手) 抬起
            var hand1Bone = model.FindBone("Hand1");
            if (hand1Bone != null)
            {
                boneTransforms[hand1Bone.Index] =
                    Matrix.CreateRotationX(AimAngleMultiplier * _currentAimAngle) *
                    Matrix.CreateRotationY(AimAngleY);
            }

            // Hand2 (右手) 不需要瞄准动画，由瞄准角度参数控制
            var hand2Bone = model.FindBone("Hand2");
            if (hand2Bone != null)
            {
                boneTransforms[hand2Bone.Index] =
                    Matrix.CreateRotationX(_currentAimAngle * 1f);
            }
        }
    }
}
