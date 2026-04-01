#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 关键帧动画来源 - 包装 AnimationPlayer
    /// </summary>
    public class ClipAnimationSource : IAnimationSource
    {
        private readonly AnimationPlayer _player;
        private readonly Model _model;
        private readonly AnimationSourceConfig _config;
        private List<AnimationEventConfig> _events;

        public string Name { get; }

        public AnimationPlayer Player => _player;
        public bool IsPlaying => _player?.IsPlaying ?? false;
        public bool IsComplete => !_config.Loop && _player != null && _player.NormalizedTime >= 1.0f;

        /// <summary>
        /// 动画事件
        /// </summary>
        public event Action<string, string> OnAnimationEvent;

        /// <summary>
        /// 根运动相关
        /// </summary>
        public bool ExtractRootMotion { get; set; }
        public string RootBoneName { get; set; } = "Root";
        public Vector3 RootMotionDelta { get; private set; }
        private Vector3 _lastRootPosition;
        private bool _rootMotionInitialized;

        public ClipAnimationSource(Model model, ModelAnimation animation, AnimationSourceConfig config = null)
        {
            _model = model;
            _config = config ?? new AnimationSourceConfig();
            Name = animation?.Name ?? "Unknown";

            _player = new AnimationPlayer();
            _player.SetAnimation(model, animation);
            _player.Speed = _config.Speed;
            _player.Play(_config.Loop);

            if (_config.InitialPhase > 0)
            {
                _player.SetNormalizedTime(_config.InitialPhase);
            }

            _events = _config.Events;
        }

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            if (_player == null) return;

            float prevTime = _player.NormalizedTime;
            _player.Update(deltaTime);

            // 检查事件触发
            if (_events != null && _player.IsPlaying)
            {
                foreach (var evt in _events)
                {
                    bool crossed = CrossedEventPoint(prevTime, _player.NormalizedTime, evt.Time);
                    if (crossed)
                    {
                        OnAnimationEvent?.Invoke(evt.Name, evt.Data);
                    }
                }
            }

            // 根运动提取
            if (ExtractRootMotion)
            {
                ExtractRootMotionDelta();
            }
        }

        private bool CrossedEventPoint(float prev, float current, float eventTime)
        {
            if (!_config.Loop)
            {
                return prev < eventTime && current >= eventTime;
            }
            else
            {
                if (current >= prev)
                {
                    return prev < eventTime && current >= eventTime;
                }
                else
                {
                    return prev < eventTime || current >= eventTime;
                }
            }
        }

        private void ExtractRootMotionDelta()
        {
            var rootBone = _model.FindBone(RootBoneName);
            if (rootBone == null) return;

            var tempTransforms = new Matrix?[_model.Bones.Count];
            _player.SampleBoneTransforms(tempTransforms);

            if (tempTransforms[rootBone.Index].HasValue)
            {
                var currentPos = tempTransforms[rootBone.Index].Value.Translation;

                if (!_rootMotionInitialized)
                {
                    _lastRootPosition = currentPos;
                    _rootMotionInitialized = true;
                    RootMotionDelta = Vector3.Zero;
                }
                else
                {
                    RootMotionDelta = currentPos - _lastRootPosition;
                    _lastRootPosition = currentPos;
                }
            }
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_player == null) return;

            _player.SampleBoneTransforms(boneTransforms);

            // 镜像处理
            if (_config.Mirror)
            {
                ApplyMirror(boneTransforms, model);
            }

            // 根运动模式下，根骨骼位移已被提取
            if (ExtractRootMotion)
            {
                var rootBone = model.FindBone(RootBoneName);
                if (rootBone != null && boneTransforms[rootBone.Index].HasValue)
                {
                    var transform = boneTransforms[rootBone.Index].Value;
                    // 保留旋转，清除位移
                    transform.Decompose(out _, out var rotation, out _);
                    boneTransforms[rootBone.Index] = Matrix.CreateFromQuaternion(rotation);
                }
            }
        }

        private void ApplyMirror(Matrix?[] boneTransforms, Model model)
        {
            for (int i = 0; i < boneTransforms.Length; i++)
            {
                if (!boneTransforms[i].HasValue) continue;

                var m = boneTransforms[i].Value;
                m.Decompose(out var scale, out var rotation, out var translation);

                // 翻转 X 轴
                translation.X = -translation.X;
                rotation.Y = -rotation.Y;
                rotation.Z = -rotation.Z;

                boneTransforms[i] = Matrix.CreateScale(scale) *
                    Matrix.CreateFromQuaternion(rotation) *
                    Matrix.CreateTranslation(translation);
            }
        }

        public void Reset()
        {
            _player?.Stop();
            _rootMotionInitialized = false;
            RootMotionDelta = Vector3.Zero;
        }
    }

    /// <summary>
    /// 驱动器动画来源 - 包装 IAnimationDriver
    /// </summary>
    public class DriverAnimationSource : IAnimationSource
    {
        private readonly IAnimationDriver _driver;

        public string Name => _driver?.Name ?? "Driver";
        public IAnimationDriver Driver => _driver;

        public DriverAnimationSource(IAnimationDriver driver)
        {
            _driver = driver;
        }

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            _driver?.Update(deltaTime, parameters);
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            _driver?.SampleTransforms(boneTransforms, model);
        }
    }
}
