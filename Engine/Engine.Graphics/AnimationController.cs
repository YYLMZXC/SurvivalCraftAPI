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

            // 2. 更新所有层
            foreach (var layer in _layers)
            {
                layer.Update(deltaTime, _parameters);
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
    }
}
