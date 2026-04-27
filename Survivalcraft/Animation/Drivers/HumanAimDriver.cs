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

        public string[] TargetBones => mTargetBones;
        private string[] mTargetBones = new[] { "Hand1", "Hand2" };

        // 参数名称
        public string AimHandAngleParam { get; set; } = "AimHandAngle";
        public string GameTimeDeltaParam { get; set; } = "GameTimeDelta";

        // 可配置属性
        public float AimAngleMultiplier { get; set; } = 1.5f;
        public float AimAngleY { get; set; } = -0.7f;
        public float SmoothSpeed { get; set; } = 12f;

        private float _aimHandAngle;
        private float _gameTimeDelta;
        private float _currentAimActive = 0f;  // 瞄准激活程度（用于平滑过渡）

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _aimHandAngle = parameters.GetFloat(AimHandAngleParam);
            _gameTimeDelta = parameters.GetFloat(GameTimeDeltaParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            // 平滑过渡瞄准激活程度
            float targetActive = _aimHandAngle != 0f ? 1f : 0f;
            float smoothFactor = MathUtils.Min(SmoothSpeed * _gameTimeDelta, 1f);
            _currentAimActive += smoothFactor * (targetActive - _currentAimActive);

            // 如果瞄准激活程度太小，跳过处理
            if (_currentAimActive < 0.01f) return;

            // 原始代码：
            // Hand1 (左手)：X = 1.5, Y = -0.7（需要平滑过渡）
            // Hand2 (右手)：X = AimHandAngle * 1, Y = 0
            var hand1Bone = model.FindBone("Hand1");
            if (hand1Bone != null)
            {
                // Hand1 角度也需要平滑过渡
                boneTransforms[hand1Bone.Index] =
                    Matrix.CreateRotationX(AimAngleMultiplier * _currentAimActive) *
                    Matrix.CreateRotationY(AimAngleY * _currentAimActive);
            }

            var hand2Bone = model.FindBone("Hand2");
            if (hand2Bone != null)
            {
                boneTransforms[hand2Bone.Index] =
                    Matrix.CreateRotationX(_aimHandAngle * _currentAimActive);
            }
        }
    }
}
