#nullable disable

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 程序化四足动画驱动器
    /// 替代 ComponentFourLeggedModel 的硬编码动画逻辑
    /// </summary>
    public class ProceduralFourLeggedDriver : IAnimationDriver
    {
        public string Name => "ProceduralFourLegged";
        public BlendMode BlendMode => BlendMode.Override;

        // 骨骼名称配置
        public string BodyBoneName { get; set; } = "Body";
        public string NeckBoneName { get; set; } = "Neck";
        public string HeadBoneName { get; set; } = "Head";
        public string Leg1BoneName { get; set; } = "Leg1";  // 前左
        public string Leg2BoneName { get; set; } = "Leg2";  // 前右
        public string Leg3BoneName { get; set; } = "Leg3";  // 后左
        public string Leg4BoneName { get; set; } = "Leg4";  // 后右

        // 参数名称配置
        public string SpeedParam { get; set; } = "Speed";
        public string MovementPhaseParam { get; set; } = "MovementPhase";
        public string GaitParam { get; set; } = "Gait";  // 0=Walk, 1=Trot, 2=Canter
        public string DeathPhaseParam { get; set; } = "DeathPhase";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";
        public string FeedFactorParam { get; set; } = "FeedFactor";
        public string BobParam { get; set; } = "Bob";

        // 配置参数名称（从 Database.xml 传递）
        public string WalkFrontLegsAngleParam { get; set; } = "WalkFrontLegsAngle";
        public string WalkHindLegsAngleParam { get; set; } = "WalkHindLegsAngle";
        public string CanterLegsAngleFactorParam { get; set; } = "CanterLegsAngleFactor";
        public string WalkBobHeightParam { get; set; } = "WalkBobHeight";

        // 动画参数（默认值，可被参数覆盖）
        public float WalkAnimationSpeed { get; set; } = 1f;
        public float WalkFrontLegsAngle { get; set; } = 0.5f;  // 弧度
        public float WalkHindLegsAngle { get; set; } = 0.5f;
        public float CanterLegsAngleFactor { get; set; } = 1f;
        public float WalkBobHeight { get; set; } = 0.1f;

        // 角度限制
        public float HeadLookMaxAngleX { get; set; } = 65f;  // 度
        public float HeadLookMaxAngleY { get; set; } = 55f;
        public float NeckLookFactor { get; set; } = 0.6f;

        // 缓存的目标骨骼
        private string[] _cachedTargetBones;
        public string[] TargetBones => _cachedTargetBones ??= new[] {
            BodyBoneName, NeckBoneName, HeadBoneName,
            Leg1BoneName, Leg2BoneName, Leg3BoneName, Leg4BoneName
        };

        // 运行时状态
        private float _speed;
        private float _movementPhase;
        private int _gait;  // 0=Walk, 1=Trot, 2=Canter
        private float _deathPhase;
        private float _lookAngleX;
        private float _lookAngleY;
        private float _feedFactor;
        private float _bob;

        // 平滑后的腿部角度
        private float _legAngle1, _legAngle2, _legAngle3, _legAngle4;
        private float _headAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            // 状态参数
            _speed = parameters.GetFloat(SpeedParam);
            _movementPhase = parameters.GetFloat(MovementPhaseParam);
            _gait = (int)parameters.GetFloat(GaitParam);
            _deathPhase = parameters.GetFloat(DeathPhaseParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _feedFactor = parameters.GetFloat(FeedFactorParam);
            _bob = parameters.GetFloat(BobParam);

            // 从参数读取配置（如果参数中有值则使用，否则使用属性默认值）
            float configValue;
            if (parameters.TryGetFloat(WalkFrontLegsAngleParam, out configValue))
                WalkFrontLegsAngle = configValue;
            if (parameters.TryGetFloat(WalkHindLegsAngleParam, out configValue))
                WalkHindLegsAngle = configValue;
            if (parameters.TryGetFloat(CanterLegsAngleFactorParam, out configValue))
                CanterLegsAngleFactor = configValue;
            if (parameters.TryGetFloat(WalkBobHeightParam, out configValue))
                WalkBobHeight = configValue;
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_deathPhase > 0f)
            {
                SampleDeathTransforms(boneTransforms, model);
            }
            else
            {
                SampleLivingTransforms(boneTransforms, model);
            }
        }

        private void SampleLivingTransforms(Matrix?[] boneTransforms, Model model)
        {
            // 计算腿部角度
            CalculateLegAngles(out float leg1, out float leg2, out float leg3, out float leg4, out float headY);

            // 平滑插值
            float smoothFactor = 12f * 0.016f;  // 假设 60fps
            _legAngle1 += smoothFactor * (leg1 - _legAngle1);
            _legAngle2 += smoothFactor * (leg2 - _legAngle2);
            _legAngle3 += smoothFactor * (leg3 - _legAngle3);
            _legAngle4 += smoothFactor * (leg4 - _legAngle4);
            _headAngleY += smoothFactor * (headY - _headAngleY);

            // 头部追踪角度（转换为弧度）
            float maxAngleXRad = HeadLookMaxAngleX * MathF.PI / 180f;
            float maxAngleYRad = HeadLookMaxAngleY * MathF.PI / 180f;
            float lookX = Math.Clamp(_lookAngleX, -maxAngleXRad, maxAngleXRad);
            float lookY = Math.Clamp(_lookAngleY + _headAngleY, -maxAngleYRad, maxAngleYRad);

            // 进食动画影响
            if (_feedFactor > 0f)
            {
                float feedY = -25f * MathF.PI / 180f;  // 简化：无噪声
                lookY = MathUtils.Lerp(lookY, feedY, _feedFactor);
            }

            // 设置身体骨骼（带 Bob）
            SetBoneTransform(boneTransforms, model, BodyBoneName,
                Matrix.CreateTranslation(0, _bob, 0));

            // 设置头部
            SetBoneTransform(boneTransforms, model, HeadBoneName,
                Matrix.CreateRotationX(lookY) * Matrix.CreateRotationZ(-lookX));

            // 设置颈部
            var neckBone = model.FindBone(NeckBoneName, false);
            if (neckBone != null)
            {
                float neckLookY = NeckLookFactor * lookY;
                float neckLookX = NeckLookFactor * lookX;
                SetBoneTransform(boneTransforms, model, NeckBoneName,
                    Matrix.CreateRotationX(neckLookY) * Matrix.CreateRotationZ(-neckLookX));
            }

            // 设置腿部
            SetBoneTransform(boneTransforms, model, Leg1BoneName, Matrix.CreateRotationX(_legAngle1));
            SetBoneTransform(boneTransforms, model, Leg2BoneName, Matrix.CreateRotationX(_legAngle2));
            SetBoneTransform(boneTransforms, model, Leg3BoneName, Matrix.CreateRotationX(_legAngle3));
            SetBoneTransform(boneTransforms, model, Leg4BoneName, Matrix.CreateRotationX(_legAngle4));
        }

        private void CalculateLegAngles(out float leg1, out float leg2, out float leg3, out float leg4, out float headY)
        {
            leg1 = leg2 = leg3 = leg4 = headY = 0f;

            if (_movementPhase == 0f || _speed < 0.1f)
                return;

            float phase = _movementPhase;
            float frontAngle = WalkFrontLegsAngle;
            float hindAngle = WalkHindLegsAngle;

            if (_gait == 2)  // Canter
            {
                float factor = CanterLegsAngleFactor;
                leg1 = frontAngle * factor * MathF.Sin((float)Math.PI * 2f * (phase + 0f));
                leg2 = frontAngle * factor * MathF.Sin((float)Math.PI * 2f * (phase + 0.25f));
                leg3 = hindAngle * factor * MathF.Sin((float)Math.PI * 2f * (phase + 0.15f));
                leg4 = hindAngle * factor * MathF.Sin((float)Math.PI * 2f * (phase + 0.4f));
                headY = 8f * MathF.PI / 180f * MathF.Sin((float)Math.PI * 2f * phase);
            }
            else if (_gait == 1)  // Trot
            {
                leg1 = frontAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0f));
                leg2 = frontAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0.5f));
                leg3 = hindAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0.5f));
                leg4 = hindAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0f));
                headY = 3f * MathF.PI / 180f * MathF.Sin((float)Math.PI * 4f * phase);
            }
            else  // Walk
            {
                leg1 = frontAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0f));
                leg2 = frontAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0.5f));
                leg3 = hindAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0.25f));
                leg4 = hindAngle * MathF.Sin((float)Math.PI * 2f * (phase + 0.75f));
                headY = 3f * MathF.PI / 180f * MathF.Sin((float)Math.PI * 4f * phase);
            }
        }

        private void SampleDeathTransforms(Matrix?[] boneTransforms, Model model)
        {
            float deathFactor = 1f - _deathPhase;

            // 身体下落 + 侧翻
            SetBoneTransform(boneTransforms, model, BodyBoneName,
                Matrix.CreateTranslation(0, -0.5f * _deathPhase, 0) *
                Matrix.CreateRotationZ((float)Math.PI / 2f * _deathPhase));

            // 头部下垂
            SetBoneTransform(boneTransforms, model, HeadBoneName,
                Matrix.CreateRotationX(50f * MathF.PI / 180f * _deathPhase));

            // 腿部收缩
            SetBoneTransform(boneTransforms, model, Leg1BoneName, Matrix.CreateRotationX(_legAngle1 * deathFactor));
            SetBoneTransform(boneTransforms, model, Leg2BoneName, Matrix.CreateRotationX(_legAngle2 * deathFactor));
            SetBoneTransform(boneTransforms, model, Leg3BoneName, Matrix.CreateRotationX(_legAngle3 * deathFactor));
            SetBoneTransform(boneTransforms, model, Leg4BoneName, Matrix.CreateRotationX(_legAngle4 * deathFactor));
        }

        private void SetBoneTransform(Matrix?[] boneTransforms, Model model, string boneName, Matrix transform)
        {
            var bone = model.FindBone(boneName, false);
            if (bone != null)
            {
                boneTransforms[bone.Index] = transform;
            }
        }
    }
}
