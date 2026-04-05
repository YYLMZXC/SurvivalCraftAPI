#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 鸟类攻击驱动器 - 处理踢腿攻击动画
    /// </summary>
    public class BirdAttackDriver : IAnimationDriver
    {
        public string Name => "BirdAttack";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Leg1", "Leg2", "Head", "Neck" };

        // 参数名称
        public string KickPhaseParam { get; set; } = "KickPhase";
        public string AttackFactorParam { get; set; } = "AttackFactor";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";

        // 可配置属性
        public float KickAngle { get; set; } = 0.8f; // 踢腿角度（弧度）
        public float PeckAngle { get; set; } = 1.25f; // 啄食角度（弧度）

        private float _kickPhase;
        private float _attackFactor;
        private float _lookAngleX;
        private float _lookAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _kickPhase = parameters.GetFloat(KickPhaseParam);
            _attackFactor = parameters.GetFloat(AttackFactorParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            // 腿部踢击动画
            float legKick = KickAngle * MathF.Sin(MathF.PI * 2f * _kickPhase);

            var leg1Bone = model.FindBone("Leg1");
            if (leg1Bone != null)
            {
                boneTransforms[leg1Bone.Index] = Matrix.CreateRotationX(legKick);
            }

            var leg2Bone = model.FindBone("Leg2");
            if (leg2Bone != null)
            {
                boneTransforms[leg2Bone.Index] = Matrix.CreateRotationX(-legKick);
            }

            // 头部动画 - 类似啄食但更激烈
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                float cosPhase = MathF.Cos(MathF.PI * 2f * _kickPhase);
                float peckAngle = PeckAngle * (1f - (cosPhase >= 0f ? cosPhase : -0.5f * cosPhase));
                float headPitch = -peckAngle + _lookAngleY;

                boneTransforms[headBone.Index] =
                    Matrix.CreateRotationX(headPitch) *
                    Matrix.CreateRotationZ(-_lookAngleX / 2f);
            }

            if (hasNeck)
            {
                float cosPhase = MathF.Cos(MathF.PI * 2f * _kickPhase);
                float peckAngle = PeckAngle * (1f - (cosPhase >= 0f ? cosPhase : -0.5f * cosPhase));
                float neckPitch = peckAngle * 0.5f + _lookAngleY;

                boneTransforms[neckBone.Index] =
                    Matrix.CreateRotationX(neckPitch) *
                    Matrix.CreateRotationZ(-_lookAngleX / 2f);
            }
        }
    }
}
