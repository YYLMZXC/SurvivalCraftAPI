#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 四足死亡驱动器 - 处理完整的死亡动画（全身倒下 + 头部下垂）
    /// </summary>
    public class FourLeggedDeathDriver : IAnimationDriver
    {
        public string Name => "FourLeggedDeath";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Head", "Neck", "Leg1", "Leg2", "Leg3", "Leg4" };

        // 参数名称
        public string DeathPhaseParam { get; set; } = "DeathPhase";
        public string DeathCauseOffsetParam { get; set; } = "DeathCauseOffset";
        public string BodyHeightParam { get; set; } = "BodyHeight";
        public string BodyRightParam { get; set; } = "BodyRight";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";

        // 可配置属性
        public float DeathHeadAngle { get; set; } = 50f;
        public float DeathRollAngle { get; set; } = 90f; // 侧翻角度
        public float DeathBodyDrop { get; set; } = 0.5f; // 身体下沉高度
        public float DeathBodyRise { get; set; } = 0.2f; // 身体抬起高度

        private float _deathPhase;
        private Vector3 _deathCauseOffset;
        private float _bodyHeight;
        private Vector3 _bodyRight;
        private float _rotationY;
        private Vector3 _position;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _deathPhase = parameters.GetFloat(DeathPhaseParam);
            _deathCauseOffset = parameters.GetVector3(DeathCauseOffsetParam);
            _bodyHeight = parameters.GetFloat(BodyHeightParam);
            _bodyRight = parameters.GetVector3(BodyRightParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_deathPhase <= 0f) return;

            // 计算侧翻方向
            float rollDirection = Vector3.Dot(_bodyRight, _deathCauseOffset) > 0f ? 1 : -1;
            float rollAngle = MathUtils.DegToRad(DeathRollAngle) * _deathPhase * rollDirection;

            // Body 骨骼 - 侧翻倒下
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateTranslation(-DeathBodyDrop * _bodyHeight * Vector3.UnitY * _deathPhase)
                    * Matrix.CreateFromYawPitchRoll(_rotationY, 0f, rollAngle)
                    * Matrix.CreateTranslation(DeathBodyRise * _bodyHeight * Vector3.UnitY * _deathPhase)
                    * Matrix.CreateTranslation(_position);
            }

            // Head 骨骼 - 头部下垂
            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                boneTransforms[headBone.Index] = Matrix.CreateRotationX(MathUtils.DegToRad(DeathHeadAngle) * _deathPhase);
            }

            // Neck 骨骼 - 重置
            var neckBone = model.FindBone("Neck", false);
            if (neckBone != null)
            {
                boneTransforms[neckBone.Index] = Matrix.Identity;
            }

            // Legs 骨骼 - 停止移动，逐渐放松
            for (int i = 0; i < 4; i++)
            {
                var bone = model.FindBone($"Leg{i + 1}", false);
                if (bone != null)
                {
                    // 腿部逐渐放松，轻微下垂
                    float relaxAngle = 0.2f * _deathPhase;
                    boneTransforms[bone.Index] = Matrix.CreateRotationX(relaxAngle);
                }
            }
        }
    }
}
