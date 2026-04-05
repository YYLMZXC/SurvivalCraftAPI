#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 人类死亡/躺下驱动器 - 处理死亡动画和躺下动画
    /// </summary>
    public class HumanDeathDriver : IAnimationDriver
    {
        public string Name => "HumanDeath";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Head", "Leg1", "Leg2", "Hand1", "Hand2" };

        // 参数名称
        public string DeathPhaseParam { get; set; } = "DeathPhase";
        public string LieDownFactorParam { get; set; } = "LieDownFactor";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";
        public string BodyHeightParam { get; set; } = "BodyHeight";
        public string BodyDepthParam { get; set; } = "BodyDepth";
        public string BodyForwardParam { get; set; } = "BodyForward";
        public string GameTimeParam { get; set; } = "GameTime";

        // 可配置属性
        public float LieDownRollAngle { get; set; } = 90f; // 躺下时侧翻角度
        public float SmoothSpeed { get; set; } = 12f;

        private float _deathPhase;
        private float _lieDownFactor;
        private float _rotationY;
        private Vector3 _position;
        private float _bodyHeight;
        private float _bodyDepth;
        private Vector3 _bodyForward;
        private float _gameTime;

        private Vector2 _currentHandAngles1 = Vector2.Zero;
        private Vector2 _currentHandAngles2 = Vector2.Zero;
        private Vector2 _currentLegAngles1 = Vector2.Zero;
        private Vector2 _currentLegAngles2 = Vector2.Zero;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _deathPhase = parameters.GetFloat(DeathPhaseParam);
            _lieDownFactor = parameters.GetFloat(LieDownFactorParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
            _bodyHeight = parameters.GetFloat(BodyHeightParam);
            _bodyDepth = parameters.GetFloat(BodyDepthParam);
            _bodyForward = parameters.GetVector3(BodyForwardParam);
            _gameTime = parameters.GetFloat(GameTimeParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            float lieDownPhase = MathUtils.Max(_deathPhase, _lieDownFactor);
            if (lieDownPhase <= 0f) return;

            float inversePhase = 1f - lieDownPhase;

            // 计算侧翻时身体位置偏移
            // 原始代码使用 BoxSize.Y 作为高度，BoxSize.Z 作为深度
            Vector3 forwardFlat = Vector3.Normalize(_bodyForward * new Vector3(1f, 0f, 1f));
            Vector3 bodyOffset = lieDownPhase * 0.5f * _bodyHeight * forwardFlat
                + lieDownPhase * Vector3.UnitY * _bodyDepth * 0.1f;

            // 设置 Body 骨骼 - 侧翻躺下
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                float rollAngle = MathUtils.DegToRad(LieDownRollAngle) * lieDownPhase;
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateFromYawPitchRoll(_rotationY, (float)Math.PI / 2f * lieDownPhase, 0f) *
                    Matrix.CreateTranslation(_position + bodyOffset);
            }

            // 设置 Head 骨骼 - 重置
            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                boneTransforms[headBone.Index] = Matrix.Identity;
            }

            // 设置 Hand 骨骼 - 逐渐放松
            var hand1Bone = model.FindBone("Hand1");
            if (hand1Bone != null)
            {
                boneTransforms[hand1Bone.Index] =
                    Matrix.CreateRotationY(_currentHandAngles1.Y * inversePhase) *
                    Matrix.CreateRotationX(_currentHandAngles1.X * inversePhase);
            }

            var hand2Bone = model.FindBone("Hand2");
            if (hand2Bone != null)
            {
                boneTransforms[hand2Bone.Index] =
                    Matrix.CreateRotationY(_currentHandAngles2.Y * inversePhase) *
                    Matrix.CreateRotationX(_currentHandAngles2.X * inversePhase);
            }

            // 设置 Leg 骨骼 - 逐渐放松
            var leg1Bone = model.FindBone("Leg1");
            if (leg1Bone != null)
            {
                boneTransforms[leg1Bone.Index] =
                    Matrix.CreateRotationY(_currentLegAngles1.Y * inversePhase) *
                    Matrix.CreateRotationX(_currentLegAngles1.X * inversePhase);
            }

            var leg2Bone = model.FindBone("Leg2");
            if (leg2Bone != null)
            {
                boneTransforms[leg2Bone.Index] =
                    Matrix.CreateRotationY(_currentLegAngles2.Y * inversePhase) *
                    Matrix.CreateRotationX(_currentLegAngles2.X * inversePhase);
            }
        }
    }
}
