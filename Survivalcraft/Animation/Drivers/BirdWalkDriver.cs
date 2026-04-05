#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 鸟类行走驱动器 - 处理行走时的腿部摆动和身体晃动
    /// </summary>
    public class BirdWalkDriver : IAnimationDriver
    {
        public string Name => "BirdWalk";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Leg1", "Leg2", "Head", "Neck" };

        // 参数名称
        public string PhaseParam { get; set; } = "MovementPhase";
        public string BobParam { get; set; } = "Bob";
        public string RotationParam { get; set; } = "Rotation";
        public string PositionParam { get; set; } = "Position";
        public string IsOnGroundParam { get; set; } = "IsOnGround";
        public string ImmersionFactorParam { get; set; } = "ImmersionFactor";
        public string FlySpeedParam { get; set; } = "FlySpeed";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";
        public string WalkBobHeightParam { get; set; } = "WalkBobHeight";

        // 可配置属性
        public float LegAngle { get; set; } = 0.6f; // 腿部摆动角度（弧度）
        public float SmoothSpeed { get; set; } = 12f;
        public float FlyLegAngle { get; set; } = 60f; // 飞行时腿部收起角度（度）

        private float _phase;
        private float _bob;
        private Vector3 _rotation;
        private Vector3 _position;
        private bool _isOnGround;
        private float _immersionFactor;
        private float _flySpeed;
        private float _lookAngleX;
        private float _lookAngleY;
        private float _walkBobHeight;

        // 平滑过渡
        private float _currentBob = 0f;
        private bool _firstUpdate = true;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _phase = parameters.GetFloat(PhaseParam);
            _bob = parameters.GetFloat(BobParam);
            _rotation = parameters.GetVector3(RotationParam);
            _position = parameters.GetVector3(PositionParam);
            _isOnGround = parameters.GetBool(IsOnGroundParam);
            _immersionFactor = parameters.GetFloat(ImmersionFactorParam);
            _flySpeed = parameters.GetFloat(FlySpeedParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _walkBobHeight = parameters.GetFloat(WalkBobHeightParam);

            // 平滑过渡 Bob
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
            // 计算腿部角度
            float legAngle1, legAngle2;

            if (_isOnGround || _immersionFactor > 0f || _flySpeed == 0f)
            {
                // 站立或在水中：腿部交替摆动
                legAngle1 = LegAngle * MathF.Sin(MathF.PI * 2f * _phase);
                legAngle2 = -legAngle1;
            }
            else
            {
                // 飞行中：腿部收起
                float flyLegAngle = -MathUtils.DegToRad(FlyLegAngle);
                legAngle1 = flyLegAngle;
                legAngle2 = flyLegAngle;
            }

            // Body 骨骼
            // 原始: Matrix.CreateFromYawPitchRoll(vector.X, 0f, 0f) * Matrix.CreateTranslation(position + Bob)
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateFromYawPitchRoll(_rotation.X, 0f, 0f) *
                    Matrix.CreateTranslation(_position.X, _position.Y + _currentBob, _position.Z);
            }

            // 腿部骨骼
            var leg1Bone = model.FindBone("Leg1");
            if (leg1Bone != null)
            {
                boneTransforms[leg1Bone.Index] = Matrix.CreateRotationX(legAngle1);
            }

            var leg2Bone = model.FindBone("Leg2");
            if (leg2Bone != null)
            {
                boneTransforms[leg2Bone.Index] = Matrix.CreateRotationX(legAngle2);
            }

            // 头部和颈部 - 严格按照原始逻辑
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            // 原始代码计算:
            // yaw = LookAngleX / 2
            // yaw2 = LookAngleX / 2
            // num4 = 0 (站立时头部无摆动，只有行走时有)
            // num5 = 0
            // num6 = Cos(2π * phase)
            // num4 -= 1.25 * (1 - (cos >= 0 ? cos : -0.5 * cos))
            // num4 += LookAngleY
            // SetBoneTransform(neck, Matrix.CreateFromYawPitchRoll(yaw2, num4, 0f))
            // SetBoneTransform(head, Matrix.CreateFromYawPitchRoll(yaw, num5 + Clamp(vector.Y, -π/4, π/4), vector.Z))

            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                float yaw = _lookAngleX / 2f;
                // 头部 pitch: 只有 Clamp(vector.Y)
                float headPitch = Math.Clamp(_rotation.Y, -(float)Math.PI / 4f, (float)Math.PI / 4f);
                boneTransforms[headBone.Index] =
                    Matrix.CreateFromYawPitchRoll(yaw, headPitch, _rotation.Z);
            }

            if (hasNeck)
            {
                float yaw2 = _lookAngleX / 2f;
                // 颈部 pitch: 只有 LookAngleY
                boneTransforms[neckBone.Index] =
                    Matrix.CreateFromYawPitchRoll(yaw2, _lookAngleY, 0f);
            }
        }
    }
}
