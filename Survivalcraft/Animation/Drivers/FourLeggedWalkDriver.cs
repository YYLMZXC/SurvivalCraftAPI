#nullable disable
using Engine;
using Engine.Graphics;
using Engine.Graphics.Drivers;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 四足行走驱动器 - 处理行走动画（Body + Legs + Head 摆动）
    /// </summary>
    public class FourLeggedWalkDriver : IAnimationDriver
    {
        public string Name => "FourLeggedWalk";
        public BlendMode BlendMode => BlendMode.Override;

        // 目标骨骼 - 行走时需要控制的所有骨骼
        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Leg1", "Leg2", "Leg3", "Leg4", "Head", "Neck" };

        // 配置参数名称
        public string PhaseParam { get; set; } = "MovementPhase";
        public string FrontAngleParam { get; set; } = "WalkFrontLegsAngle";
        public string HindAngleParam { get; set; } = "WalkHindLegsAngle";
        public string GaitParam { get; set; } = "Gait";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";
        public string CanterLegsAngleFactorParam { get; set; } = "CanterLegsAngleFactor";
        public string IsOnGroundParam { get; set; } = "IsOnGround";
        public string ImmersionFactorParam { get; set; } = "ImmersionFactor";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";

        // ========== 可配置的动画参数 ==========

        // 步态相位偏移 (Leg1, Leg2, Leg3, Leg4)
        public float[] WalkPhases { get; set; } = { 0.0f, 0.5f, 0.25f, 0.75f };
        public float[] TrotPhases { get; set; } = { 0.0f, 0.5f, 0.5f, 0.0f };
        public float[] CanterPhases { get; set; } = { 0.0f, 0.25f, 0.15f, 0.4f };

        // 平滑过渡速度
        public float SmoothSpeed { get; set; } = 12f;

        // 头部追踪参数
        public float HeadMaxAngleX { get; set; } = 65f;
        public float HeadMaxAngleY { get; set; } = 55f;
        public float HeadRatio { get; set; } = 0.4f;
        public float NeckRatio { get; set; } = 0.6f;

        // 行走时头部摆动参数
        public float WalkHeadAngle { get; set; } = 3f;
        public float TrotHeadAngle { get; set; } = 3f;
        public float CanterHeadAngle { get; set; } = 8f;
        public float WalkHeadFrequency { get; set; } = 4f;
        public float TrotHeadFrequency { get; set; } = 4f;
        public float CanterHeadFrequency { get; set; } = 2f;

        // Bob 参数
        public string BobHeightParam { get; set; } = "WalkBobHeight";

        private float _phase;
        private float _frontAngle;
        private float _hindAngle;
        private int _gait;
        private float _rotationY;
        private Vector3 _position;
        private float _canterLegsAngleFactor;
        private bool _isOnGround;
        private float _immersionFactor;
        private float _lookAngleX;
        private float _lookAngleY;
        private float _bobHeight;

        // 平滑过渡用的当前角度
        private float _legAngle1 = 0f;
        private float _legAngle2 = 0f;
        private float _legAngle3 = 0f;
        private float _legAngle4 = 0f;
        private float _headAngleY = 0f;
        private float _currentBob = 0f;

        // 首次更新标记
        private bool _firstUpdate = true;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _phase = parameters.GetFloat(PhaseParam);
            _frontAngle = parameters.GetFloat(FrontAngleParam);
            _hindAngle = parameters.GetFloat(HindAngleParam);
            _gait = (int)parameters.GetFloat(GaitParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
            _canterLegsAngleFactor = parameters.GetFloat(CanterLegsAngleFactorParam);
            _isOnGround = parameters.GetBool(IsOnGroundParam);
            _immersionFactor = parameters.GetFloat(ImmersionFactorParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _bobHeight = parameters.GetFloat(BobHeightParam);

            // 计算腿部角度
            float targetAngle1 = 0f, targetAngle2 = 0f, targetAngle3 = 0f, targetAngle4 = 0f;

            if (_phase != 0f && (_isOnGround || _immersionFactor > 0f))
            {
                // 获取对应步态的相位偏移
                float[] phaseOffsets = _gait switch
                {
                    2 => CanterPhases,
                    1 => TrotPhases,
                    _ => WalkPhases
                };

                if (_gait == 2) // Canter
                {
                    float factor = _canterLegsAngleFactor > 0 ? _canterLegsAngleFactor : 1.5f;
                    targetAngle1 = _frontAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[0]));
                    targetAngle2 = _frontAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[1]));
                    targetAngle3 = _hindAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[2]));
                    targetAngle4 = _hindAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[3]));
                }
                else
                {
                    targetAngle1 = _frontAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[0]));
                    targetAngle2 = _frontAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[1]));
                    targetAngle3 = _hindAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[2]));
                    targetAngle4 = _hindAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[3]));
                }
            }

            // 计算行走时的头部摆动
            float targetHeadY = 0f;
            if (_phase != 0f)
            {
                targetHeadY = _gait switch
                {
                    2 => MathUtils.DegToRad(CanterHeadAngle) * MathF.Sin(CanterHeadFrequency * MathF.PI * _phase),
                    1 => MathUtils.DegToRad(TrotHeadAngle) * MathF.Sin(TrotHeadFrequency * MathF.PI * _phase),
                    _ => MathUtils.DegToRad(WalkHeadAngle) * MathF.Sin(WalkHeadFrequency * MathF.PI * _phase)
                };
            }

            // 计算 Bob（根据步态不同）
            float targetBob = 0f;
            if (_phase != 0f)
            {
                targetBob = _gait switch
                {
                    2 => -_bobHeight * 1.5f * MathF.Sin(2f * MathF.PI * _phase),  // Canter: 正弦，1.5倍
                    1 => _bobHeight * 1.5f * MathUtils.Sqr(MathF.Sin(2f * MathF.PI * _phase)),  // Trot: 平方，正向
                    _ => -_bobHeight * MathUtils.Sqr(MathF.Sin(2f * MathF.PI * _phase))  // Walk: 平方，负向
                };
            }

            // 平滑过渡
            float smoothFactor = MathUtils.Min(SmoothSpeed * deltaTime, 1f);

            // 首次更新时直接设置目标值，避免从 0 平滑过渡导致的闪烁
            if (_firstUpdate)
            {
                _legAngle1 = targetAngle1;
                _legAngle2 = targetAngle2;
                _legAngle3 = targetAngle3;
                _legAngle4 = targetAngle4;
                _headAngleY = targetHeadY;
                _currentBob = targetBob;
                _firstUpdate = false;
            }
            else
            {
                _legAngle1 += smoothFactor * (targetAngle1 - _legAngle1);
                _legAngle2 += smoothFactor * (targetAngle2 - _legAngle2);
                _legAngle3 += smoothFactor * (targetAngle3 - _legAngle3);
                _legAngle4 += smoothFactor * (targetAngle4 - _legAngle4);
                _headAngleY += smoothFactor * (targetHeadY - _headAngleY);
                _currentBob += smoothFactor * (targetBob - _currentBob);
            }
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            // Body 骨骼（包含位置和旋转）
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateRotationY(_rotationY) *
                    Matrix.CreateTranslation(_position.X, _position.Y + _currentBob, _position.Z);
            }

            // 腿部骨骼
            for (int i = 0; i < 4; i++)
            {
                var boneName = $"Leg{i + 1}";
                var bone = model.FindBone(boneName, false);
                if (bone != null)
                {
                    float angle = i switch
                    {
                        0 => _legAngle1,
                        1 => _legAngle2,
                        2 => _legAngle3,
                        _ => _legAngle4
                    };

                    boneTransforms[bone.Index] = Matrix.CreateRotationX(angle);
                }
            }

            // 头部和颈部 - 行走时的头部摆动 + 头部追踪
            var neckBone = model.FindBone("Neck", false);
            bool hasNeck = neckBone != null;

            var headBone = model.FindBone("Head");
            if (headBone != null)
            {
                float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
                float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
                float lookAngleX = Math.Clamp(_lookAngleX, -maxAngleX, maxAngleX);
                float lookAngleY = Math.Clamp(_lookAngleY + _headAngleY, -maxAngleY, maxAngleY);

                if (hasNeck)
                {
                    lookAngleX *= HeadRatio;
                    lookAngleY *= HeadRatio;
                }

                boneTransforms[headBone.Index] =
                    Matrix.CreateRotationX(lookAngleY) *
                    Matrix.CreateRotationZ(-lookAngleX);
            }

            // 颈部动画
            if (hasNeck)
            {
                float maxAngleX = MathUtils.DegToRad(HeadMaxAngleX);
                float maxAngleY = MathUtils.DegToRad(HeadMaxAngleY);
                float lookAngleX = Math.Clamp(_lookAngleX * NeckRatio, -maxAngleX, maxAngleX);
                float lookAngleY = Math.Clamp((_lookAngleY + _headAngleY) * NeckRatio, -maxAngleY, maxAngleY);

                boneTransforms[neckBone.Index] =
                    Matrix.CreateRotationX(lookAngleY) *
                    Matrix.CreateRotationZ(-lookAngleX);
            }
        }
    }
}
