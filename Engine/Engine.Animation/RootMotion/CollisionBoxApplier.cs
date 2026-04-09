using System;
using Engine;

namespace Engine.Animation.RootMotion
{
    /// <summary>
    /// 碰撞体尺寸应用器
    /// 用于根据根运动动画调整实体的碰撞体尺寸
    /// </summary>
    public class CollisionBoxApplier
    {
        private Vector3 _currentSize;
        private Vector3 _targetSize;
        private Vector3 _defaultSize;

        /// <summary>
        /// 默认碰撞体尺寸
        /// </summary>
        public Vector3 DefaultSize
        {
            get => _defaultSize;
            set
            {
                _defaultSize = value;
                _currentSize = value;
                _targetSize = value;
            }
        }

        /// <summary>
        /// 当前碰撞体尺寸
        /// </summary>
        public Vector3 CurrentSize => _currentSize;

        /// <summary>
        /// 应用缩放到碰撞体
        /// </summary>
        /// <param name="config">缩放配置</param>
        /// <param name="animationScale">动画提取的缩放值（可空）</param>
        /// <param name="setCollisionBox">设置碰撞体的回调</param>
        /// <param name="deltaTime">帧时间</param>
        public void ApplyScale(
            ScaleConfig config,
            Vector3? animationScale,
            Action<Vector3> setCollisionBox,
            float deltaTime)
        {
            if (config == null || config.Mode == ScaleMode.None)
                return;

            Vector3 targetScale;

            if (config.Source == ScaleSource.Fixed && config.Value.HasValue)
            {
                targetScale = config.Value.Value;
            }
            else if (config.Source == ScaleSource.Animation && animationScale.HasValue)
            {
                targetScale = animationScale.Value;
            }
            else
            {
                return;
            }

            // 边界处理：限制最小缩放值
            targetScale = ClampScale(targetScale, config.MinScale);

            _targetSize = _defaultSize * targetScale;

            // 平滑过渡（指数衰减）
            if (config.BlendDuration > 0 && deltaTime > 0)
            {
                // 指数衰减：每秒完成约 1/blendDuration 的剩余距离
                float decay = MathF.Exp(-deltaTime / config.BlendDuration);
                _currentSize = Vector3.Lerp(_targetSize, _currentSize, decay);
            }
            else
            {
                _currentSize = _targetSize;
            }

            setCollisionBox?.Invoke(_currentSize);
        }

        /// <summary>
        /// 缩放值边界处理
        /// </summary>
        private static Vector3 ClampScale(Vector3 scale, Vector3? minScale)
        {
            Vector3 min = minScale ?? new Vector3(0.01f, 0.01f, 0.01f);
            return Vector3.Max(scale, min);
        }

        /// <summary>
        /// 重置为默认尺寸
        /// </summary>
        public void ResetToDefault()
        {
            _targetSize = _defaultSize;
        }

        /// <summary>
        /// 更新过渡（每帧调用以实现平滑过渡）
        /// </summary>
        /// <param name="setCollisionBox">设置碰撞体的回调</param>
        /// <param name="deltaTime">帧时间</param>
        /// <param name="blendDuration">过渡时长</param>
        public void UpdateTransition(
            Action<Vector3> setCollisionBox,
            float deltaTime,
            float blendDuration = 0.2f)
        {
            if (_currentSize != _targetSize && blendDuration > 0 && deltaTime > 0)
            {
                // 指数衰减：每秒完成约 1/blendDuration 的剩余距离
                float decay = MathF.Exp(-deltaTime / blendDuration);
                _currentSize = Vector3.Lerp(_targetSize, _currentSize, decay);
                setCollisionBox?.Invoke(_currentSize);
            }
        }
    }
}
