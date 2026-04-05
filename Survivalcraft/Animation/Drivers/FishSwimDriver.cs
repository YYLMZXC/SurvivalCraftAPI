#nullable disable
using Engine;
using Engine.Animation;
using Engine.Graphics;

namespace Game.Animation.Drivers
{
    /// <summary>
    /// 鱼类游泳驱动器 - 处理游泳时的尾巴摆动动画
    /// </summary>
    public class FishSwimDriver : IAnimationDriver
    {
        public string Name => "FishSwim";
        public AnimationBlendMode BlendMode => AnimationBlendMode.Override;

        public string[] TargetBones => _targetBones;
        private string[] _targetBones = new[] { "Body", "Tail1", "Tail2" };

        // 参数名称
        public string PhaseParam { get; set; } = "MovementPhase";
        public string TailWagPhaseParam { get; set; } = "TailWagPhase";
        public string TailTurnXParam { get; set; } = "TailTurnX";
        public string TailTurnYParam { get; set; } = "TailTurnY";
        public string HasVerticalTailParam { get; set; } = "HasVerticalTail";
        public string RotationYParam { get; set; } = "RotationY";
        public string PositionParam { get; set; } = "Position";
        public string DigInDepthParam { get; set; } = "DigInDepth";
        public string IsEmbeddedInIceParam { get; set; } = "IsEmbeddedInIce";

        // 可配置属性
        public float Tail1Angle { get; set; } = 25f;
        public float Tail2Angle { get; set; } = 30f;
        public float TailPhaseOffset { get; set; } = 0.25f;
        public float SmoothSpeed { get; set; } = 12f;

        private float _phase;
        private float _tailWagPhase;
        private float _tailTurnX;
        private float _tailTurnY;
        private bool _hasVerticalTail;
        private float _rotationY;
        private Vector3 _position;
        private float _digInDepth;
        private bool _isEmbeddedInIce;

        // 平滑过渡用的当前值
        private float _currentTail1Z = 0f;
        private float _currentTail1X = 0f;
        private float _currentTail2Z = 0f;
        private float _currentTail2X = 0f;
        private bool _firstUpdate = true;

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _phase = parameters.GetFloat(PhaseParam);
            _tailWagPhase = parameters.GetFloat(TailWagPhaseParam);
            _tailTurnX = parameters.GetFloat(TailTurnXParam);
            _tailTurnY = parameters.GetFloat(TailTurnYParam);
            _hasVerticalTail = parameters.GetBool(HasVerticalTailParam);
            _rotationY = parameters.GetFloat(RotationYParam);
            _position = parameters.GetVector3(PositionParam);
            _digInDepth = parameters.GetFloat(DigInDepthParam);
            _isEmbeddedInIce = parameters.GetBool(IsEmbeddedInIceParam);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            // Body 骨骼
            var bodyBone = model.FindBone("Body");
            if (bodyBone != null)
            {
                // 位置下沉（如果嵌入冰中）
                float yOffset = -_digInDepth;
                boneTransforms[bodyBone.Index] =
                    Matrix.CreateRotationY(_rotationY) *
                    Matrix.CreateTranslation(_position.X, _position.Y + yOffset, _position.Z);
            }

            // 如果嵌入冰中，使用 DigIn 动画而不是游泳动画
            if (_isEmbeddedInIce)
            {
                // 嵌入冰中时，尾巴使用 DigInTailPhase
                float digInPhase = _tailWagPhase; // 这里复用 TailWagPhase 作为 DigInTailPhase

                float tail1Angle = MathUtils.DegToRad(Tail1Angle) * 0.5f * MathF.Sin(MathF.PI * 2f * digInPhase);
                float tail2Angle = MathUtils.DegToRad(Tail2Angle) * 0.5f * MathF.Sin(2f * (MathF.PI * MathUtils.Max(digInPhase - TailPhaseOffset, 0f)));

                var tail1Bone = model.FindBone("Tail1");
                var tail2Bone = model.FindBone("Tail2");

                if (tail1Bone != null)
                {
                    boneTransforms[tail1Bone.Index] = Matrix.CreateRotationZ(tail1Angle);
                }
                if (tail2Bone != null)
                {
                    boneTransforms[tail2Bone.Index] = Matrix.CreateRotationZ(tail2Angle);
                }
                return;
            }

            // 计算尾巴摆动角度
            float tail1Z, tail1X, tail2Z, tail2X;

            if (_hasVerticalTail)
            {
                // 垂直尾巴：垂直方向由 tailTurn.Y 控制，水平方向由游泳相位控制
                tail1Z = MathUtils.DegToRad(Tail1Angle) * Math.Clamp(0.5f * MathF.Sin(MathF.PI * 2f * _tailWagPhase) - _tailTurnX, -1f, 1f);
                tail2Z = MathUtils.DegToRad(Tail2Angle) * Math.Clamp(0.5f * MathF.Sin(2f * (MathF.PI * MathUtils.Max(_tailWagPhase - TailPhaseOffset, 0f))) - _tailTurnX, -1f, 1f);

                tail1X = MathUtils.DegToRad(Tail1Angle) * Math.Clamp(0.5f * MathF.Sin(MathF.PI * 2f * _phase) - _tailTurnY, -1f, 1f);
                tail2X = MathUtils.DegToRad(Tail2Angle) * Math.Clamp(0.5f * MathF.Sin(MathF.PI * 2f * MathUtils.Max(_phase - TailPhaseOffset, 0f)) - _tailTurnY, -1f, 1f);
            }
            else
            {
                // 水平尾巴：游泳相位控制水平摆动，tailTurn.Y 控制垂直
                float combinedPhase = _phase + _tailWagPhase;

                tail1Z = MathUtils.DegToRad(Tail1Angle) * Math.Clamp(0.5f * MathF.Sin(MathF.PI * 2f * combinedPhase) - _tailTurnX, -1f, 1f);
                tail2Z = MathUtils.DegToRad(Tail2Angle) * Math.Clamp(0.5f * MathF.Sin(2f * (MathF.PI * MathUtils.Max(combinedPhase - TailPhaseOffset, 0f))) - _tailTurnX, -1f, 1f);

                tail1X = MathUtils.DegToRad(Tail1Angle) * Math.Clamp(-_tailTurnY, -1f, 1f);
                tail2X = MathUtils.DegToRad(Tail2Angle) * Math.Clamp(-_tailTurnY, -1f, 1f);
            }

            // 平滑过渡
            if (_firstUpdate)
            {
                _currentTail1Z = tail1Z;
                _currentTail1X = tail1X;
                _currentTail2Z = tail2Z;
                _currentTail2X = tail2X;
                _firstUpdate = false;
            }
            else
            {
                // 这里不使用平滑过渡，因为尾巴摆动是快速动画
                _currentTail1Z = tail1Z;
                _currentTail1X = tail1X;
                _currentTail2Z = tail2Z;
                _currentTail2X = tail2X;
            }

            // Tail1 骨骼
            var tail1Bone2 = model.FindBone("Tail1");
            if (tail1Bone2 != null)
            {
                Matrix transform = Matrix.Identity;
                if (_currentTail1Z != 0f)
                {
                    transform *= Matrix.CreateRotationZ(_currentTail1Z);
                }
                if (_currentTail1X != 0f)
                {
                    transform *= Matrix.CreateRotationX(_currentTail1X);
                }
                boneTransforms[tail1Bone2.Index] = transform;
            }

            // Tail2 骨骼
            var tail2Bone2 = model.FindBone("Tail2");
            if (tail2Bone2 != null)
            {
                Matrix transform = Matrix.Identity;
                if (_currentTail2Z != 0f)
                {
                    transform *= Matrix.CreateRotationZ(_currentTail2Z);
                }
                if (_currentTail2X != 0f)
                {
                    transform *= Matrix.CreateRotationX(_currentTail2X);
                }
                boneTransforms[tail2Bone2.Index] = transform;
            }
        }
    }
}
