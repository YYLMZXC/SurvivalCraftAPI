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
        private readonly string _speedParameter;
        private readonly float _baseSpeed;
        private readonly Dictionary<string, string> _boneRemapping;

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
        private Matrix?[] _rootMotionTransforms;

        public ClipAnimationSource(Model model, ModelAnimation animation, AnimationSourceConfig config = null)
        {
            _model = model;
            _config = config ?? new AnimationSourceConfig();
            Name = animation?.Name ?? "Unknown";

            _baseSpeed = _config.Speed;
            _speedParameter = _config.SpeedParameter;
            _boneRemapping = _config.BoneRemapping;

            _player = new AnimationPlayer();
            _player.SetAnimation(model, animation);
            _player.Speed = _baseSpeed;
            _player.Play(_config.Loop);

            if (_config.InitialPhase > 0)
            {
                _player.SetNormalizedTime(_config.InitialPhase);
            }

            _events = _config.Events;

            // 预分配根运动变换数组，避免每帧分配
            if (model?.Bones != null)
            {
                _rootMotionTransforms = new Matrix?[model.Bones.Count];
            }
        }

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            if (_player == null) return;

            // 动态速度：如果设置了 SpeedParameter，从参数读取速度值
            if (!string.IsNullOrEmpty(_speedParameter) && parameters != null)
            {
                float paramSpeed = parameters.TryGetFloat(_speedParameter, out var speed) ? speed : 1.0f;
                _player.Speed = _baseSpeed * paramSpeed;
            }

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
            if (rootBone == null || _rootMotionTransforms == null) return;

            _player.SampleBoneTransforms(_rootMotionTransforms);

            if (_rootMotionTransforms[rootBone.Index].HasValue)
            {
                var currentPos = _rootMotionTransforms[rootBone.Index].Value.Translation;

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

            // 骨骼重映射处理
            if (_boneRemapping != null && _boneRemapping.Count > 0)
            {
                ApplyBoneRemapping(boneTransforms, model);
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

                boneTransforms[i].Value.Decompose(out var scale, out var rotation, out var translation);

                // 翻转 X 轴
                translation.X = -translation.X;
                rotation.Y = -rotation.Y;
                rotation.Z = -rotation.Z;

                boneTransforms[i] = Matrix.CreateScale(scale) *
                    Matrix.CreateFromQuaternion(rotation) *
                    Matrix.CreateTranslation(translation);
            }
        }

        /// <summary>
        /// 应用骨骼重映射 - 交换骨骼变换
        /// </summary>
        private void ApplyBoneRemapping(Matrix?[] boneTransforms, Model model)
        {
            // 收集需要交换的骨骼变换
            var swapped = new Dictionary<int, Matrix?>();

            foreach (var (boneA, boneB) in _boneRemapping)
            {
                var boneAInfo = model.FindBone(boneA);
                var boneBInfo = model.FindBone(boneB);

                if (boneAInfo != null && boneBInfo != null)
                {
                    // 交换两个骨骼的变换
                    swapped[boneAInfo.Index] = boneTransforms[boneBInfo.Index];
                    swapped[boneBInfo.Index] = boneTransforms[boneAInfo.Index];
                }
            }

            // 应用交换后的变换
            foreach (var (index, transform) in swapped)
            {
                boneTransforms[index] = transform;
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
