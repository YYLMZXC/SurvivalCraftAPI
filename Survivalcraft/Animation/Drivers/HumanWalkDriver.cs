#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 人类行走驱动器 - 处理行走时的腿部摆动、手部摆动、身体晃动和头部追踪
    /// </summary>
    public class HumanWalkDriver : IAnimationDriver
    {
        public string Name => "HumanWalk";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Head", "Leg1", "Leg2", "Hand1", "Hand2" };

        // 参数名称
        public string PhaseParam { get; set; } = "MovementPhase";
        public string BobParam { get; set; } = "Bob";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";
        public string WalkLegsAngleParam { get; set; } = "WalkLegsAngle";
        public string WalkBobHeightParam { get; set; } = "WalkBobHeight";
        public string HeadingOffsetParam { get; set; } = "HeadingOffset";
        public string CrouchFactorParam { get; set; } = "CrouchFactor";
        public string IsCreativeFlyParam { get; set; } = "IsCreativeFly";
        public string GameTimeParam { get; set; } = "GameTime";

        // 可配置属性
        public float LegAngle { get; set; } = 0.5f;
        public float HandSwingAngle { get; set; } = 0.5f;
        public float SmoothSpeed { get; set; } = 12f;
        public float HeadMaxAngleX { get; set; } = 80f;
        public float HeadMaxAngleY { get; set; } = 45f;
        public float CrouchBodyDrop { get; set; } = 0.7f;
        public float CrouchLegScale { get; set; } = 0.5f;
        public float HandNoiseScale { get; set; } = 0.1f;

        private float _phase;
        private float _bob;
        private float _rotationY;
        private Vector3 _position;
        private float _lookAngleX;
        private float _lookAngleY;
        private float _walkLegsAngle;
        private float _walkBobHeight;
        private float _headingOffset;
        private float _crouchFactor;
        private bool _isCreativeFly;
        private float _gameTime;

        // 平滑过渡
        private float _currentBob = 0f;
        private Vector2 _currentHeadAngles = Vector2.Zero;
        private Vector2 _currentHandAngles1 = Vector2.Zero;
        private Vector2 _currentHandAngles2 = Vector2.Zero;
        private Vector2 _currentLegAngles1 = Vector2.Zero;
        private Vector2 _currentLegAngles2 = Vector2.Zero;
        private bool _firstUpdate = true;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _phase = parameters.GetFloat(PhaseParam);
            _bob = parameters.GetFloat(BobParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _walkLegsAngle = parameters.GetFloat(WalkLegsAngleParam);
            _walkBobHeight = parameters.GetFloat(WalkBobHeightParam);
            _headingOffset = parameters.GetFloat(HeadingOffsetParam);
            _crouchFactor = parameters.GetFloat(CrouchFactorParam);
            _isCreativeFly = parameters.GetBool(IsCreativeFlyParam);
            _gameTime = parameters.GetFloat(GameTimeParam);

            // 平滑过渡
            float smoothFactor = MathUtils.Min(SmoothSpeed * deltaTime, 1f);
            if (_firstUpdate)
            {
                _currentBob = _bob;
                _firstUpdate = false;
            }
            else
            {
                _currentBob += smoothFactor * (_bob - _currentBob);
            }
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            float num = MathF.Sin((float)Math.PI * 2f * _phase);

            // 计算蹲下因子
            float crouchSigmoid = MathUtils.Sigmoid(_crouchFactor, 4f);

            // 计算手部自然摆动噪声
            float noiseTime = (float)MathUtils.Remainder(0.75 * _gameTime + 10000, 10000.0);
            float handNoise1 = MathUtils.Lerp(-HandNoiseScale, HandNoiseScale, SimplexNoise.Noise(noiseTime));
            float handNoise2 = MathUtils.Lerp(-HandNoiseScale, HandNoiseScale, SimplexNoise.Noise(noiseTime + 100f));

            // 计算各部位角度
            float legAngle1 = _walkLegsAngle * num;
            float legAngle2 = -legAngle1;

            float handAngleX1, handAngleX2;
            float handAngleY1, handAngleY2;

            if (_isCreativeFly)
            {
                // 创造模式飞行
                float flyNoise1 = MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(1.07f * noiseTime + 400f));
                float flyNoise2 = MathUtils.Lerp(0f, 0.25f, SimplexNoise.Noise(0.93f * noiseTime + 500f));
                handAngleX1 = -0.1f;
                handAngleX2 = -0.1f;
                handAngleY1 = flyNoise1;
                handAngleY2 = -flyNoise2;
            }
            else if (_phase != 0f)
            {
                // 行走时手部摆动（与腿反向）
                handAngleX1 = -HandSwingAngle * num;
                handAngleX2 = HandSwingAngle * num;
                handAngleY1 = 0f;
                handAngleY2 = 0f;
            }
            else
            {
                // 站立时手部自然下垂
                handAngleX1 = handNoise1;
                handAngleX2 = handNoise2;
                handAngleY1 = 0f;
                handAngleY2 = 0f;
            }

            // 平滑过渡所有角度
            float smoothFactor = MathUtils.Min(SmoothSpeed * 0.016f, 1f);

            _currentHeadAngles += smoothFactor * (new Vector2(_lookAngleX, _lookAngleY) - _currentHeadAngles);
            _currentHandAngles1 += smoothFactor * (new Vector2(handAngleX1, handAngleY1) - _currentHandAngles1);
            _currentHandAngles2 += smoothFactor * (new Vector2(handAngleX2, handAngleY2) - _currentHandAngles2);
            _currentLegAngles1 += smoothFactor * (new Vector2(legAngle1, 0f) - _currentLegAngles1);
            _currentLegAngles2 += smoothFactor * (new Vector2(legAngle2, 0f) - _currentLegAngles2);

            // 蹲下时腿部角度减半
            if (_crouchFactor >= 1f)
            {
                _currentLegAngles1 *= 0.5f;
                _currentLegAngles2 *= 0.5f;
            }

            // 计算身体位置（考虑蹲下）
            Vector3 bodyPosition = new(
                _position.X,
                _position.Y + _currentBob - MathUtils.Lerp(0f, CrouchBodyDrop, crouchSigmoid),
                _position.Z
            );

            // 腿部平移和缩放（蹲下时）
            Vector3 legTranslate = new(0f, MathUtils.Lerp(0f, 7f, crouchSigmoid), MathUtils.Lerp(0f, 28f, crouchSigmoid));
            Vector3 legScale = new(1f, 1f, MathUtils.Lerp(1f, CrouchLegScale, crouchSigmoid));

            // 设置 Body 骨骼
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                float bodyRotationY = _rotationY + _headingOffset;
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateRotationY(bodyRotationY) *
                    Matrix.CreateTranslation(bodyPosition);
            }

            // 设置 Head 骨骼
            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
                float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
                float clampedX = Math.Clamp(_currentHeadAngles.X, -maxAngleX, maxAngleX);
                float clampedY = Math.Clamp(_currentHeadAngles.Y, -maxAngleY, maxAngleY);

                boneTransforms[headBone.Index] =
                    Matrix.CreateRotationX(clampedY) *
                    Matrix.CreateRotationZ(-clampedX);
            }

            // 设置 Hand1 骨骼
            var hand1Bone = model.FindBone("Hand1");
            if (hand1Bone != null)
            {
                boneTransforms[hand1Bone.Index] =
                    Matrix.CreateRotationY(_currentHandAngles1.Y) *
                    Matrix.CreateRotationX(_currentHandAngles1.X);
            }

            // 设置 Hand2 骨骼
            var hand2Bone = model.FindBone("Hand2");
            if (hand2Bone != null)
            {
                boneTransforms[hand2Bone.Index] =
                    Matrix.CreateRotationY(_currentHandAngles2.Y) *
                    Matrix.CreateRotationX(_currentHandAngles2.X);
            }

            // 设置 Leg1 骨骼
            var leg1Bone = model.FindBone("Leg1");
            if (leg1Bone != null)
            {
                boneTransforms[leg1Bone.Index] =
                    Matrix.CreateRotationY(_currentLegAngles1.Y) *
                    Matrix.CreateRotationX(_currentLegAngles1.X) *
                    Matrix.CreateTranslation(legTranslate) *
                    Matrix.CreateScale(legScale);
            }

            // 设置 Leg2 骨骼
            var leg2Bone = model.FindBone("Leg2");
            if (leg2Bone != null)
            {
                boneTransforms[leg2Bone.Index] =
                    Matrix.CreateRotationY(_currentLegAngles2.Y) *
                    Matrix.CreateRotationX(_currentLegAngles2.X) *
                    Matrix.CreateTranslation(legTranslate) *
                    Matrix.CreateScale(legScale);
            }
        }
    }
}
