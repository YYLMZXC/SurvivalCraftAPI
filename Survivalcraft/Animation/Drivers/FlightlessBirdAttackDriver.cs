#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 不能飞的鸟类踢腿攻击驱动器 - 处理踢腿攻击动画
    /// </summary>
    public class FlightlessBirdAttackDriver : IAnimationDriver
    {
        public string Name => "FlightlessBirdAttack";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Leg1", "Leg2", "Head", "Neck" };

        // 参数名称
        public string KickPhaseParam { get; set; } = "KickPhase";
        public string KickFactorParam { get; set; } = "KickFactor";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";

        // 可配置属性
        public float KickAngle { get; set; } = 60f; // 踢腿角度（度）
        public float SmoothSpeed { get; set; } = 12f;
        public float HeadMaxAngleX { get; set; } = 90f;
        public float HeadMaxAngleY { get; set; } = 50f;
        public float HeadRatio { get; set; } = 0.6f;
        public float NeckRatio { get; set; } = 0.4f;

        private float _kickPhase;
        private float _kickFactor;
        private float _lookAngleX;
        private float _lookAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _kickPhase = parameters.GetFloat(KickPhaseParam);
            _kickFactor = parameters.GetFloat(KickFactorParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_kickFactor <= 0f) return;

            // 踢腿动画：使用 Sigmoid 函数使动画更自然
            // 原始逻辑: x = DegToRad(60) * Sin(PI * Sigmoid(kickPhase, 5))
            float kickAngleRad = MathUtils.DegToRad(KickAngle);
            float sigmoid = MathUtils.Sigmoid(_kickPhase, 5f);
            float kickAngle = kickAngleRad * MathF.Sin(MathF.PI * sigmoid);

            // Leg1 踢出
            var leg1Bone = model.FindBone("Leg1");
            if (leg1Bone != null)
            {
                boneTransforms[leg1Bone.Index] = Matrix.CreateRotationX(kickAngle);
            }

            // Leg2 保持不动或轻微移动
            var leg2Bone = model.FindBone("Leg2");
            if (leg2Bone != null)
            {
                boneTransforms[leg2Bone.Index] = Matrix.CreateRotationX(-kickAngle * 0.2f);
            }

            // 头部和颈部
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
                float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
                float lookAngleX = Math.Clamp(_lookAngleX, -maxAngleX, maxAngleX);
                float lookAngleY = Math.Clamp(_lookAngleY, -maxAngleY, maxAngleY);

                if (hasNeck)
                {
                    lookAngleX *= HeadRatio;
                    lookAngleY *= HeadRatio;
                }

                boneTransforms[headBone.Index] =
                    Matrix.CreateRotationX(lookAngleY) *
                    Matrix.CreateRotationZ(-lookAngleX);
            }

            if (hasNeck)
            {
                float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
                float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
                float lookAngleX = Math.Clamp(_lookAngleX * NeckRatio, -maxAngleX, maxAngleX);
                float lookAngleY = Math.Clamp(_lookAngleY * NeckRatio, -maxAngleY, maxAngleY);

                boneTransforms[neckBone.Index] =
                    Matrix.CreateRotationX(lookAngleY) *
                    Matrix.CreateRotationZ(-lookAngleX);
            }
        }
    }
}
