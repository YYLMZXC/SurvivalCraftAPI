#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 鸟类飞行动画驱动器 - 处理飞行时的翅膀扇动和腿部收起
    /// </summary>
    public class BirdFlyDriver : IAnimationDriver
    {
        public string Name => "BirdFly";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Wing1", "Wing2", "Leg1", "Leg2", "Head", "Neck" };

        // 参数名称
        public string FlyPhaseParam { get; set; } = "FlyPhase";
        public string PhaseParam { get; set; } = "MovementPhase";
        public string IsOnGroundParam { get; set; } = "IsOnGround";
        public string ImmersionFactorParam { get; set; } = "ImmersionFactor";
        public string FlySpeedParam { get; set; } = "FlySpeed";
        public string RotationYParam { get; set; } = "RotationY";
        public string RotationParam { get; set; } = "Rotation";
        public string PositionParam { get; set; } = "Position";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";
        public string BobParam { get; set; } = "Bob";

        // 可配置属性
        public float WingAngle { get; set; } = 1.2f; // 翅膀扇动角度（弧度）
        public float WingGroundAngle { get; set; } = 0.3f; // 站立时的翅膀小摆动
        public float FlyLegAngle { get; set; } = 60f; // 飞行时腿部收起角度（度）
        public float HeadMaxAngleX { get; set; } = 65f;
        public float HeadMaxAngleY { get; set; } = 55f;
        public float HeadRatio { get; set; } = 0.5f;
        public float NeckRatio { get; set; } = 0.5f;

        private float _flyPhase;
        private float _phase;
        private bool _isOnGround;
        private float _immersionFactor;
        private float _flySpeed;
        private float _rotationY;
        private Vector3 _rotation;
        private Vector3 _position;
        private float _lookAngleX;
        private float _lookAngleY;
        private float _bob;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _flyPhase = parameters.GetFloat(FlyPhaseParam);
            _phase = parameters.GetFloat(PhaseParam);
            _isOnGround = parameters.GetBool(IsOnGroundParam);
            _immersionFactor = parameters.GetFloat(ImmersionFactorParam);
            _flySpeed = parameters.GetFloat(FlySpeedParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _rotation = parameters.GetVector3(RotationParam);
            _position = parameters.GetVector3(PositionParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _bob = parameters.GetFloat(BobParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            // 检查是否有翅膀
            var wing1Bone = model.FindBone("Wing1", false);
            var wing2Bone = model.FindBone("Wing2", false);
            bool hasWings = wing1Bone != null && wing2Bone != null;

            // 计算腿部角度
            float legAngle1, legAngle2;

            if (_isOnGround || _immersionFactor > 0f || _flySpeed == 0f)
            {
                // 站立或在水中：腿部交替摆动
                legAngle1 = 0.6f * MathF.Sin(MathF.PI * 2f * _phase);
                legAngle2 = -legAngle1;
            }
            else
            {
                // 飞行中：腿部收起
                float flyLegAngle = -MathUtils.DegToRad(FlyLegAngle);
                legAngle1 = flyLegAngle;
                legAngle2 = flyLegAngle;
            }

            // 计算翅膀角度
            float wingAngle = 0f;
            if (hasWings)
            {
                // 基础飞行翅膀扇动
                wingAngle = WingAngle * MathF.Sin(MathF.PI * 2f * (_flyPhase + 0.75f));

                // 站立时的小摆动
                if (_isOnGround)
                {
                    wingAngle += WingGroundAngle * MathF.Sin(MathF.PI * 2f * _phase);
                }
            }

            // Body 骨骼
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateFromYawPitchRoll(_rotation.X, 0f, 0f) *
                    Matrix.CreateTranslation(_position.X, _position.Y + _bob, _position.Z);
            }

            // 翅膀骨骼
            if (hasWings)
            {
                boneTransforms[wing1Bone.Index] = Matrix.CreateRotationY(wingAngle);
                boneTransforms[wing2Bone.Index] = Matrix.CreateRotationY(-wingAngle);
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

            // 头部和颈部
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
                float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
                float yaw = _lookAngleX / 2f;
                float pitch = 0f;

                // 站立时头部的额外摆动
                if (_isOnGround || _immersionFactor > 0f)
                {
                    pitch = 0.5f * MathF.Sin(MathF.PI * _phase);
                }

                boneTransforms[headBone.Index] =
                    Matrix.CreateFromYawPitchRoll(yaw, pitch + Math.Clamp(_rotation.Y, -(float)Math.PI / 4f, (float)Math.PI / 4f), _rotation.Z);
            }

            if (hasNeck)
            {
                float yaw2 = _lookAngleX / 2f;
                float neckPitch = 0f;

                // 站立时颈部的额外摆动
                if (_isOnGround || _immersionFactor > 0f)
                {
                    neckPitch = -0.5f * MathF.Sin(MathF.PI * _phase);
                }

                boneTransforms[neckBone.Index] = Matrix.CreateFromYawPitchRoll(yaw2, neckPitch + _lookAngleY, 0f);
            }
        }
    }
}
