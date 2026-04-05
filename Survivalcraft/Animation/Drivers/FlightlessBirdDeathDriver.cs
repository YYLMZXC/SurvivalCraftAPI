#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 不能飞的鸟类死亡驱动器 - 处理死亡时的侧翻动画
    /// </summary>
    public class FlightlessBirdDeathDriver : IAnimationDriver
    {
        public string Name => "FlightlessBirdDeath";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Head", "Neck", "Leg1", "Leg2" };

        // 参数名称
        public string DeathPhaseParam { get; set; } = "DeathPhase";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";
        public string DeathCauseOffsetParam { get; set; } = "DeathCauseOffset";
        public string BodyHeightParam { get; set; } = "BodyHeight";

        // 可配置属性
        public float DeathRollAngle { get; set; } = 90f; // 死亡侧翻角度（度）
        public float SmoothSpeed { get; set; } = 12f;

        private float _deathPhase;
        private float _rotationY;
        private Vector3 _position;
        private Vector3 _deathCauseOffset;
        private float _bodyHeight;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _deathPhase = parameters.GetFloat(DeathPhaseParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
            _deathCauseOffset = parameters.GetVector3(DeathCauseOffsetParam);
            _bodyHeight = parameters.GetFloat(BodyHeightParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_deathPhase <= 0f) return;

            // 死亡时的倒下系数
            float deathInverse = 1f - _deathPhase;

            // 计算侧翻方向（根据死亡原因偏移）
            float rollDirection = Vector3.Dot(Vector3.UnitX, _deathCauseOffset) > 0f ? 1f : -1f;
            float rollAngle = MathUtils.DegToRad(DeathRollAngle) * _deathPhase * rollDirection;

            // 计算高度（用于下沉动画）
            float bodyHeight = _bodyHeight > 0 ? _bodyHeight : 1f;

            // Body 骨骼 - 侧翻倒下
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                // 原始逻辑:
                // Matrix.CreateTranslation(-0.5 * height * phase * UnitY)
                // * Matrix.CreateFromYawPitchRoll(rotation.X, 0, PI/2 * phase * direction)
                // * Matrix.CreateTranslation(0.2 * height * phase * UnitY)
                // * Matrix.CreateTranslation(position)

                boneTransforms[bodyBone.Index] =
                    Matrix.CreateTranslation(-0.5f * bodyHeight * _deathPhase * Vector3.UnitY) *
                    Matrix.CreateFromYawPitchRoll(_rotationY, 0f, MathF.PI / 2f * _deathPhase * rollDirection) *
                    Matrix.CreateTranslation(0.2f * bodyHeight * _deathPhase * Vector3.UnitY) *
                    Matrix.CreateTranslation(_position);
            }

            // Head 和 Neck 骨骼重置
            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                boneTransforms[headBone.Index] = Matrix.Identity;
            }

            var neckBone = model.FindBone("Neck", false);
            if (neckBone != null)
            {
                boneTransforms[neckBone.Index] = Matrix.Identity;
            }

            // 腿部逐渐放松（保持当前角度但逐渐减弱）
            var leg1Bone = model.FindBone("Leg1");
            if (leg1Bone != null)
            {
                // 死亡时腿部保持最后的角度但逐渐放松
                boneTransforms[leg1Bone.Index] = Matrix.CreateRotationX(0f);
            }

            var leg2Bone = model.FindBone("Leg2");
            if (leg2Bone != null)
            {
                boneTransforms[leg2Bone.Index] = Matrix.CreateRotationX(0f);
            }
        }
    }
}
