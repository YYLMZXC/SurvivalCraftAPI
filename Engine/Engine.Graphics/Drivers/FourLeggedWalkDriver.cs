#nullable disable

namespace Engine.Graphics.Drivers
{
    /// <summary>
    /// 四足行走驱动器 - 程序化生成四足动物行走动画
    /// </summary>
    public class FourLeggedWalkDriver : IAnimationDriver
    {
        public string Name => "FourLeggedWalk";
        public BlendMode BlendMode => BlendMode.Override;

        // 目标骨骼
        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Leg1", "Leg2", "Leg3", "Leg4", "Head", "Neck" };

        // 配置参数名称
        public string PhaseParam { get; set; } = "MovementPhase";
        public string FrontAngleParam { get; set; } = "WalkFrontLegsAngle";
        public string HindAngleParam { get; set; } = "WalkHindLegsAngle";
        public string GaitParam { get; set; } = "Gait";
        public string BobParam { get; set; } = "Bob";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";
        public string FeedFactorParam { get; set; } = "FeedFactor";
        public string ButtFactorParam { get; set; } = "ButtFactor";
        public string ButtPhaseParam { get; set; } = "ButtPhase";
        public string DeathPhaseParam { get; set; } = "DeathPhase";
        public string DeathCauseOffsetParam { get; set; } = "DeathCauseOffset";
        public string GameTimeParam { get; set; } = "GameTime";
        public string BodyHeightParam { get; set; } = "BodyHeight";
        public string CanterLegsAngleFactorParam { get; set; } = "CanterLegsAngleFactor";
        public string LookAngleXParam { get; set; } = "LookAngleX";
        public string LookAngleYParam { get; set; } = "LookAngleY";
        public string IsOnGroundParam { get; set; } = "IsOnGround";
        public string ImmersionFactorParam { get; set; } = "ImmersionFactor";
        public string BodyRightParam { get; set; } = "BodyRight";

        // 可配置的相位偏移模式（与原始 ComponentFourLeggedModel 一致）
        public static readonly Dictionary<int, float[]> GaitPhasePatterns = new()
        {
            [0] = new[] { 0.0f, 0.5f, 0.25f, 0.75f },  // Walk: Leg1=0, Leg2=0.5, Leg3=0.25, Leg4=0.75
            [1] = new[] { 0.0f, 0.5f, 0.5f, 0.0f },    // Trot: Leg1=0, Leg2=0.5, Leg3=0.5, Leg4=0
            [2] = new[] { 0.0f, 0.25f, 0.15f, 0.4f },  // Canter: Leg1=0, Leg2=0.25, Leg3=0.15, Leg4=0.4
        };

        private float _phase;
        private float _frontAngle;
        private float _hindAngle;
        private int _gait;
        private float _bob;
        private float _rotationY;
        private Vector3 _position;
        private float _feedFactor;
        private float _buttFactor;
        private float _buttPhase;
        private float _deathPhase;
        private Vector3 _deathCauseOffset;
        private float _gameTime;
        private float _bodyHeight;
        private float _canterLegsAngleFactor;
        private float _lookAngleX;
        private float _lookAngleY;
        private bool _isOnGround;
        private float _immersionFactor;
        private Vector3 _bodyRight;

        // 平滑过渡用的当前角度
        private float _legAngle1, _legAngle2, _legAngle3, _legAngle4;
        private float _headAngleY;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _phase = parameters.GetFloat(PhaseParam);
            _frontAngle = parameters.GetFloat(FrontAngleParam);
            _hindAngle = parameters.GetFloat(HindAngleParam);
            _gait = (int)parameters.GetFloat(GaitParam);
            _bob = parameters.GetFloat(BobParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
            _feedFactor = parameters.GetFloat(FeedFactorParam);
            _buttFactor = parameters.GetFloat(ButtFactorParam);
            _buttPhase = parameters.GetFloat(ButtPhaseParam);
            _deathPhase = parameters.GetFloat(DeathPhaseParam);
            _deathCauseOffset = parameters.GetVector3(DeathCauseOffsetParam);
            _gameTime = parameters.GetFloat(GameTimeParam);
            _bodyHeight = parameters.GetFloat(BodyHeightParam);
            _canterLegsAngleFactor = parameters.GetFloat(CanterLegsAngleFactorParam);
            _lookAngleX = parameters.GetFloat(LookAngleXParam);
            _lookAngleY = parameters.GetFloat(LookAngleYParam);
            _isOnGround = parameters.GetBool(IsOnGroundParam);
            _immersionFactor = parameters.GetFloat(ImmersionFactorParam);
            _bodyRight = parameters.GetVector3(BodyRightParam);

            // 计算腿部角度
            // 原始条件：MovementAnimationPhase != 0f && (StandingOnValue.HasValue || ImmersionFactor > 0f)
            float targetAngle1 = 0f, targetAngle2 = 0f, targetAngle3 = 0f, targetAngle4 = 0f, targetHeadY = 0f;

            if (_phase != 0f && _deathPhase == 0f && (_isOnGround || _immersionFactor > 0f))
            {
                var phaseOffsets = GaitPhasePatterns.TryGetValue(_gait, out var offsets) ? offsets : GaitPhasePatterns[0];

                if (_gait == 2) // Canter
                {
                    float factor = _canterLegsAngleFactor > 0 ? _canterLegsAngleFactor : 1.5f;
                    targetAngle1 = _frontAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[0]));
                    targetAngle2 = _frontAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[1]));
                    targetAngle3 = _hindAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[2]));
                    targetAngle4 = _hindAngle * factor * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[3]));
                    targetHeadY = MathUtils.DegToRad(8f) * MathF.Sin(2f * MathF.PI * _phase);
                }
                else if (_gait == 1) // Trot
                {
                    targetAngle1 = _frontAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[0]));
                    targetAngle2 = _frontAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[1]));
                    targetAngle3 = _hindAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[2]));
                    targetAngle4 = _hindAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[3]));
                    targetHeadY = MathUtils.DegToRad(3f) * MathF.Sin(4f * MathF.PI * _phase);
                }
                else // Walk
                {
                    targetAngle1 = _frontAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[0]));
                    targetAngle2 = _frontAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[1]));
                    targetAngle3 = _hindAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[2]));
                    targetAngle4 = _hindAngle * MathF.Sin(2f * MathF.PI * (_phase + phaseOffsets[3]));
                    targetHeadY = MathUtils.DegToRad(3f) * MathF.Sin(4f * MathF.PI * _phase);
                }
            }

            // 平滑过渡
            float smoothFactor = MathUtils.Min(12f * deltaTime, 1f);
            _legAngle1 += smoothFactor * (targetAngle1 - _legAngle1);
            _legAngle2 += smoothFactor * (targetAngle2 - _legAngle2);
            _legAngle3 += smoothFactor * (targetAngle3 - _legAngle3);
            _legAngle4 += smoothFactor * (targetAngle4 - _legAngle4);
            _headAngleY += smoothFactor * (targetHeadY - _headAngleY);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            float deathFactor = 1f - _deathPhase;

            if (_deathPhase > 0f)
            {
                // 死亡动画
                // 原始代码：float num20 = Vector3.Dot(m_componentFrame.Matrix.Right, DeathCauseOffset) > 0f ? 1 : -1;
                float num20 = Vector3.Dot(_bodyRight, _deathCauseOffset) > 0f ? 1 : -1;

                var bodyBone = model.FindBone("Body");
                if (bodyBone != null)
                {
                    boneTransforms[bodyBone.Index] =
                        Matrix.CreateTranslation(-0.5f * _bodyHeight * Vector3.UnitY * _deathPhase)
                        * Matrix.CreateFromYawPitchRoll(_rotationY, 0f, MathF.PI / 2f * _deathPhase * num20)
                        * Matrix.CreateTranslation(0.2f * _bodyHeight * Vector3.UnitY * _deathPhase)
                        * Matrix.CreateTranslation(_position);
                }

                var headBone = model.FindBone("Head");
                if (headBone != null)
                {
                    boneTransforms[headBone.Index] = Matrix.CreateRotationX(MathUtils.DegToRad(50f) * _deathPhase);
                }

                var neckBone = model.FindBone("Neck", false);
                if (neckBone != null)
                {
                    boneTransforms[neckBone.Index] = Matrix.Identity;
                }
            }
            else
            {
                // 正常动画 - Body 骨骼（包含位置和旋转）
                var bodyBone = model.FindBone("Body");
                if (bodyBone != null)
                {
                    boneTransforms[bodyBone.Index] =
                        Matrix.CreateRotationY(_rotationY) *
                        Matrix.CreateTranslation(_position.X, _position.Y + _bob, _position.Z);
                }

                // 检查是否有 Neck 骨骼
                var neckBone = model.FindBone("Neck", false);
                bool hasNeck = neckBone != null;

                // 头部动画
                var headBone = model.FindBone("Head");
                if (headBone != null)
                {
                    float lookAngleX = Math.Clamp(_lookAngleX, -MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
                    float lookAngleY = Math.Clamp(_lookAngleY + _headAngleY, -MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));

                    // 如果有 Neck，Head 只应用 40%；否则应用全部
                    if (hasNeck)
                    {
                        lookAngleX *= 0.4f;
                        lookAngleY *= 0.4f;
                    }

                    // 进食动画
                    // 原始代码：float y = 0f - MathUtils.DegToRad(25f + 45f * SimplexNoise.OctavedNoise((float)m_subsystemTime.GameTime, 3f, 2, 2f, 0.75f));
                    if (_feedFactor > 0f)
                    {
                        // OctavedNoise 返回 0 到 1 的值
                        float noise = OctavedNoise1D(_gameTime, 3f, 2, 2f, 0.75f);
                        float feedY = -MathUtils.DegToRad(25f + 45f * noise);
                        // 进食时：X 角度趋向 0，Y 角度趋向 feedY
                        lookAngleX = MathUtils.Lerp(lookAngleX, 0f, _feedFactor);
                        lookAngleY = MathUtils.Lerp(lookAngleY, feedY, _feedFactor);
                    }

                    // 顶撞动画
                    if (_buttFactor > 0f)
                    {
                        float buttY = -MathUtils.DegToRad(40f) * MathF.Sin(MathF.PI * 2f * MathUtils.Sigmoid(_buttPhase, 4f));
                        lookAngleY = lookAngleY + (buttY - lookAngleY) * _buttFactor;
                    }

                    boneTransforms[headBone.Index] =
                        Matrix.CreateRotationX(lookAngleY) *
                        Matrix.CreateRotationZ(-lookAngleX);
                }

                // 颈部动画 - 只有存在 Neck 骨骼时才设置
                if (hasNeck)
                {
                    float lookAngleX = Math.Clamp(_lookAngleX * 0.6f, -MathUtils.DegToRad(65f), MathUtils.DegToRad(65f));
                    float lookAngleY = Math.Clamp((_lookAngleY + _headAngleY) * 0.6f, -MathUtils.DegToRad(55f), MathUtils.DegToRad(55f));

                    boneTransforms[neckBone.Index] =
                        Matrix.CreateRotationX(lookAngleY) *
                        Matrix.CreateRotationZ(-lookAngleX);
                }
            }

            // 腿部骨骼
            // 注意：对于 DAE 模型，ProcessBoneHierarchy 会保留原始位置，只替换旋转
            // 所以我们只需要设置旋转部分
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

                    // 如果角度太小（可能是参数问题），使用一个基于时间的测试角度
                    if (MathF.Abs(angle) < 0.001f && _phase != 0f)
                    {
                        // 参数可能没有正确传递，使用相位直接计算
                        angle = 0.5f * MathF.Sin(2f * MathF.PI * (_phase + i * 0.25f));
                    }

                    boneTransforms[bone.Index] = Matrix.CreateRotationX(-angle * deathFactor);
                }
            }
        }

        /// <summary>
        /// 一维噪声 - 模拟 SimplexNoise.Noise(float x)
        /// 返回 0 到 1 的值
        /// </summary>
        private static float Noise1D(float x)
        {
            int i = (int)MathF.Floor(x);
            int j = (int)MathF.Ceiling(x);
            float t = x - i;
            float n0 = Hash(i);
            float n1 = Hash(j);
            // 平滑插值
            float smooth = t * t * (3f - 2f * t);
            return n0 + smooth * (n1 - n0);
        }

        /// <summary>
        /// 哈希函数 - 返回 0 到 1
        /// </summary>
        private static float Hash(int x)
        {
            x = (x << 13) ^ x;
            return ((x * (x * x * 15731 + 789221) + 1376312589) & 0x7FFFFFFF) / 2147483648f;
        }

        /// <summary>
        /// 多倍频噪声 - 模拟 SimplexNoise.OctavedNoise
        /// 参数顺序：x, frequency, octaves, frequencyStep, amplitudeStep
        /// 返回 0 到 1 的值
        /// </summary>
        private static float OctavedNoise1D(float x, float frequency, int octaves, float frequencyStep, float amplitudeStep)
        {
            float total = 0f;
            float amplitude = 1f;
            float maxAmplitude = 0f;

            for (int i = 0; i < octaves; i++)
            {
                total += amplitude * Noise1D(x * frequency);
                maxAmplitude += amplitude;
                frequency *= frequencyStep;
                amplitude *= amplitudeStep;
            }

            return total / maxAmplitude;
        }

        /// <summary>
        /// 配置驱动器参数
        /// </summary>
        public void Configure(Dictionary<string, object> args)
        {
            if (args == null) return;

            if (args.TryGetValue("FrontAngle", out var front))
            {
                _frontAngle = Convert.ToSingle(front);
            }

            if (args.TryGetValue("HindAngle", out var hind))
            {
                _hindAngle = Convert.ToSingle(hind);
            }

            if (args.TryGetValue("PhaseParam", out var phaseParam))
            {
                PhaseParam = phaseParam?.ToString();
            }

            if (args.TryGetValue("FrontAngleParam", out var frontParam))
            {
                FrontAngleParam = frontParam?.ToString();
            }

            if (args.TryGetValue("HindAngleParam", out var hindParam))
            {
                HindAngleParam = hindParam?.ToString();
            }
        }
    }
}
