#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// 动画层，负责管理单个动画或驱动器
    /// </summary>
    public class AnimationLayer
    {
        private AnimationPlayer _animationPlayer;
        private IAnimationDriver _driver;
        private AnimationTransition _transition;
        private bool _active = true;  // 层是否激活（参与采样）

        /// <summary>
        /// 层名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 层索引
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 混合模式
        /// </summary>
        public AnimationBlendMode BlendMode { get; }

        /// <summary>
        /// 骨骼遮罩（null 表示影响所有骨骼）
        /// </summary>
        public string[] BoneMask { get; set; }

        /// <summary>
        /// 混合权重 (0-1)
        /// </summary>
        public float Weight { get; set; } = 1f;

        /// <summary>
        /// 是否有活动内容（动画或驱动器）且已激活
        /// </summary>
        public bool IsActive => _active && (
            _animationPlayer?.IsPlaying == true
            || (_animationPlayer?.PreservePose == true && _animationPlayer.HasValidAnimation)
            || _driver != null
            || _transition?.IsActive == true);

        /// <summary>
        /// 动画播放器（过渡期间返回目标播放器）
        /// </summary>
        public AnimationPlayer AnimationPlayer => _transition?.IsActive == true ? _transition.TargetPlayer : _animationPlayer;

        /// <summary>
        /// 驱动器
        /// </summary>
        public IAnimationDriver Driver => _driver;

        /// <summary>
        /// 当前过渡（如果没有活动过渡则为 null）
        /// </summary>
        public AnimationTransition Transition => _transition?.IsActive == true ? _transition : null;

        /// <summary>
        /// 动画事件触发时调用（统一转发主播放器和过渡播放器的事件）
        /// </summary>
        public event AnimationEventHandler OnAnimationEvent;

        /// <summary>
        /// 创建动画层
        /// </summary>
        public AnimationLayer(string name, int index, AnimationBlendMode blendMode, string[] boneMask = null)
        {
            Name = name;
            Index = index;
            BlendMode = blendMode;
            BoneMask = boneMask;
            _animationPlayer = new AnimationPlayer();
            _transition = new AnimationTransition();

            // 订阅主播放器的事件，转发到层的事件
            _animationPlayer.OnAnimationEvent += (evt) => OnAnimationEvent?.Invoke(evt);

            // 订阅过渡的 TargetPlayer 事件，转发到层的事件
            _transition.TargetPlayerEvent += (evt) => OnAnimationEvent?.Invoke(evt);
        }

        /// <summary>
        /// 设置驱动器
        /// </summary>
        public void SetDriver(IAnimationDriver driver)
        {
            _driver = driver;
            _animationPlayer?.Stop();
            _transition?.CancelTransition();
            _active = true;  // 设置驱动器时激活层
        }

        /// <summary>
        /// 停用层（不参与采样，但保留驱动器）
        /// </summary>
        public void Deactivate()
        {
            _active = false;
            _animationPlayer?.Stop();
            _transition?.CancelTransition();
        }

        /// <summary>
        /// 激活层
        /// </summary>
        public void Activate()
        {
            _active = true;
        }

        /// <summary>
        /// 播放动画（立即切换，无过渡）
        /// </summary>
        public void PlayAnimation(Model model, ModelAnimation animation, bool loop = true)
        {
            _driver = null;  // 清除驱动器
            _transition?.CancelTransition();
            _animationPlayer.SetAnimation(model, animation);
            _animationPlayer.Play(loop);
            _active = true;  // 激活层
        }

        /// <summary>
        /// 播放动画（带过渡效果）
        /// </summary>
        /// <param name="model">模型</param>
        /// <param name="animation">目标动画</param>
        /// <param name="loop">是否循环</param>
        /// <param name="transitionDuration">过渡时长（秒）</param>
        /// <param name="interruptMode">中断模式</param>
        /// <param name="priority">过渡优先级</param>
        /// <returns>是否成功开始播放</returns>
        public bool PlayAnimationWithTransition(
            Model model,
            ModelAnimation animation,
            bool loop = true,
            float transitionDuration = 0.25f,
            TransitionInterruptMode interruptMode = TransitionInterruptMode.CanInterrupt,
            int priority = 0)
        {
            _driver = null;  // 清除驱动器
            _active = true;  // 激活层

            // 检查是否可以开始新过渡
            if (!_transition.CanStartNewTransition(priority))
                return false;

            // 开始过渡
            bool started = _transition.StartTransition(
                _animationPlayer,
                model,
                animation,
                loop,
                transitionDuration,
                interruptMode,
                priority);

            if (started)
            {
                // 同时更新主播放器，这样外部查询和速度设置都能正常工作
                _animationPlayer.SetAnimation(model, animation);
                _animationPlayer.Play(loop);
            }

            return started;
        }

        /// <summary>
        /// 交叉淡入淡出到新动画
        /// </summary>
        /// <param name="model">模型</param>
        /// <param name="animation">目标动画</param>
        /// <param name="duration">过渡时长</param>
        /// <param name="loop">是否循环</param>
        /// <returns>是否成功开始交叉淡入淡出</returns>
        public bool CrossFade(
            Model model,
            ModelAnimation animation,
            float duration,
            bool loop = true)
        {
            return PlayAnimationWithTransition(
                model,
                animation,
                loop,
                duration,
                TransitionInterruptMode.CanInterrupt);
        }

        /// <summary>
        /// 停止动画
        /// </summary>
        public void StopAnimation()
        {
            _transition?.CancelTransition();
            _animationPlayer?.Stop();
        }

        /// <summary>
        /// 更新层状态
        /// </summary>
        public void Update(float deltaTime, AnimationParameters parameters)
        {
            // 更新过渡
            if (_transition?.IsActive == true)
            {
                _transition.Update(deltaTime);

                // 检查过渡是否刚完成
                if (!_transition.IsActive && _transition.TargetPlayer != null)
                {
                    // 过渡完成，切换到目标动画
                    _animationPlayer = _transition.TargetPlayer;
                    _transition.CompleteTransition();
                }
            }
            // 更新普通动画
            else if (_animationPlayer?.IsPlaying == true)
            {
                _animationPlayer.Update(deltaTime);
            }

            // 更新驱动器
            if (_driver != null)
            {
                _driver.Update(deltaTime, parameters);
            }
        }

        /// <summary>
        /// 采样当前层的骨骼变换
        /// </summary>
        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            // 如果有活动过渡，使用过渡采样
            if (_transition?.IsActive == true)
            {
                _transition.SampleTransforms(boneTransforms, model);
            }
            // 否则使用普通动画采样
            else if (_animationPlayer != null && _animationPlayer.IsPlaying)
            {
                _animationPlayer.SampleBoneTransforms(boneTransforms);
            }
            // preservePose: 非循环动画结束后保持最后帧
            else if (_animationPlayer != null && _animationPlayer.PreservePose && _animationPlayer.HasValidAnimation)
            {
                _animationPlayer.SampleBoneTransformsAtPhase(_animationPlayer.EndPhase, boneTransforms);
            }
            // 最后尝试驱动器
            else if (_driver != null)
            {
                _driver.SampleTransforms(boneTransforms, model);
            }
        }
    }
}
