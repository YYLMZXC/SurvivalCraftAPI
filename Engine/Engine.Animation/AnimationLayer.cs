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
        private bool _holdPose;       // 手动控制期间保持当前姿态（直到释放）

        // 停用渐变过渡状态
        private bool _deactivating;
        private float _deactivateElapsed;
        private float _deactivateDuration;
        private float _originalWeight;

        // 激活渐变过渡状态（Override 层的权重渐入）
        private bool _activating;
        private float _activateElapsed;
        private float _activateDuration;
        private float _targetWeight;
        private Matrix?[] _activateSourceTransforms;  // 激活过渡时的源姿态

        /// <summary>
        /// 层名称
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// 层索引
        /// </summary>
        public int Index { get; }

        /// <summary>
        /// 动画播放器（用于 pointer 采样）
        /// </summary>
        public AnimationPlayer Player => _animationPlayer;

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
        public bool IsActive => (_active || _deactivating || _activating || _holdPose) && (
            _animationPlayer?.IsPlaying == true
            || (_animationPlayer?.PreservePose == true && _animationPlayer.HasValidAnimation)
            || (_holdPose && _animationPlayer?.HasValidAnimation == true)
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
            _holdPose = false;
            _animationPlayer?.Stop();
            _transition?.CancelTransition();
            CancelTransitioning();
        }

        /// <summary>
        /// 设置保持姿态模式（手动控制时使用）
        /// 当非循环动画结束后，保持当前姿态直到释放
        /// </summary>
        /// <param name="hold">true 表示启用保持姿态模式；false 表示禁用</param>
        public void SetHoldPose(bool hold)
        {
            _holdPose = hold;
        }

        /// <summary>
        /// 取消所有渐变过渡状态，恢复权重
        /// </summary>
        private void CancelTransitioning()
        {
            if (_activating)
            {
                _activating = false;
                Weight = _targetWeight;
                // 清除源姿态
                if (_activateSourceTransforms != null)
                {
                    Array.Clear(_activateSourceTransforms, 0, _activateSourceTransforms.Length);
                }
            }
            if (_deactivating)
            {
                _deactivating = false;
                Weight = _originalWeight;
            }
        }

        /// <summary>
        /// 带过渡效果的停用层
        /// </summary>
        /// <param name="blendDuration">过渡时长（秒）</param>
        /// <returns>是否成功开始过渡</returns>
        public bool DeactivateWithBlend(float blendDuration = 0.25f)
        {
            // 如果层已经不活动或没有播放动画，直接停用
            if (!_active || _animationPlayer == null || !_animationPlayer.IsPlaying)
            {
                Deactivate();
                return false;
            }

            // 对于 Override 模式的层，使用权重渐变过渡
            // 逐渐降低 Weight，让下层内容平滑显现
            if (BlendMode == AnimationBlendMode.Override)
            {
                if (blendDuration <= 0f)
                {
                    Deactivate();
                    return true;
                }
                _deactivating = true;
                _deactivateElapsed = 0f;
                _deactivateDuration = blendDuration;
                _originalWeight = Weight;
                return true;
            }

            // 对于 Additive 模式的层，使用过渡淡出到 Identity
            bool started = _transition.StartDeactivateTransition(_animationPlayer, blendDuration);
            if (!started)
            {
                Deactivate();
            }
            return started;
        }

        /// <summary>
        /// 激活层
        /// </summary>
        public void Activate()
        {
            _active = true;
            CancelTransitioning();
        }

        /// <summary>
        /// 播放动画（立即切换，无过渡）
        /// </summary>
        public void PlayAnimation(Model model, ModelAnimation animation, bool loop = true)
        {
            _driver = null;  // 清除驱动器
            _transition?.CancelTransition();
            CancelTransitioning();
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
            CancelTransitioning();

            // 对于 Override 层，如果之前没有任何动画（Animation == null）
            // 使用权重渐入代替动画过渡
            // 注意：如果 Animation 存在但 IsPlaying=false（非循环动画结束），
            // 应该使用正常过渡，因为我们有源姿态可以过渡
            if (BlendMode == AnimationBlendMode.Override &&
                _animationPlayer.Animation == null &&
                transitionDuration > 0f)
            {
                // 保存目标权重（当前设置的权重）
                _targetWeight = Weight > 0 ? Weight : 1f;

                // 采样当前姿态作为源姿态（用于平滑过渡）
                if (_animationPlayer.Animation != null && model != null)
                {
                    int boneCount = model.Bones.Count;
                    if (boneCount > 0)
                    {
                        if (_activateSourceTransforms == null || _activateSourceTransforms.Length < boneCount)
                        {
                            _activateSourceTransforms = new Matrix?[boneCount];
                        }
                        Array.Clear(_activateSourceTransforms, 0, boneCount);
                        _animationPlayer.SampleBoneTransforms(_activateSourceTransforms);
                    }
                }

                // 直接播放目标动画
                _animationPlayer.SetAnimation(model, animation);
                _animationPlayer.Play(loop);

                // 启动权重渐入
                Weight = 0f;
                _activating = true;
                _activateElapsed = 0f;
                _activateDuration = transitionDuration;

                return true;
            }

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
            // 更新激活渐变过渡（Override 模式的权重渐入）
            if (_activating)
            {
                _activateElapsed += deltaTime;
                float progress = _activateDuration > 0 ? _activateElapsed / _activateDuration : 1f;

                if (progress >= 1f)
                {
                    // 过渡完成，设置目标权重
                    Weight = _targetWeight;
                    _activating = false;
                    // 清除源姿态
                    if (_activateSourceTransforms != null)
                    {
                        Array.Clear(_activateSourceTransforms, 0, _activateSourceTransforms.Length);
                    }
                }
                else
                {
                    // 渐变权重：从 0 渐变到目标权重
                    Weight = _targetWeight * progress;
                }
            }

            // 更新停用渐变过渡（Override 模式的权重渐变）
            if (_deactivating)
            {
                _deactivateElapsed += deltaTime;
                float progress = _deactivateDuration > 0 ? _deactivateElapsed / _deactivateDuration : 1f;

                if (progress >= 1f)
                {
                    // 过渡完成，停用层并恢复原始权重
                    Weight = _originalWeight;
                    _deactivating = false;
                    _active = false;
                }
                else
                {
                    // 渐变权重：从原始权重渐变到 0
                    Weight = _originalWeight * (1f - progress);
                }
            }

            // 更新过渡
            if (_transition?.IsActive == true)
            {
                _transition.Update(deltaTime);

                // 检查过渡是否刚完成
                if (!_transition.IsActive)
                {
                    if (_transition.IsDeactivateTransition)
                    {
                        // 停用过渡完成，停用层
                        _transition.CompleteTransition();
                        _active = false;
                    }
                    else if (_transition.TargetPlayer != null)
                    {
                        // 过渡完成，切换到目标动画
                        _animationPlayer = _transition.TargetPlayer;
                        _transition.CompleteTransition();
                    }
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
            // 激活渐变过渡：从保存的源姿态混合到目标动画
            if (_activating && _activateSourceTransforms != null && model != null)
            {
                float progress = _activateDuration > 0 ? _activateElapsed / _activateDuration : 1f;
                progress = Math.Clamp(progress, 0f, 1f);

                // 采样目标动画
                _animationPlayer?.SampleBoneTransforms(boneTransforms);

                // 混合源姿态和目标姿态
                int boneCount = Math.Min(boneTransforms.Length, _activateSourceTransforms.Length);
                for (int i = 0; i < boneCount; i++)
                {
                    if (boneTransforms[i].HasValue && _activateSourceTransforms[i].HasValue)
                    {
                        // 两者都有值：正常混合
                        boneTransforms[i] = BlendTransforms(
                            _activateSourceTransforms[i].Value,
                            boneTransforms[i].Value,
                            progress);
                    }
                    else if (_activateSourceTransforms[i].HasValue)
                    {
                        // 只有源姿态有值：从源姿态淡出
                        boneTransforms[i] = BlendTransforms(
                            _activateSourceTransforms[i].Value,
                            Matrix.Identity,
                            progress);
                    }
                    // 如果只有目标有值，保持目标值（不需要处理）
                }
                return;
            }

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
            // preservePose: 非循环动画结束后持续保持最终帧
            else if (_animationPlayer != null && _animationPlayer.PreservePose && _animationPlayer.HasValidAnimation)
            {
                _animationPlayer.SampleBoneTransformsAtPhase(_animationPlayer.EndPhase, boneTransforms);
            }
            // holdPose: 手动控制期间非循环动画结束后保持当前姿态
            else if (_holdPose && _animationPlayer != null && _animationPlayer.HasValidAnimation)
            {
                _animationPlayer.SampleBoneTransforms(boneTransforms);
            }
            // 最后尝试驱动器
            else if (_driver != null)
            {
                _driver.SampleTransforms(boneTransforms, model);
            }
        }

        /// <summary>
        /// 混合两个变换矩阵
        /// </summary>
        private static Matrix BlendTransforms(Matrix a, Matrix b, float t)
        {
            a.Decompose(out var tA, out var rA, out var sA);
            b.Decompose(out var tB, out var rB, out var sB);

            return Matrix.CreateScale(Vector3.Lerp(sA, sB, t))
                 * Matrix.CreateFromQuaternion(Quaternion.Slerp(rA, rB, t))
                 * Matrix.CreateTranslation(Vector3.Lerp(tA, tB, t));
        }
    }
}
