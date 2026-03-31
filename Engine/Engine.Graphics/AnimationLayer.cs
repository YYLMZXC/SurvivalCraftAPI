#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 动画层，负责管理单个动画或驱动器
    /// </summary>
    public class AnimationLayer
    {
        private AnimationPlayer _animationPlayer;
        private IAnimationDriver _driver;

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
        public BlendMode BlendMode { get; }

        /// <summary>
        /// 骨骼遮罩（null 表示影响所有骨骼）
        /// </summary>
        public string[] BoneMask { get; }

        /// <summary>
        /// 混合权重 (0-1)
        /// </summary>
        public float Weight { get; set; } = 1f;

        /// <summary>
        /// 是否有活动内容（动画或驱动器）
        /// </summary>
        public bool IsActive => _animationPlayer?.IsPlaying == true || _driver != null;

        /// <summary>
        /// 动画播放器
        /// </summary>
        public AnimationPlayer AnimationPlayer => _animationPlayer;

        /// <summary>
        /// 驱动器
        /// </summary>
        public IAnimationDriver Driver => _driver;

        /// <summary>
        /// 创建动画层
        /// </summary>
        public AnimationLayer(string name, int index, BlendMode blendMode, string[] boneMask = null)
        {
            Name = name;
            Index = index;
            BlendMode = blendMode;
            BoneMask = boneMask;
            _animationPlayer = new AnimationPlayer();
        }

        /// <summary>
        /// 设置驱动器
        /// </summary>
        public void SetDriver(IAnimationDriver driver)
        {
            _driver = driver;
            _animationPlayer?.Stop();
        }

        /// <summary>
        /// 播放动画
        /// </summary>
        public void PlayAnimation(Model model, ModelAnimation animation, bool loop = true)
        {
            _driver = null;  // 清除驱动器
            _animationPlayer.SetAnimation(model, animation);
            _animationPlayer.Play(loop);
        }

        /// <summary>
        /// 停止动画
        /// </summary>
        public void StopAnimation()
        {
            _animationPlayer?.Stop();
        }

        /// <summary>
        /// 更新层状态
        /// </summary>
        public void Update(float deltaTime, AnimationParameters parameters)
        {
            if (_animationPlayer?.IsPlaying == true)
            {
                _animationPlayer.Update(deltaTime);
            }
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
            if (_animationPlayer != null && _animationPlayer.IsPlaying)
            {
                _animationPlayer.SampleBoneTransforms(boneTransforms);
            }
            else if (_driver != null)
            {
                _driver.SampleTransforms(boneTransforms, model);
            }
        }
    }
}
