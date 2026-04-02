#nullable disable

namespace Engine.Graphics
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

        public Model Model => _model;
        public AnimationTemplate Template => _template;
        public AnimationParameters Parameters => _parameters;
        public AnimationLayer[] Layers => _layers;

        /// <summary>
        /// 动画事件触发时调用
        /// </summary>
        public event AnimationEventHandler OnAnimationEvent;

        public AnimationController(Model model, string templateName)
        {
            _model = model;
            _template = AnimationTemplateRegistry.Get(templateName);

            if (_template == null)
            {
                // 使用简单模板作为后备
                _template = AnimationTemplateRegistry.Get("Simple");
            }

            // 初始化层
            _layers = new AnimationLayer[_template.Layers.Length];
            for (int i = 0; i < _template.Layers.Length; i++)
            {
                _layers[i] = new AnimationLayer(
                    _template.Layers[i].Name,
                    _template.Layers[i].Index,
                    _template.Layers[i].BlendMode,
                    _template.Layers[i].BoneMask);

                // 订阅层的动画事件
                _layers[i].AnimationPlayer.OnAnimationEvent += ForwardAnimationEvent;
            }

            // 初始化状态轨道
            foreach (var trackDef in _template.StateTracks)
            {
                _stateTracks[trackDef.Name] = new StateTrack(trackDef);
            }

            // 初始化内置驱动器
            InitializeBuiltInDrivers();
        }

        private void InitializeBuiltInDrivers()
        {
            foreach (var driverDef in _template.BuiltInDrivers)
            {
                var driver = CreateBuiltInDriver(driverDef);
                if (driver == null) continue;

                // 绑定到对应层
                var layer = _layers.FirstOrDefault(l => l.Name == driverDef.LayerName);
                if (layer != null)
                {
                    layer.SetDriver(driver);
                }
            }
        }

        private IAnimationDriver CreateBuiltInDriver(BuiltInDriverDefinition definition)
        {
            return definition.Type switch
            {
                "LookAt" => new Drivers.LookAtDriver(),
                "Death" => new Drivers.DeathDriver(),
                _ => null
            };
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

            // 3. 更新所有层
            foreach (var layer in _layers)
            {
                layer.Update(deltaTime, _parameters);
            }

            // 4. 检查动画完成事件
            CheckAnimationCompletion();
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
            if (_stateConfigs == null) return;

            foreach (var (trackName, trackConfig) in _stateConfigs)
            {
                if (string.IsNullOrEmpty(trackConfig.Layer)) continue;
                if (!_layers.Any(l => l.Name == trackConfig.Layer)) continue;
                if (trackConfig.Rules == null || trackConfig.Rules.Count == 0) continue;

                // 找到匹配的规则
                int matchedIndex = -1;
                StateRuleConfig matchedRule = null;

                for (int i = 0; i < trackConfig.Rules.Count; i++)
                {
                    var rule = trackConfig.Rules[i];
                    if (_ruleEvaluator.EvaluateCondition(rule.Condition, _parameters))
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
                    // animation: null 表示清除层的驱动器，让下层输出可见
                    var layer = _layers.FirstOrDefault(l => l.Name == trackConfig.Layer);
                    layer?.SetDriver(null);
                }
            }
        }

        /// <summary>
        /// 应用动画配置到指定层
        /// </summary>
        private void ApplyAnimationToLayer(string layerName, AnimationReference animRef)
        {
            var layer = _layers.FirstOrDefault(l => l.Name == layerName);
            if (layer == null) return;

            string source = animRef?.Source;
            if (string.IsNullOrEmpty(source)) return;

            // 处理 driver: 语法
            if (source.StartsWith("driver:"))
            {
                // 如果层已经有驱动器，复用它并更新参数
                if (layer.Driver != null)
                {
                    // 驱动器已存在，通过 driverArgs 更新参数
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
                    // 创建新驱动器
                    string driverType = source.Substring(7);
                    var driver = CreateDriverFromConfig(driverType, animRef.DriverArgs);
                    if (driver != null)
                    {
                        layer.SetDriver(driver);
                    }
                }

                // 驱动器相关参数通过 Parameters 传递
                if (animRef.Speed != 1f)
                {
                    _parameters.SetParameter("Speed", animRef.Speed);
                }

                // 驱动器没有完成概念，清除跟踪
                _layerAnimationRef.Remove(layerName);
                _layerLooping.Remove(layerName);
            }
            // 处理 file: 语法 - 加载外部动画文件
            else if (source.StartsWith("file:"))
            {
                var animation = LoadExternalAnimation(source);
                if (animation != null)
                {
                    // 根据是否有过渡时长选择播放方式
                    if (animRef.BlendDuration > 0f)
                    {
                        layer.PlayAnimationWithTransition(
                            _model,
                            animation,
                            animRef.Loop,
                            animRef.BlendDuration);
                    }
                    else
                    {
                        layer.PlayAnimation(_model, animation, animRef.Loop);
                    }

                    // 设置播放速度
                    layer.AnimationPlayer.Speed = animRef.Speed;

                    // 设置初始相位
                    if (animRef.InitialPhase > 0f)
                    {
                        layer.AnimationPlayer.SetNormalizedTime(animRef.InitialPhase);
                    }

                    // 记录动画引用和循环设置（用于 OnComplete）
                    _layerAnimationRef[layerName] = animRef;
                    _layerLooping[layerName] = animRef.Loop;
                    _layerWasPlaying[layerName] = true;
                }
            }
            // 处理动画名（模型内置动画）
            else
            {
                var animation = _model.Animations.FirstOrDefault(a =>
                    a.Name.Equals(source, StringComparison.OrdinalIgnoreCase) ||
                    a.Name.Contains(source, StringComparison.OrdinalIgnoreCase));

                if (animation != null)
                {
                    // 根据是否有过渡时长选择播放方式
                    if (animRef.BlendDuration > 0f)
                    {
                        // 使用过渡播放
                        layer.PlayAnimationWithTransition(
                            _model,
                            animation,
                            animRef.Loop,
                            animRef.BlendDuration);
                    }
                    else
                    {
                        // 立即播放
                        layer.PlayAnimation(_model, animation, animRef.Loop);
                    }

                    // 设置播放速度
                    layer.AnimationPlayer.Speed = animRef.Speed;

                    // 设置初始相位
                    if (animRef.InitialPhase > 0f)
                    {
                        layer.AnimationPlayer.SetNormalizedTime(animRef.InitialPhase);
                    }

                    // 记录动画引用和循环设置（用于 OnComplete）
                    _layerAnimationRef[layerName] = animRef;
                    _layerLooping[layerName] = animRef.Loop;
                    _layerWasPlaying[layerName] = true;
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
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[AnimationController] Failed to load animation file: {path}, Error: {ex.Message}");
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
            _blender.BlendLayers(_layers, boneTransforms, _model);
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

        /// <summary>
        /// 清理资源，取消事件订阅
        /// </summary>
        public void Dispose()
        {
            foreach (var layer in _layers)
            {
                if (layer?.AnimationPlayer != null)
                {
                    layer.AnimationPlayer.OnAnimationEvent -= ForwardAnimationEvent;
                }
            }
        }
    }
}
