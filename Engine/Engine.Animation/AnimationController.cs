#nullable disable

using Engine.Animation.RootMotion;
using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// 动画控制器，管理状态机、参数和层
    /// </summary>
    public class AnimationController
    {
        private readonly Model _model;
        private readonly AnimationTemplate _template;
        private readonly AnimationLayer[] _layers;
        private readonly Dictionary<string, StateTrack> _stateTracks = new();
        private readonly AnimationParameters _parameters = new();
        private readonly AnimationBlender _blender = new();
        private readonly StateRuleEvaluator _ruleEvaluator = new();
        private readonly AnimationConfigLoader _configLoader = new();

        // 共享的表达式求值器
        private readonly ExpressionEvaluator _expressionEvaluator;

        // 根运动应用器
        private readonly TranslationApplier _translationApplier = new();
        private readonly CollisionBoxApplier _collisionBoxApplier = new();

        // 根运动缓存（按动画名称缓存）
        private readonly Dictionary<string, RootMotionCache> _rootMotionCaches = new();
        private readonly Dictionary<string, RootScaleCache> _rootScaleCaches = new();

        // 当前 Base 层的动画名称和根运动配置
        private string _currentAnimationName;
        private RootMotionConfig _currentRootMotionConfig;
        private float _prevRootMotionTime;

        // 状态规则配置（从动画配置文件加载）
        private Dictionary<string, StateTrackConfig> _stateConfigs;

        // 记录每个状态轨道当前匹配的规则索引（用于避免重复切换）
        private readonly Dictionary<string, int> _lastMatchedRuleIndex = new();

        // 动画引用配置（用于获取 OnComplete 动作）
        private Dictionary<string, AnimationReference> _animationReferences = new();

        // 当前层播放的动画别名（用于查找 OnComplete 配置）
        private readonly Dictionary<string, string> _layerAnimationAlias = new();

        // 记录层的动画播放状态（用于检测完成）
        private readonly Dictionary<string, bool> _layerWasPlaying = new();

        // 记录层的循环设置（用于判断是否是非循环动画完成）
        private readonly Dictionary<string, bool> _layerLooping = new();

        // 记录当前应用在每个层上的动画引用（用于获取 OnComplete）
        private readonly Dictionary<string, AnimationReference> _layerAnimationRef = new();

        // 记录哪些层被手动控制（跳过状态规则评估）
        private readonly HashSet<string> _manualOverrideLayers = new();

        // IK 求解器（延迟初始化）
        private IKSolver _ikSolver;

        public Model Model => _model;
        public AnimationTemplate Template => _template;
        public AnimationParameters Parameters => _parameters;
        public AnimationLayer[] Layers => _layers;

        /// <summary>
        /// 共享的表达式求值器（供动画来源使用）
        /// </summary>
        public ExpressionEvaluator ExpressionEvaluator => _expressionEvaluator;

        /// <summary>
        /// IK 求解器（延迟初始化）
        /// </summary>
        public IKSolver IKSolver
        {
            get
            {
                if (_ikSolver == null)
                {
                    _ikSolver = new IKSolver();
                }
                return _ikSolver;
            }
        }

        /// <summary>
        /// 根骨骼旋转角度（弧度），用于修正模型朝向
        /// 某些 glTF 模型的前方方向可能与游戏不一致，需要旋转修正
        /// </summary>
        public float RootBoneRotation { get; set; } = 0f;

        /// <summary>
        /// 模型缩放比例
        /// </summary>
        public float ModelScale { get; set; } = 1f;

        /// <summary>
        /// 根骨骼名称（可通过配置指定，或自动检测）
        /// </summary>
        public string RootBoneName { get; set; } = "Root";

        /// <summary>
        /// 关联的速度向量（用于根运动应用）
        /// 设置后根运动会修改此向量的值
        /// </summary>
        public Vector3? Velocity { get; set; }

        /// <summary>
        /// 关联的旋转（用于根运动坐标转换）
        /// </summary>
        public Quaternion? EntityRotation { get; set; }

        /// <summary>
        /// 默认碰撞体尺寸
        /// </summary>
        public Vector3 DefaultCollisionSize
        {
            get => _collisionBoxApplier.DefaultSize;
            set => _collisionBoxApplier.DefaultSize = value;
        }

        /// <summary>
        /// 碰撞体尺寸设置回调（由外部提供）
        /// </summary>
        public Action<Vector3> SetCollisionBox { get; set; }

        /// <summary>
        /// 动画事件触发时调用
        /// </summary>
        public event AnimationEventHandler OnAnimationEvent;

        public AnimationController(Model model, string templateName)
        {
            _model = model;
            _template = AnimationTemplateManager.Get(templateName);

            if (_template == null)
            {
                // 使用简单模板作为后备
                _template = AnimationTemplateManager.Get("Simple");
            }

            // 初始化共享的表达式求值器
            _expressionEvaluator = _ruleEvaluator.Evaluator;

            // 初始化层（按 Index 排序，确保混合顺序正确）
            _layers = new AnimationLayer[_template.Layers.Count];
            int layerIndex = 0;
            foreach (var (name, layerDef) in _template.Layers.OrderBy(kvp => kvp.Value.Index))
            {
                _layers[layerIndex] = new AnimationLayer(
                    name,
                    layerDef.Index,
                    layerDef.BlendMode,
                    layerDef.BoneMask);

                // 订阅层的动画事件（通过层的事件接口，统一处理主播放器和过渡播放器）
                _layers[layerIndex].OnAnimationEvent += ForwardAnimationEvent;
                layerIndex++;
            }

            // 初始化状态轨道
            foreach (var (name, trackDef) in _template.StateTracks)
            {
                _stateTracks[name] = new StateTrack(name, trackDef);
            }
        }

        /// <summary>
        /// 设置状态值
        /// </summary>
        public void SetState(string trackName, object value)
        {
            if (_stateTracks.TryGetValue(trackName, out var track))
            {
                track.SetValue(value);
                OnStateChanged(trackName, value);
            }
        }

        /// <summary>
        /// 获取状态值
        /// </summary>
        public object GetState(string trackName)
        {
            if (_stateTracks.TryGetValue(trackName, out var track))
                return track.Value;
            return null;
        }

        private void OnStateChanged(string trackName, object value)
        {
            // 根据状态切换动画
            switch (trackName)
            {
                case "Gait":
                    PlayGaitAnimation(value?.ToString());
                    break;
                case "Activity":
                    PlayActivityAnimation(value?.ToString());
                    break;
            }
        }

        private void PlayGaitAnimation(string gait)
        {
            if (string.IsNullOrEmpty(gait)) return;

            var baseLayer = _layers.FirstOrDefault(l => l.Name == "Base");
            if (baseLayer == null) return;

            // 查找对应动画
            var animation = _model.Animations.FirstOrDefault(a =>
                a.Name.Equals(gait, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(gait, StringComparison.OrdinalIgnoreCase));

            if (animation != null)
            {
                baseLayer.PlayAnimation(_model, animation);
            }
        }

        private void PlayActivityAnimation(string activity)
        {
            var upperBodyLayer = _layers.FirstOrDefault(l => l.Name == "UpperBody");
            if (upperBodyLayer == null) return;

            if (string.IsNullOrEmpty(activity) || activity == "None")
            {
                // 停止上半身动画
                upperBodyLayer.StopAnimation();
                return;
            }

            var animation = _model.Animations.FirstOrDefault(a =>
                a.Name.Equals(activity, StringComparison.OrdinalIgnoreCase) ||
                a.Name.Contains(activity, StringComparison.OrdinalIgnoreCase));

            if (animation != null)
            {
                upperBodyLayer.PlayAnimation(_model, animation);
            }
        }

        /// <summary>
        /// 更新控制器
        /// </summary>
        public void Update(float deltaTime)
        {
            // 1. 同步引擎参数
            SyncEngineParameters();

            // 2. 评估状态规则（仅当参数有变化时）
            if (_parameters.IsDirty)
            {
                EvaluateStateRules();
                _parameters.ClearDirty();
            }

            // 3. 更新动态属性（速度、循环状态等）
            UpdateDynamicProperties();

            // 4. 更新所有层
            foreach (var layer in _layers)
            {
                layer.Update(deltaTime, _parameters);
            }

            // 5. 应用根运动（仅 Base 层）
            ApplyRootMotion(deltaTime);

            // 6. 检查动画完成事件
            CheckAnimationCompletion();
        }

        /// <summary>
        /// 应用根运动到物理体
        /// </summary>
        private void ApplyRootMotion(float deltaTime)
        {
            // 检查是否有根运动配置
            if (_currentRootMotionConfig == null)
                return;

            // 只处理 Base 层（index 0）
            var baseLayer = _layers.FirstOrDefault(l => l.Index == 0);
            if (baseLayer == null) return;

            var player = baseLayer.AnimationPlayer;
            if (player == null || !player.IsPlaying) return;

            var animation = player.Animation;
            if (animation == null) return;

            // 检查是否需要更新缓存
            string animName = animation.Name;
            if (animName != _currentAnimationName)
            {
                _currentAnimationName = animName;
                _prevRootMotionTime = player.Time;

                // 构建缓存
                if (!_rootMotionCaches.TryGetValue(animName, out var motionCache))
                {
                    motionCache = new RootMotionCache();
                    motionCache.BuildFromAnimation(animation, RootBoneName);
                    _rootMotionCaches[animName] = motionCache;
                }

                if (!_rootScaleCaches.TryGetValue(animName, out var scaleCache))
                {
                    scaleCache = new RootScaleCache();
                    scaleCache.BuildFromAnimation(animation, RootBoneName);
                    _rootScaleCaches[animName] = scaleCache;
                }
            }

            // 获取缓存
            if (!_rootMotionCaches.TryGetValue(animName, out var rootMotionCache))
                return;
            if (!_rootScaleCaches.TryGetValue(animName, out var rootScaleCache))
                rootScaleCache = null;

            float currentTime = player.Time;
            float duration = animation.Duration;

            // 非循环动画已完成：返回零速度
            if (!player.Loop && !player.IsPlaying && player.NormalizedTime >= 1.0f)
            {
                return;
            }

            var rootMotionConfig = _currentRootMotionConfig;
            Vector3 velocity = Vector3.Zero;
            Vector3? impulse = null;

            var translationConfig = rootMotionConfig.Translation;
            if (translationConfig.Mode != TranslationMode.None && rootMotionCache.HasTranslationData)
            {
                if (translationConfig.Mode == TranslationMode.AddImpulse)
                {
                    bool loopPoint = DetectRootMotionLoopPoint(_prevRootMotionTime, currentTime, duration, player.Loop);
                    if (loopPoint)
                    {
                        impulse = CalculateRootMotionImpulse(translationConfig, rootMotionCache);
                    }
                }
                else
                {
                    velocity = rootMotionCache.GetVelocity(_prevRootMotionTime, currentTime);
                }
            }

            Vector3? scale = null;
            if (rootMotionConfig.Scale.Mode != ScaleMode.None &&
                rootMotionConfig.Scale.Source == ScaleSource.Animation &&
                rootScaleCache?.HasScaleData == true)
            {
                float normalizedTime = duration > 0 ? currentTime / duration : 0;
                scale = rootScaleCache.SampleScale(normalizedTime);
            }

            _prevRootMotionTime = currentTime;

            // 应用位移
            if (translationConfig.Mode != TranslationMode.None && Velocity.HasValue)
            {
                var vel = Velocity.Value;
                var rotation = EntityRotation ?? Quaternion.Identity;
                _translationApplier.ApplyTranslation(
                    translationConfig,
                    velocity,
                    impulse,
                    rotation,
                    ref vel,
                    deltaTime);
                Velocity = vel;
            }

            // 应用缩放
            if (rootMotionConfig.Scale.Mode != ScaleMode.None && SetCollisionBox != null)
            {
                _collisionBoxApplier.ApplyScale(
                    rootMotionConfig.Scale,
                    scale,
                    SetCollisionBox,
                    deltaTime);
            }
        }

        /// <summary>
        /// 检测根运动循环点
        /// </summary>
        private bool DetectRootMotionLoopPoint(float prevTime, float currentTime, float duration, bool isLooping)
        {
            // 循环回绕
            if (isLooping && currentTime < prevTime && prevTime > duration * 0.5f)
            {
                return true;
            }

            // 非循环动画完成
            if (!isLooping && currentTime >= duration && prevTime < duration)
            {
                return true;
            }

            return false;
        }

        /// <summary>
        /// 计算根运动冲量
        /// </summary>
        private Vector3 CalculateRootMotionImpulse(TranslationConfig config, RootMotionCache cache)
        {
            // 优先使用配置覆盖值
            if (config.ImpulseOverride.HasValue)
            {
                return config.ImpulseOverride.Value;
            }

            return config.ImpulseMethod switch
            {
                ImpulseMethod.Peak => cache.GetPeakVelocity(),
                ImpulseMethod.Weighted => (cache.GetAverageVelocity() + cache.GetPeakVelocity()) * 0.5f,
                _ => cache.GetAverageVelocity()
            };
        }

        /// <summary>
        /// 设置当前根运动配置
        /// </summary>
        public void SetRootMotionConfig(RootMotionConfig config)
        {
            _currentRootMotionConfig = config;
            _currentAnimationName = null;  // 重置动画名称，触发缓存更新
        }

        /// <summary>
        /// 更新动态属性（每帧评估表达式）
        /// </summary>
        private void UpdateDynamicProperties()
        {
            foreach (var layer in _layers)
            {
                string layerName = layer.Name;

                // 检查是否有该层的动画引用
                if (!_layerAnimationRef.TryGetValue(layerName, out var animRef) || animRef == null)
                    continue;

                var player = layer.AnimationPlayer;
                if (player == null) continue;

                // 动态速度
                var speedProp = animRef.GetSpeedProperty();
                if (speedProp.IsExpression)
                {
                    float speed = speedProp.GetValue(_parameters, _expressionEvaluator);
                    player.Speed = speed;
                }

                // 动态循环状态
                var loopProp = animRef.GetLoopProperty();
                if (loopProp.IsExpression)
                {
                    bool loop = loopProp.GetValue(_parameters, _expressionEvaluator);
                    player.Loop = loop;
                    _layerLooping[layerName] = loop;
                }
            }
        }

        /// <summary>
        /// 检查动画完成事件
        /// </summary>
        private void CheckAnimationCompletion()
        {
            foreach (var layer in _layers)
            {
                string layerName = layer.Name;
                var player = layer.AnimationPlayer;

                // 获取当前播放状态
                bool isPlaying = player?.IsPlaying ?? false;
                bool wasPlaying = _layerWasPlaying.GetValueOrDefault(layerName, false);
                bool isLooping = _layerLooping.GetValueOrDefault(layerName, true);

                // 检测非循环动画完成：之前在播放，现在停止了，且不是循环动画
                if (wasPlaying && !isPlaying && !isLooping)
                {
                    // 动画完成，执行 OnComplete 动作
                    if (_layerAnimationRef.TryGetValue(layerName, out var animRef) && animRef?.OnComplete != null)
                    {
                        ExecuteOnCompleteAction(animRef.OnComplete);
                    }
                }

                // 更新播放状态记录
                _layerWasPlaying[layerName] = isPlaying;
            }
        }

        /// <summary>
        /// 执行动画完成动作
        /// </summary>
        private void ExecuteOnCompleteAction(OnCompleteAction action)
        {
            if (action == null) return;

            switch (action.Type?.ToLowerInvariant())
            {
                case "setstate":
                    // 设置状态轨道的值
                    if (!string.IsNullOrEmpty(action.State))
                    {
                        SetState(action.State, action.Value);
                    }
                    break;

                case "trigger":
                    // 触发自定义事件
                    if (!string.IsNullOrEmpty(action.Name))
                    {
                        var evt = new AnimationEvent(action.Name, 0f, action.Data);
                        OnAnimationEvent?.Invoke(evt);
                    }
                    break;
            }
        }

        /// <summary>
        /// 设置动画引用配置（用于 OnComplete 回调查找）
        /// </summary>
        public void SetAnimationReferences(Dictionary<string, AnimationReference> references)
        {
            _animationReferences = references ?? new();
        }

        /// <summary>
        /// 设置状态规则配置
        /// </summary>
        public void SetStateConfigs(Dictionary<string, StateTrackConfig> configs)
        {
            _stateConfigs = configs;
        }

        /// <summary>
        /// 评估状态规则，根据条件自动切换动画
        /// </summary>
        private void EvaluateStateRules()
        {
            if (_stateConfigs == null)
            {
                return;
            }

            // 如果所有层都被手动控制，跳过评估
            if (_manualOverrideLayers.Count > 0 && _manualOverrideLayers.Count >= _layers.Length)
            {
                return;
            }

            foreach (var (trackName, trackConfig) in _stateConfigs)
            {
                if (string.IsNullOrEmpty(trackConfig.Layer)) continue;
                if (!_layers.Any(l => l.Name == trackConfig.Layer)) continue;

                // 跳过被手动控制的层
                if (_manualOverrideLayers.Contains(trackConfig.Layer)) continue;
                if (trackConfig.Rules == null || trackConfig.Rules.Count == 0) continue;

                // 找到匹配的规则
                int matchedIndex = -1;
                StateRuleConfig matchedRule = null;

                for (int i = 0; i < trackConfig.Rules.Count; i++)
                {
                    var rule = trackConfig.Rules[i];
                    bool result = _ruleEvaluator.EvaluateCondition(rule.Condition, _parameters);

                    if (result)
                    {
                        matchedIndex = i;
                        matchedRule = rule;
                        break;
                    }
                }

                // 如果没有匹配任何规则，保持当前状态
                if (matchedRule == null) continue;

                // 检查是否与上次匹配相同
                if (_lastMatchedRuleIndex.TryGetValue(trackName, out var lastIndex) && lastIndex == matchedIndex)
                {
                    continue; // 规则未变化，跳过
                }

                // 更新匹配记录
                _lastMatchedRuleIndex[trackName] = matchedIndex;

                // 切换动画
                if (matchedRule.Animation != null)
                {
                    ApplyAnimationToLayer(trackConfig.Layer, matchedRule.Animation);
                }
                else
                {
                    // animation: null 表示该层不激活，让下层输出可见
                    var layer = _layers.FirstOrDefault(l => l.Name == trackConfig.Layer);
                    if (layer != null)
                    {
                        // 检查层是否正在播放动画，如果是则使用过渡停用
                        if (layer.IsActive && layer.AnimationPlayer?.IsPlaying == true)
                        {
                            // 使用过渡停用，实现平滑淡出
                            layer.DeactivateWithBlend(0.2f);
                        }
                        else
                        {
                            // 没有活动动画，直接停用
                            layer.Deactivate();
                        }
                    }
                }
            }
        }

        /// <summary>
        /// 应用动画配置到指定层
        /// </summary>
        /// <returns>是否成功应用动画</returns>
        private bool ApplyAnimationToLayer(string layerName, AnimationReference animRef)
        {
            var layer = _layers.FirstOrDefault(l => l.Name == layerName);
            if (layer == null) return false;

            string source = animRef?.Source;
            if (string.IsNullOrEmpty(source)) return false;

            // 检查 source 是否是动画别名（在 animations 部分定义）
            if (_animationReferences.TryGetValue(source, out var aliasRef))
            {
                // 使用别名解析后的配置（别名配置优先，因为状态规则通常只指定 source）
                // 保留动态属性值
                animRef = new AnimationReference
                {
                    Source = aliasRef.Source,
                    SpeedValue = aliasRef.SpeedValue,
                    LoopValue = aliasRef.LoopValue,
                    StartPhaseValue = aliasRef.StartPhaseValue,
                    EndPhaseValue = aliasRef.EndPhaseValue,
                    PreservePose = aliasRef.PreservePose,
                    BlendDurationValue = aliasRef.BlendDurationValue,
                    DriverArgs = aliasRef.DriverArgs,
                    Events = aliasRef.Events,
                    OnComplete = aliasRef.OnComplete
                };
                source = animRef.Source;
            }

            // 获取动态属性值（初始静态值）
            float speed = animRef.GetSpeedProperty().IsExpression ? 1.0f : animRef.GetSpeedProperty().StaticValue;
            bool loop = animRef.GetLoopProperty().IsExpression ? true : animRef.GetLoopProperty().StaticValue;
            float blendDuration = animRef.GetBlendDurationProperty().IsExpression ? 0.3f : animRef.GetBlendDurationProperty().StaticValue;

            // 处理 driver: 语法
            if (source.StartsWith("driver:"))
            {
                string driverType = source.Substring(7);

                // 激活层
                layer.Activate();

                // 检查层是否有预配置的驱动器，且类型匹配
                if (layer.Driver != null && IsDriverTypeMatch(layer.Driver, driverType))
                {
                    // 驱动器已存在且类型匹配，只更新运行时参数（通过 driverArgs）
                    if (animRef.DriverArgs != null)
                    {
                        foreach (var kvp in animRef.DriverArgs)
                        {
                            _parameters.SetParameter(kvp.Key, kvp.Value);
                        }
                    }
                }
                else
                {
                    // 没有预配置驱动器或类型不匹配，创建新驱动器
                    // 注意：这会覆盖预配置的驱动器
                    var driver = CreateDriverFromConfig(driverType, animRef.DriverArgs);
                    if (driver != null)
                    {
                        layer.SetDriver(driver);
                    }
                    else
                    {
                        // 驱动器创建失败
                        return false;
                    }
                }

                // 驱动器相关参数通过 Parameters 传递
                if (speed != 1f)
                {
                    _parameters.SetParameter("Speed", speed);
                }

                // 驱动器没有完成概念，清除跟踪
                _layerAnimationRef.Remove(layerName);
                _layerLooping.Remove(layerName);

                return true;
            }
            // 处理 file: 语法 - 加载外部动画文件
            else if (source.StartsWith("file:"))
            {
                var animation = LoadExternalAnimation(source);
                if (animation != null)
                {
                    // 创建动画配置
                    var sourceConfig = new AnimationSourceConfig
                    {
                        Source = source,
                        SpeedValue = animRef.SpeedValue,
                        LoopValue = animRef.LoopValue,
                        StartPhaseValue = animRef.StartPhaseValue,
                        EndPhaseValue = animRef.EndPhaseValue,
                        BlendDurationValue = animRef.BlendDurationValue
                    };

                    // 根据是否有过渡时长选择播放方式
                    if (blendDuration > 0f)
                    {
                        layer.PlayAnimationWithTransition(
                            _model,
                            animation,
                            loop,
                            blendDuration);
                    }
                    else
                    {
                        layer.PlayAnimation(_model, animation, loop);
                    }

                    // 设置播放速度
                    layer.AnimationPlayer.Speed = speed;

                    // 应用相位范围（静态值；表达式由 ClipAnimationSource 处理）
                    float startPhase = animRef.GetStartPhaseProperty().IsExpression ? 0f : animRef.GetStartPhaseProperty().StaticValue;
                    float endPhase = animRef.GetEndPhaseProperty().IsExpression ? 0f : animRef.GetEndPhaseProperty().StaticValue;
                    layer.AnimationPlayer.StartPhase = startPhase;
                    layer.AnimationPlayer.EndPhase = endPhase;

                    // 应用 PreservePose
                    layer.AnimationPlayer.PreservePose = animRef.PreservePose;

                    // 添加动画事件
                    ApplyAnimationEvents(layer.AnimationPlayer, animation, animRef.Events);

                    // 记录动画引用和循环设置（用于 OnComplete）
                    _layerAnimationRef[layerName] = animRef;
                    _layerLooping[layerName] = loop;
                    _layerWasPlaying[layerName] = true;

                    return true;
                }

                // 外部文件加载失败
                return false;
            }
            // 处理动画名（模型内置动画）
            else
            {
                var animation = _model.Animations.FirstOrDefault(a =>
                    a.Name.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains(source, StringComparison.OrdinalIgnoreCase));

                if (animation == null) return false;

                // 根据是否有过渡时长选择播放方式
                if (blendDuration > 0f)
                {
                    // 使用过渡播放
                    layer.PlayAnimationWithTransition(
                        _model,
                        animation,
                        loop,
                        blendDuration);
                }
                else
                {
                    // 立即播放
                    layer.PlayAnimation(_model, animation, loop);
                }

                // 设置播放速度
                layer.AnimationPlayer.Speed = speed;

                // 应用相位范围（静态值；表达式由 ClipAnimationSource 处理）
                float startPhase = animRef.GetStartPhaseProperty().IsExpression ? 0f : animRef.GetStartPhaseProperty().StaticValue;
                float endPhase = animRef.GetEndPhaseProperty().IsExpression ? 0f : animRef.GetEndPhaseProperty().StaticValue;
                layer.AnimationPlayer.StartPhase = startPhase;
                layer.AnimationPlayer.EndPhase = endPhase;

                // 应用 PreservePose
                layer.AnimationPlayer.PreservePose = animRef.PreservePose;

                // 添加动画事件
                ApplyAnimationEvents(layer.AnimationPlayer, animation, animRef.Events);

                // 记录动画引用和循环设置（用于 OnComplete）
                _layerAnimationRef[layerName] = animRef;
                _layerLooping[layerName] = loop;
                _layerWasPlaying[layerName] = true;

                return true;
            }
        }

        /// <summary>
        /// 应用动画事件到播放器
        /// </summary>
        /// <param name="player">动画播放器</param>
        /// <param name="animation">动画（未使用，保留参数兼容性）</param>
        /// <param name="events">事件配置列表（时间使用归一化时间 0-1）</param>
        private void ApplyAnimationEvents(AnimationPlayer player, ModelAnimation animation, List<AnimationEventConfig> events)
        {
            // 清除旧事件
            player.ClearEvents();

            // 添加新事件（时间直接使用归一化时间）
            if (events != null)
            {
                foreach (var evt in events)
                {
                    player.AddEvent(evt.Name, evt.Time, evt.Data);
                }
            }
        }

        /// <summary>
        /// 根据配置创建驱动器
        /// </summary>
        private IAnimationDriver CreateDriverFromConfig(string driverType, Dictionary<string, object> args)
        {
            var driver = _configLoader.CreateDriver(driverType);

            if (driver != null && args != null)
            {
                _configLoader.ApplyDriverProperties(driver, args);
            }

            return driver;
        }

        /// <summary>
        /// 检查驱动器类型是否匹配
        /// 支持多种匹配方式：完整名称、简短名称、带/不带 Driver 后缀
        /// </summary>
        private static bool IsDriverTypeMatch(IAnimationDriver driver, string requestedType)
        {
            if (driver == null || string.IsNullOrEmpty(requestedType))
                return false;

            string driverName = driver.Name;

            // 精确匹配
            if (string.Equals(driverName, requestedType, StringComparison.OrdinalIgnoreCase))
                return true;

            // 尝试添加/移除 Driver 后缀
            string requestedWithDriver = requestedType.EndsWith("Driver", StringComparison.OrdinalIgnoreCase)
                ? requestedType
                : requestedType + "Driver";

            string requestedWithoutDriver = requestedType.EndsWith("Driver", StringComparison.OrdinalIgnoreCase)
                ? requestedType.Substring(0, requestedType.Length - 6)
                : requestedType;

            if (string.Equals(driverName, requestedWithDriver, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(driverName, requestedWithoutDriver, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        /// <summary>
        /// 加载外部动画文件
        /// 支持格式：file:path/to/animation.glb 或 file:path/animation.glb#AnimationName
        /// </summary>
        private ModelAnimation LoadExternalAnimation(string source)
        {
            // 移除 file: 前缀
            string pathAndName = source.Substring(5);

            // 解析路径和动画名称
            string filePath = pathAndName;
            string animationName = null;

            int hashIndex = pathAndName.IndexOf('#');
            if (hashIndex >= 0)
            {
                filePath = pathAndName.Substring(0, hashIndex);
                animationName = pathAndName.Substring(hashIndex + 1);
            }

            // 使用缓存加载
            var loadedData = AnimationCache.GetOrLoad(filePath, animationName, path =>
            {
                return LoadAnimationFile(path);
            });

            if (loadedData == null)
            {
                return null;
            }

            // 查找指定的动画
            if (!string.IsNullOrEmpty(animationName))
            {
                return loadedData.GetAnimation(animationName);
            }

            // 返回第一个动画
            return loadedData.Animations.FirstOrDefault();
        }

        /// <summary>
        /// 加载动画文件
        /// </summary>
        private LoadedAnimationData LoadAnimationFile(string path)
        {
            try
            {
                // 使用 GltfLoader 加载外部动画文件
                var modelData = Engine.Media.GltfLoader.LoadFromFile(path);
                if (modelData == null)
                {
                    return null;
                }

                // 创建 LoadedAnimationData，包含所有动画
                return new LoadedAnimationData(path, modelData, modelData.Animations);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 同步引擎参数（由子类扩展）
        /// </summary>
        protected virtual void SyncEngineParameters() { }

        /// <summary>
        /// 计算最终骨骼变换
        /// </summary>
        public void ComputeBoneTransforms(Matrix?[] boneTransforms)
        {
            // 1. 层混合
            _blender.BlendLayers(_layers, boneTransforms, _model);

            // 2. KHR_animation_pointer 采样（材质/纹理属性动画）
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].IsActive)
                    _layers[i].AnimationPlayer?.SamplePointerTargets(_model);
            }

            // 3. Morph target 权重采样
            for (int i = 0; i < _layers.Length; i++)
            {
                if (_layers[i].IsActive)
                    _layers[i].AnimationPlayer?.SampleMorphWeights(_model);
            }

            // 4. IK 后处理（在层混合后应用）
            _ikSolver?.Solve(boneTransforms, _model);
        }

        /// <summary>
        /// 转发动画事件
        /// </summary>
        private void ForwardAnimationEvent(AnimationEvent animationEvent)
        {
            OnAnimationEvent?.Invoke(animationEvent);
        }

        /// <summary>
        /// 为指定层添加动画事件
        /// </summary>
        /// <param name="layerName">层名称</param>
        /// <param name="eventName">事件名称</param>
        /// <param name="time">触发时间</param>
        /// <param name="parameter">可选参数</param>
        public void AddAnimationEvent(string layerName, string eventName, float time, object parameter = null)
        {
            var layer = _layers.FirstOrDefault(l => l.Name == layerName);
            if (layer != null)
            {
                layer.AnimationPlayer.AddEvent(eventName, time, parameter);
            }
        }

        /// <summary>
        /// 为所有层清除动画事件
        /// </summary>
        public void ClearAnimationEvents()
        {
            foreach (var layer in _layers)
            {
                layer.AnimationPlayer.ClearEvents();
            }
        }

        /// <summary>
        /// 为指定层设置驱动器
        /// </summary>
        /// <param name="layerName">层名称</param>
        /// <param name="driver">驱动器实例</param>
        public void SetDriver(string layerName, IAnimationDriver driver)
        {
            var layer = _layers.FirstOrDefault(l => l.Name == layerName);
            if (layer != null)
            {
                layer.SetDriver(driver);
            }
        }

        #region 手动动画控制 API

        /// <summary>
        /// 强制播放指定动画（支持配置文件中定义的别名）
        /// <para>
        /// 调用后，该层将跳过配置文件中的状态规则条件评估，
        /// 直到调用 <see cref="ReleaseManualControl"/> 释放控制权。
        /// </para>
        /// <para>
        /// <b>调用时机：</b>应在 <see cref="Update"/> 之前调用，
        /// 通常在 <see cref="SyncEngineParameters"/> 重写方法中或动画事件回调中调用。
        /// </para>
        /// </summary>
        /// <param name="layerName">层名称（如 "Base"、"Head"）</param>
        /// <param name="animationNameOrAlias">动画名或配置文件中定义的别名（如 "idle1"、"walk"）</param>
        /// <param name="loop">是否循环播放</param>
        /// <param name="blendDuration">过渡时长（秒），0 表示立即切换</param>
        /// <returns>是否成功开始播放</returns>
        public bool PlayAnimation(string layerName, string animationNameOrAlias,
            bool loop = true, float blendDuration = 0.3f)
        {
            var layer = _layers.FirstOrDefault(l => l.Name == layerName);
            if (layer == null) return false;

            // 1. 检查是否是别名
            AnimationReference animRef = null;
            if (_animationReferences.TryGetValue(animationNameOrAlias, out var aliasRef))
            {
                // 使用别名配置，但覆盖循环和过渡时长（如果显式指定）
                animRef = new AnimationReference
                {
                    Source = aliasRef.Source,
                    SpeedValue = aliasRef.SpeedValue,
                    LoopValue = loop,  // 使用参数值
                    StartPhaseValue = aliasRef.StartPhaseValue,
                    EndPhaseValue = aliasRef.EndPhaseValue,
                    PreservePose = aliasRef.PreservePose,
                    BlendDurationValue = blendDuration,  // 使用参数值
                    DriverArgs = aliasRef.DriverArgs,
                    Events = aliasRef.Events,
                    OnComplete = aliasRef.OnComplete
                };
            }
            else
            {
                // 2. 创建临时引用（直接使用动画名）
                animRef = new AnimationReference
                {
                    Source = animationNameOrAlias,
                    LoopValue = loop,
                    BlendDurationValue = blendDuration
                };
            }

            // 3. 标记该层为手动控制
            _manualOverrideLayers.Add(layerName);

            // 4. 设置保持姿态模式（非循环动画结束后保持当前姿态）
            layer.SetHoldPose(true);

            // 5. 应用到层并返回结果
            bool success = ApplyAnimationToLayer(layerName, animRef);
            if (!success)
            {
                // 应用失败时回滚状态
                layer.SetHoldPose(false);
                _manualOverrideLayers.Remove(layerName);
            }
            return success;
        }

        /// <summary>
        /// 播放外部动画文件中的动画
        /// <para>
        /// 调用后，该层将跳过配置文件中的状态规则条件评估，
        /// 直到调用 <see cref="ReleaseManualControl"/> 释放控制权。
        /// </para>
        /// <para>
        /// <b>调用时机：</b>应在 <see cref="Update"/> 之前调用。
        /// </para>
        /// </summary>
        /// <param name="layerName">层名称</param>
        /// <param name="filePath">动画文件路径（相对于 Content 目录）</param>
        /// <param name="animationName">文件中的动画名称（可选，默认使用第一个动画）</param>
        /// <param name="loop">是否循环播放</param>
        /// <param name="blendDuration">过渡时长（秒）</param>
        /// <returns>是否成功开始播放</returns>
        public bool PlayExternalAnimation(string layerName, string filePath,
            string animationName = null, bool loop = true, float blendDuration = 0.3f)
        {
            string source = string.IsNullOrEmpty(animationName)
                ? $"file:{filePath}"
                : $"file:{filePath}#{animationName}";

            var animRef = new AnimationReference
            {
                Source = source,
                LoopValue = loop,
                BlendDurationValue = blendDuration
            };

            // 标记该层为手动控制
            _manualOverrideLayers.Add(layerName);

            // 应用并返回结果
            return ApplyAnimationToLayer(layerName, animRef);
        }

        /// <summary>
        /// 停止指定层的动画播放
        /// <para>
        /// 此方法不会释放手动控制权，层仍会跳过状态规则评估。
        /// 如需恢复自动控制，请调用 <see cref="ReleaseManualControl"/>。
        /// </para>
        /// </summary>
        /// <param name="layerName">层名称</param>
        public void StopAnimation(string layerName)
        {
            var layer = _layers.FirstOrDefault(l => l.Name == layerName);
            layer?.StopAnimation();
        }

        /// <summary>
        /// 释放层的手动控制权，恢复配置文件中的状态规则自动评估
        /// <para>
        /// 释放后会强制重新评估该层的状态规则，确保动画状态正确恢复。
        /// </para>
        /// <para>
        /// <b>调用时机：</b>当手动动画播放完成，需要恢复自动状态切换时调用。
        /// 通常在动画完成回调或特定条件满足时调用。
        /// </para>
        /// </summary>
        /// <param name="layerName">层名称，为 null 时释放所有层</param>
        public void ReleaseManualControl(string layerName = null)
        {
            if (string.IsNullOrEmpty(layerName))
            {
                // 清除所有层的保持姿态状态
                foreach (var layer in _layers)
                {
                    layer?.SetHoldPose(false);
                }
                _manualOverrideLayers.Clear();
                // 清除所有规则匹配缓存，强制重新评估
                _lastMatchedRuleIndex.Clear();
            }
            else
            {
                // 清除指定层的保持姿态状态
                var layer = _layers.FirstOrDefault(l => l.Name == layerName);
                layer?.SetHoldPose(false);

                _manualOverrideLayers.Remove(layerName);

                // 清除该层相关状态轨道的规则匹配缓存
                // 通过遍历状态配置找到该层对应的轨道
                if (_stateConfigs != null)
                {
                    foreach (var (trackName, trackConfig) in _stateConfigs)
                    {
                        if (trackConfig.Layer == layerName)
                        {
                            _lastMatchedRuleIndex.Remove(trackName);
                        }
                    }
                }
            }

            // 设置参数脏标记，确保下一帧会重新评估状态规则
            // 这对于插播动画完成后立即切换到其他状态（如 Sit）很重要
            _parameters.SetDirty();
        }

        /// <summary>
        /// 检查指定层是否处于手动控制模式
        /// </summary>
        /// <param name="layerName">层名称</param>
        /// <returns>是否被手动控制</returns>
        public bool IsManualControl(string layerName)
        {
            return _manualOverrideLayers.Contains(layerName);
        }

        /// <summary>
        /// 获取配置文件中定义的动画别名列表
        /// </summary>
        /// <returns>别名列表</returns>
        public IEnumerable<string> GetAnimationAliases()
        {
            return _animationReferences.Keys;
        }

        /// <summary>
        /// 检查动画别名是否存在
        /// </summary>
        /// <param name="alias">别名</param>
        /// <returns>是否存在</returns>
        public bool HasAnimationAlias(string alias)
        {
            return _animationReferences.ContainsKey(alias);
        }

        #endregion

        #region IK API

        /// <summary>
        /// 注册 IK 链
        /// </summary>
        /// <param name="name">链名称</param>
        /// <param name="endBoneName">末端骨骼名称</param>
        /// <param name="algorithmName">算法名称（SingleBoneIK/TwoBoneIK/CCD/FABRIK），null 则自动选择</param>
        /// <param name="maxChainLength">最大链长度</param>
        public void RegisterIKChain(string name, string endBoneName,
            string algorithmName = null, int maxChainLength = 3)
        {
            IKSolver.RegisterChainByName(name, endBoneName, algorithmName, maxChainLength);
        }

        /// <summary>
        /// 设置 IK 位置目标
        /// </summary>
        /// <param name="chainName">链名称</param>
        /// <param name="targetPosition">目标位置（模型空间）</param>
        /// <param name="weight">权重（0-1）</param>
        public void SetIKTarget(string chainName, Vector3? targetPosition, float weight = 1.0f)
        {
            IKSolver.SetIKTarget(chainName, targetPosition, weight);
        }

        /// <summary>
        /// 设置 IK 方向目标
        /// </summary>
        /// <param name="chainName">链名称</param>
        /// <param name="aimDirection">目标方向（模型空间）</param>
        /// <param name="weight">权重（0-1）</param>
        public void SetIKAim(string chainName, Vector3? aimDirection, float weight = 1.0f)
        {
            IKSolver.SetIKAim(chainName, aimDirection, weight);
        }

        /// <summary>
        /// 设置完整 IK 目标
        /// </summary>
        /// <param name="chainName">链名称</param>
        /// <param name="target">IK 目标对象</param>
        public void SetIKTarget(string chainName, IKTarget target)
        {
            IKSolver.SetIKTarget(chainName, target);
        }

        /// <summary>
        /// 清除 IK 目标
        /// </summary>
        /// <param name="chainName">链名称</param>
        public void ClearIKTarget(string chainName)
        {
            IKSolver.ClearIKTarget(chainName);
        }

        /// <summary>
        /// 获取 IK 链
        /// </summary>
        public IKChain GetIKChain(string chainName)
        {
            return IKSolver.GetChain(chainName);
        }

        /// <summary>
        /// 注册 IK 链并立即构建（用于需要在注册后立即访问链对象的场景）
        /// </summary>
        /// <returns>构建的 IK 链，如果构建失败返回 null</returns>
        public IKChain RegisterAndBuildIKChain(string name, string endBoneName,
            string algorithmName = null, int maxChainLength = 3)
        {
            IKSolver.RegisterChainByName(name, endBoneName, algorithmName, maxChainLength);
            return IKSolver.BuildChainImmediate(name, _model);
        }

        /// <summary>
        /// 获取 IK 目标
        /// </summary>
        public IKTarget GetIKTarget(string chainName)
        {
            return IKSolver.GetTarget(chainName);
        }

        #endregion

        /// <summary>
        /// 清理资源，取消事件订阅
        /// </summary>
        public void Dispose()
        {
            foreach (var layer in _layers)
            {
                if (layer != null)
                {
                    layer.OnAnimationEvent -= ForwardAnimationEvent;
                }
            }
        }
    }
}
