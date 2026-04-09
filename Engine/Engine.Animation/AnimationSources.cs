#nullable disable

using Engine.Animation.RootMotion;
using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// 关键帧动画来源 - 包装 AnimationPlayer
    /// </summary>
    public class ClipAnimationSource : IAnimationSource
    {
        private readonly AnimationPlayer _player;
        private readonly Model _model;
        private readonly ModelAnimation _animation;
        private readonly AnimationSourceConfig _config;
        private List<AnimationEventConfig> _events;
        private readonly Dictionary<string, string> _boneRemapping;

        // Dynamic properties
        private readonly DynamicProperty<float> _speedProperty;
        private readonly DynamicProperty<bool> _loopProperty;
        private readonly DynamicProperty<float> _initialPhaseProperty;

        // Cached evaluator reference
        private ExpressionEvaluator _evaluator;

        // Cached last loop state to avoid unnecessary updates
        private bool _lastLoopState = true;

        public string Name { get; }

        public AnimationPlayer Player => _player;
        public bool IsPlaying => _player?.IsPlaying ?? false;
        public bool IsComplete => !_lastLoopState && _player != null && _player.NormalizedTime >= 1.0f;

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

        /// <summary>
        /// 根运动配置
        /// </summary>
        public RootMotionConfig RootMotionConfig => _config?.RootMotion;

        /// <summary>
        /// 创建关键帧动画来源
        /// </summary>
        /// <param name="model">模型</param>
        /// <param name="animation">动画</param>
        /// <param name="config">动画配置</param>
        /// <param name="evaluator">表达式求值器（可选，用于动态属性）</param>
        public ClipAnimationSource(Model model, ModelAnimation animation, AnimationSourceConfig config = null, ExpressionEvaluator evaluator = null)
        {
            _model = model;
            _animation = animation;
            _config = config ?? new AnimationSourceConfig();
            _evaluator = evaluator;
            Name = animation?.Name ?? "Unknown";

            // Create dynamic properties from config
            _speedProperty = _config.GetSpeedProperty();
            _loopProperty = _config.GetLoopProperty();
            _initialPhaseProperty = _config.GetInitialPhaseProperty();
            _boneRemapping = _config.BoneRemapping;

            _player = new AnimationPlayer();
            _player.SetAnimation(model, animation);

            // Get initial static values
            float speed = _speedProperty.IsExpression ? 1.0f : _speedProperty.StaticValue;
            bool loop = _loopProperty.IsExpression ? true : _loopProperty.StaticValue;
            float initialPhase = _initialPhaseProperty.IsExpression ? 0f : _initialPhaseProperty.StaticValue;

            _player.Speed = speed;
            _player.Play(loop);
            _lastLoopState = loop;

            if (initialPhase > 0)
            {
                _player.SetNormalizedTime(initialPhase);
            }

            _events = _config.Events;

            // 预分配根运动变换数组，避免每帧分配
            if (model?.Bones != null)
            {
                _rootMotionTransforms = new Matrix?[model.Bones.Count];
            }

            // 初始化根运动缓存
            InitializeRootMotionCache();
        }

        /// <summary>
        /// 初始化根运动设置（根骨骼检测和标志设置）
        /// 注意：缓存由 AnimationController 统一管理，避免重复存储
        /// </summary>
        private void InitializeRootMotionCache()
        {
            var rootMotionConfig = _config?.RootMotion;
            if (rootMotionConfig == null || _animation == null || _model == null)
                return;

            // 自动检测根骨骼名称
            var detectedName = RootMotionCache.DetectRootBoneName(_model, RootBoneName);
            if (!string.IsNullOrEmpty(detectedName))
                RootBoneName = detectedName;

            // 启用根运动提取
            // 注意：缓存由 AnimationController 维护，此处仅设置标志
            ExtractRootMotion = true;
        }

        /// <summary>
        /// 设置表达式求值器（用于动态属性）
        /// </summary>
        /// <param name="evaluator">表达式求值器</param>
        public void SetEvaluator(ExpressionEvaluator evaluator)
        {
            _evaluator = evaluator;
        }

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            if (_player == null) return;

            // Update dynamic properties
            UpdateDynamicProperties(parameters);

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

        /// <summary>
        /// 更新动态属性（速度、循环状态等）
        /// </summary>
        private void UpdateDynamicProperties(AnimationParameters parameters)
        {
            // 如果没有求值器或参数，使用静态值
            if (_evaluator == null || parameters == null)
                return;

            // 动态速度
            if (_speedProperty.IsExpression)
            {
                float speed = _speedProperty.GetValue(parameters, _evaluator);
                _player.Speed = speed;
            }

            // 动态循环状态
            if (_loopProperty.IsExpression)
            {
                bool loop = _loopProperty.GetValue(parameters, _evaluator);
                if (_lastLoopState != loop)
                {
                    _player.Loop = loop;
                    _lastLoopState = loop;
                }
            }
        }

        private bool CrossedEventPoint(float prev, float current, float eventTime)
        {
            bool isLooping = _config.LoopValue is bool b ? b : true;
            if (!isLooping)
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
