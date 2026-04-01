using Engine;
using Engine.Graphics;
using System.Collections.Generic;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 骨骼变换计算器接口
    /// 允许模组自定义单个骨骼的变换计算
    /// </summary>
    public interface IBoneTransformCalculator
    {
        /// <summary>
        /// 目标骨骼名称
        /// </summary>
        string BoneName { get; }

        /// <summary>
        /// 计算骨骼变换
        /// </summary>
        Matrix? Calculate(AnimationParameters parameters, float deltaTime);
    }

    /// <summary>
    /// 表达式驱动的骨骼变换计算器
    /// 使用数学表达式计算单个骨骼的旋转
    /// </summary>
    public class ExpressionBoneCalculator : IBoneTransformCalculator
    {
        public string BoneName { get; set; }

        /// <summary>
        /// X轴旋转表达式（弧度），如 "frontAngle * sin(PI * 2 * phase)"
        /// </summary>
        public string RotationX { get; set; }

        /// <summary>
        /// Y轴旋转表达式（弧度）
        /// </summary>
        public string RotationY { get; set; }

        /// <summary>
        /// Z轴旋转表达式（弧度）
        /// </summary>
        public string RotationZ { get; set; }

        // 缓存的表达式
        private NCalc.Expression _exprX, _exprY, _exprZ;

        public Matrix? Calculate(AnimationParameters parameters, float deltaTime)
        {
            float rx = EvaluateExpression(ref _exprX, RotationX, parameters);
            float ry = EvaluateExpression(ref _exprY, RotationY, parameters);
            float rz = EvaluateExpression(ref _exprZ, RotationZ, parameters);

            if (rx == 0f && ry == 0f && rz == 0f)
                return null;

            return Matrix.CreateRotationX(rx) * Matrix.CreateRotationY(ry) * Matrix.CreateRotationZ(rz);
        }

        private float EvaluateExpression(ref NCalc.Expression cachedExpr, string expression, AnimationParameters parameters)
        {
            if (string.IsNullOrEmpty(expression))
                return 0f;

            try
            {
                if (cachedExpr == null)
                {
                    cachedExpr = new NCalc.Expression(expression);
                }

                // 注入参数
                cachedExpr.Parameters["phase"] = parameters.GetFloat("MovementPhase");
                cachedExpr.Parameters["gait"] = parameters.GetFloat("Gait");
                cachedExpr.Parameters["speed"] = parameters.GetFloat("Speed");
                cachedExpr.Parameters["frontAngle"] = parameters.GetFloat("WalkFrontLegsAngle");
                cachedExpr.Parameters["hindAngle"] = parameters.GetFloat("WalkHindLegsAngle");
                cachedExpr.Parameters["canterFactor"] = parameters.GetFloat("CanterLegsAngleFactor");
                cachedExpr.Parameters["PI"] = MathF.PI;
                cachedExpr.Parameters["feedFactor"] = parameters.GetFloat("FeedFactor");
                cachedExpr.Parameters["deathPhase"] = parameters.GetFloat("DeathPhase");
                cachedExpr.Parameters["lookX"] = parameters.GetFloat("LookAngleX");
                cachedExpr.Parameters["lookY"] = parameters.GetFloat("LookAngleY");
                cachedExpr.Parameters["bob"] = parameters.GetFloat("Bob");
                cachedExpr.Parameters["gameTime"] = parameters.GetFloat("GameTime");

                var result = cachedExpr.Evaluate();
                return Convert.ToSingle(result);
            }
            catch
            {
                return 0f;
            }
        }
    }

    /// <summary>
    /// 步态配置
    /// </summary>
    public class GaitConfig
    {
        public float[] LegPhases { get; set; } = new float[] { 0f, 0.5f, 0.25f, 0.75f };
        public float HeadBobAmplitude { get; set; } = 3f;
        public float HeadBobFrequency { get; set; } = 4f;
    }

    /// <summary>
    /// 四足动物程序化动画驱动器
    /// 支持通过表达式覆盖特定骨骼的变换
    /// </summary>
    public class FourLeggedDriver : IAnimationDriver
    {
        public string Name => "FourLegged";
        public BlendMode BlendMode => BlendMode.Override;

        // 骨骼名称配置
        public string BodyBoneName { get; set; } = "Body";
        public string NeckBoneName { get; set; } = "Neck";
        public string HeadBoneName { get; set; } = "Head";
        public string Leg1BoneName { get; set; } = "Leg1";
        public string Leg2BoneName { get; set; } = "Leg2";
        public string Leg3BoneName { get; set; } = "Leg3";
        public string Leg4BoneName { get; set; } = "Leg4";

        // 基础动画参数
        public float WalkFrontLegsAngle { get; set; } = 0.5f;
        public float WalkHindLegsAngle { get; set; } = 0.5f;
        public float CanterLegsAngleFactor { get; set; } = 1f;
        public float HeadLookMaxAngleX { get; set; } = 65f;
        public float HeadLookMaxAngleY { get; set; } = 55f;
        public float NeckLookFactor { get; set; } = 0.6f;

        // 步态配置
        public GaitConfig WalkGait { get; set; } = new GaitConfig { LegPhases = new float[] { 0f, 0.5f, 0.25f, 0.75f }, HeadBobAmplitude = 3f, HeadBobFrequency = 4f };
        public GaitConfig TrotGait { get; set; } = new GaitConfig { LegPhases = new float[] { 0f, 0.5f, 0.5f, 0f }, HeadBobAmplitude = 3f, HeadBobFrequency = 4f };
        public GaitConfig CanterGait { get; set; } = new GaitConfig { LegPhases = new float[] { 0f, 0.25f, 0.15f, 0.4f }, HeadBobAmplitude = 8f, HeadBobFrequency = 2f };

        /// <summary>
        /// 自定义骨骼计算器（用于覆盖默认行为）
        /// 键为骨骼名称，值为计算器
        /// </summary>
        public Dictionary<string, IBoneTransformCalculator> BoneCalculators { get; set; } = new();

        /// <summary>
        /// 表达式覆盖（简化配置方式）
        /// 键为骨骼名称，值为表达式配置
        /// </summary>
        public Dictionary<string, ExpressionBoneCalculator> ExpressionOverrides { get; set; }

        private string[] _cachedTargetBones;
        public string[] TargetBones => _cachedTargetBones ??= new[] {
            BodyBoneName, NeckBoneName, HeadBoneName,
            Leg1BoneName, Leg2BoneName, Leg3BoneName, Leg4BoneName
        };

        // 运行时状态
        private Vector3 _position;
        private float _rotationY;
        private float _movementPhase;
        private int _gait;
        private float _deathPhase;
        private float _lookAngleX, _lookAngleY;
        private float _feedFactor, _bob, _gameTime;
        private Vector3 _deathCauseOffset, _bodyRight;
        private float _bodyHeight;
        private bool _isOnGround;
        private float _immersionFactor;
        private float _buttFactor, _buttPhase;
        private float _smoothFactor;
        private float _legAngle1, _legAngle2, _legAngle3, _legAngle4, _headAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _position = parameters.GetVector3("Position");
            _rotationY = parameters.GetFloat("RotationY");
            _bodyRight = parameters.GetVector3("BodyRight");
            _movementPhase = parameters.GetFloat("MovementPhase");
            _gait = (int)parameters.GetFloat("Gait");
            _deathPhase = parameters.GetFloat("DeathPhase");
            _lookAngleX = parameters.GetFloat("LookAngleX");
            _lookAngleY = parameters.GetFloat("LookAngleY");
            _feedFactor = parameters.GetFloat("FeedFactor");
            _bob = parameters.GetFloat("Bob");
            _gameTime = parameters.GetFloat("GameTime");
            _deathCauseOffset = parameters.GetVector3("DeathCauseOffset");
            _bodyHeight = parameters.GetFloat("BodyHeight");
            _isOnGround = parameters.GetBool("IsOnGround");
            _immersionFactor = parameters.GetFloat("ImmersionFactor");
            _buttFactor = parameters.GetFloat("ButtFactor");
            _buttPhase = parameters.GetFloat("ButtPhase");
            _smoothFactor = MathUtils.Min(12f * deltaTime, 1f);

            // 初始化表达式覆盖
            if (ExpressionOverrides != null)
            {
                foreach (var kvp in ExpressionOverrides)
                {
                    kvp.Value.BoneName = kvp.Key;
                    if (!BoneCalculators.ContainsKey(kvp.Key))
                    {
                        BoneCalculators[kvp.Key] = kvp.Value;
                    }
                }
            }
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_deathPhase > 0f)
                SampleDeathTransforms(boneTransforms, model);
            else
                SampleLivingTransforms(boneTransforms, model);
        }

        private void SampleLivingTransforms(Matrix?[] boneTransforms, Model model)
        {
            var gaitConfig = GetGaitConfig();
            CalculateLegAngles(gaitConfig, out float leg1, out float leg2, out float leg3, out float leg4, out float headY);

            _legAngle1 += _smoothFactor * (leg1 - _legAngle1);
            _legAngle2 += _smoothFactor * (leg2 - _legAngle2);
            _legAngle3 += _smoothFactor * (leg3 - _legAngle3);
            _legAngle4 += _smoothFactor * (leg4 - _legAngle4);
            _headAngleY += _smoothFactor * (headY - _headAngleY);

            float maxAngleXRad = HeadLookMaxAngleX * MathF.PI / 180f;
            float maxAngleYRad = HeadLookMaxAngleY * MathF.PI / 180f;
            float lookX = Math.Clamp(_lookAngleX, -maxAngleXRad, maxAngleXRad);
            float lookY = Math.Clamp(_lookAngleY + _headAngleY, -maxAngleYRad, maxAngleYRad);

            if (_feedFactor > 0f)
            {
                float noise = SimplexNoise.OctavedNoise(_gameTime, 3f, 2, 2f, 0.75f);
                float feedY = -(25f + 45f * noise) * MathF.PI / 180f;
                lookX = MathUtils.Lerp(lookX, 0f, _feedFactor);
                lookY = MathUtils.Lerp(lookY, feedY, _feedFactor);
            }

            if (_buttFactor != 0f)
            {
                float buttY = -40f * MathF.PI / 180f * MathF.Sin((float)Math.PI * 2f * MathUtils.Sigmoid(_buttPhase, 4f));
                lookY = MathUtils.Lerp(lookY, buttY, _buttFactor);
            }

            float neckX = NeckLookFactor * lookX;
            float neckY = NeckLookFactor * lookY;
            float headAngleX = lookX - neckX;
            float headAngleY = lookY - neckY;

            // 设置身体骨骼
            SetBoneWithOverride(boneTransforms, model, BodyBoneName,
                Matrix.CreateRotationY(_rotationY) * Matrix.CreateTranslation(_position.X, _position.Y + _bob, _position.Z));

            // 设置头部和颈部
            SetBoneWithOverride(boneTransforms, model, HeadBoneName,
                Matrix.CreateRotationX(headAngleY) * Matrix.CreateRotationZ(-headAngleX));

            SetBoneWithOverride(boneTransforms, model, NeckBoneName,
                Matrix.CreateRotationX(neckY) * Matrix.CreateRotationZ(-neckX));

            // 设置腿部（支持覆盖）
            SetBoneWithOverride(boneTransforms, model, Leg1BoneName, Matrix.CreateRotationX(_legAngle1));
            SetBoneWithOverride(boneTransforms, model, Leg2BoneName, Matrix.CreateRotationX(_legAngle2));
            SetBoneWithOverride(boneTransforms, model, Leg3BoneName, Matrix.CreateRotationX(_legAngle3));
            SetBoneWithOverride(boneTransforms, model, Leg4BoneName, Matrix.CreateRotationX(_legAngle4));
        }

        private void SetBoneWithOverride(Matrix?[] boneTransforms, Model model, string boneName, Matrix defaultTransform)
        {
            var bone = model.FindBone(boneName, false);
            if (bone == null) return;

            // 检查是否有自定义计算器
            if (BoneCalculators.TryGetValue(boneName, out var calculator))
            {
                // 创建临时参数容器
                var tempParams = CreateParametersForExpression();
                var customTransform = calculator.Calculate(tempParams, _smoothFactor);
                if (customTransform.HasValue)
                {
                    boneTransforms[bone.Index] = customTransform.Value;
                    return;
                }
            }

            // 使用默认变换
            boneTransforms[bone.Index] = defaultTransform;
        }

        private AnimationParameters CreateParametersForExpression()
        {
            var p = new AnimationParameters();
            p.SetFloat("MovementPhase", _movementPhase);
            p.SetFloat("Gait", _gait);
            p.SetFloat("Speed", 0f);
            p.SetFloat("WalkFrontLegsAngle", WalkFrontLegsAngle);
            p.SetFloat("WalkHindLegsAngle", WalkHindLegsAngle);
            p.SetFloat("CanterLegsAngleFactor", CanterLegsAngleFactor);
            p.SetFloat("FeedFactor", _feedFactor);
            p.SetFloat("DeathPhase", _deathPhase);
            p.SetFloat("LookAngleX", _lookAngleX);
            p.SetFloat("LookAngleY", _lookAngleY);
            p.SetFloat("Bob", _bob);
            p.SetFloat("GameTime", _gameTime);
            return p;
        }

        private GaitConfig GetGaitConfig() => _gait switch
        {
            2 => CanterGait,
            1 => TrotGait,
            _ => WalkGait
        };

        private void CalculateLegAngles(GaitConfig gait, out float leg1, out float leg2, out float leg3, out float leg4, out float headY)
        {
            leg1 = leg2 = leg3 = leg4 = headY = 0f;

            if (_movementPhase == 0f || !_isOnGround && _immersionFactor <= 0f)
                return;

            float phase = _movementPhase;
            float frontAngle = _gait == 2 ? WalkFrontLegsAngle * CanterLegsAngleFactor : WalkFrontLegsAngle;
            float hindAngle = _gait == 2 ? WalkHindLegsAngle * CanterLegsAngleFactor : WalkHindLegsAngle;

            var phases = gait.LegPhases;
            if (phases != null && phases.Length >= 4)
            {
                leg1 = frontAngle * MathF.Sin((float)Math.PI * 2f * (phase + phases[0]));
                leg2 = frontAngle * MathF.Sin((float)Math.PI * 2f * (phase + phases[1]));
                leg3 = hindAngle * MathF.Sin((float)Math.PI * 2f * (phase + phases[2]));
                leg4 = hindAngle * MathF.Sin((float)Math.PI * 2f * (phase + phases[3]));
            }

            headY = gait.HeadBobAmplitude * MathF.PI / 180f * MathF.Sin((float)Math.PI * gait.HeadBobFrequency * phase);
        }

        private void SampleDeathTransforms(Matrix?[] boneTransforms, Model model)
        {
            float deathFactor = 1f - _deathPhase;
            float rollDirection = Vector3.Dot(_bodyRight, _deathCauseOffset) > 0f ? 1f : -1f;
            if (_deathCauseOffset == Vector3.Zero) rollDirection = 1f;
            float bodyHeight = _bodyHeight > 0 ? _bodyHeight : 0.8f;

            SetBoneWithOverride(boneTransforms, model, BodyBoneName,
                Matrix.CreateTranslation(-0.5f * bodyHeight * Vector3.UnitY * _deathPhase)
                * Matrix.CreateFromYawPitchRoll(_rotationY, 0f, (float)Math.PI / 2f * _deathPhase * rollDirection)
                * Matrix.CreateTranslation(0.2f * bodyHeight * Vector3.UnitY * _deathPhase)
                * Matrix.CreateTranslation(_position));

            SetBoneWithOverride(boneTransforms, model, HeadBoneName,
                Matrix.CreateRotationX(50f * MathF.PI / 180f * _deathPhase));

            SetBoneWithOverride(boneTransforms, model, NeckBoneName, Matrix.Identity);

            SetBoneWithOverride(boneTransforms, model, Leg1BoneName, Matrix.CreateRotationX(_legAngle1 * deathFactor));
            SetBoneWithOverride(boneTransforms, model, Leg2BoneName, Matrix.CreateRotationX(_legAngle2 * deathFactor));
            SetBoneWithOverride(boneTransforms, model, Leg3BoneName, Matrix.CreateRotationX(_legAngle3 * deathFactor));
            SetBoneWithOverride(boneTransforms, model, Leg4BoneName, Matrix.CreateRotationX(_legAngle4 * deathFactor));
        }
    }
}
