using System;
using System.Collections.Generic;
using Engine;
using Engine.Graphics;

namespace Engine.Animation.RootMotion
{
    /// <summary>
    /// 根运动位移数据缓存
    /// </summary>
    public class RootMotionCache
    {
        private readonly List<(float time, Vector3 position)> _positionSamples = new();
        private float _animationDuration;
        private Vector3 _totalTranslation;
        private Vector3 _peakVelocity;
        private int _lastSampleIndex = 0;

        /// <summary>
        /// 是否有位移数据
        /// </summary>
        public bool HasTranslationData => _positionSamples.Count > 1;

        /// <summary>
        /// 自动检测根骨骼名称
        /// </summary>
        /// <param name="model">模型数据</param>
        /// <param name="preferredName">优先使用的名称</param>
        /// <returns>检测到的根骨骼名称，或 null</returns>
        public static string DetectRootBoneName(Model model, string preferredName = null)
        {
            // 优先使用配置指定的名称
            if (!string.IsNullOrEmpty(preferredName) && model.FindBone(preferredName) != null)
                return preferredName;

            // 回退 1：找骨骼树最顶层且有位移动画的骨骼
            foreach (var bone in model.Bones)
            {
                if (bone.ParentBone == null && HasTranslationAnimation(model, bone.Name))
                    return bone.Name;
            }

            // 回退 2：找第一个有位移动画的骨骼
            foreach (var bone in model.Bones)
            {
                if (HasTranslationAnimation(model, bone.Name))
                    return bone.Name;
            }

            return null;
        }

        private static bool HasTranslationAnimation(Model model, string boneName)
        {
            // 检查模型的所有动画是否有该骨骼的位移通道
            foreach (var anim in model.Animations)
            {
                foreach (var channel in anim.Channels)
                {
                    if (channel.TargetBoneName == boneName &&
                        channel.Property == ModelAnimation.AnimationProperty.Translation)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// 从动画数据构建缓存
        /// </summary>
        /// <param name="animation">动画数据</param>
        /// <param name="rootBoneName">根骨骼名称</param>
        public void BuildFromAnimation(ModelAnimation animation, string rootBoneName)
        {
            _positionSamples.Clear();
            _animationDuration = animation.Duration;
            _totalTranslation = Vector3.Zero;
            _peakVelocity = Vector3.Zero;
            _lastSampleIndex = 0;

            // 查找根骨骼的位移通道
            ModelAnimation.AnimationChannel translationChannel = null;
            foreach (var channel in animation.Channels)
            {
                if (channel.TargetBoneName == rootBoneName &&
                    channel.Property == ModelAnimation.AnimationProperty.Translation)
                {
                    translationChannel = channel;
                    break;
                }
            }

            if (translationChannel == null)
                return;

            var sampler = translationChannel.Sampler;
            if (sampler.KeyTimes == null || sampler.Translations == null)
                return;

            int count = Math.Min(sampler.KeyTimes.Length, sampler.Translations.Length);
            if (count == 0)
                return;

            for (int i = 0; i < count; i++)
            {
                _positionSamples.Add((sampler.KeyTimes[i], sampler.Translations[i]));
            }

            if (_positionSamples.Count > 0)
            {
                _totalTranslation = _positionSamples[^1].position - _positionSamples[0].position;
                _peakVelocity = CalculatePeakVelocity();
            }
        }

        /// <summary>
        /// 计算动画中的峰值速度
        /// </summary>
        private Vector3 CalculatePeakVelocity()
        {
            if (_positionSamples.Count < 2)
                return Vector3.Zero;

            Vector3 peakVel = Vector3.Zero;

            for (int i = 0; i < _positionSamples.Count - 1; i++)
            {
                var (t1, p1) = _positionSamples[i];
                var (t2, p2) = _positionSamples[i + 1];

                float dt = t2 - t1;
                if (dt <= 0) continue;

                Vector3 vel = (p2 - p1) / dt;
                if (vel.LengthSquared() > peakVel.LengthSquared())
                {
                    peakVel = vel;
                }
            }

            return peakVel;
        }

        /// <summary>
        /// 获取指定时间区间的位移速度（本地坐标系）
        /// </summary>
        /// <param name="prevTime">上一帧时间</param>
        /// <param name="currentTime">当前时间</param>
        /// <returns>速度向量</returns>
        public Vector3 GetVelocity(float prevTime, float currentTime)
        {
            if (_positionSamples.Count < 2)
                return Vector3.Zero;

            Vector3 prevPos = SamplePosition(prevTime);
            Vector3 currentPos = SamplePosition(currentTime);

            float deltaTime = currentTime - prevTime;
            if (deltaTime <= 0)
            {
                // 处理循环回绕
                if (currentTime < prevTime && _animationDuration > 0)
                {
                    deltaTime = (_animationDuration - prevTime) + currentTime;
                    if (deltaTime <= 0)
                        return Vector3.Zero;

                    Vector3 endPos = _positionSamples[^1].position;
                    Vector3 startPos = _positionSamples[0].position;
                    Vector3 delta = (endPos - prevPos) + (currentPos - startPos);
                    return delta / deltaTime;
                }
                return Vector3.Zero;
            }

            return (currentPos - prevPos) / deltaTime;
        }

        /// <summary>
        /// 采样指定时间的位置
        /// </summary>
        private Vector3 SamplePosition(float time)
        {
            if (_positionSamples.Count == 0)
                return Vector3.Zero;
            if (_positionSamples.Count == 1)
                return _positionSamples[0].position;

            // 处理循环
            if (_animationDuration > 0)
            {
                time = time % _animationDuration;
                if (time < 0)
                    time += _animationDuration;
            }

            // 从上次位置开始搜索（通常只需要 0-2 次比较）
            int startIdx = Math.Max(0, _lastSampleIndex - 1);
            for (int i = startIdx; i < _positionSamples.Count - 1; i++)
            {
                if (_positionSamples[i].time <= time && _positionSamples[i + 1].time >= time)
                {
                    _lastSampleIndex = i;
                    var (t1, p1) = _positionSamples[i];
                    var (t2, p2) = _positionSamples[i + 1];
                    float t = (t2 - t1) > 0 ? (time - t1) / (t2 - t1) : 0;
                    return Vector3.Lerp(p1, p2, t);
                }
            }

            // 回退到二分查找
            int left = 0, right = _positionSamples.Count - 1;
            while (left < right - 1)
            {
                int mid = (left + right) / 2;
                if (_positionSamples[mid].time <= time)
                    left = mid;
                else
                    right = mid;
            }

            _lastSampleIndex = left;
            if (left == right)
                return _positionSamples[left].position;

            {
                var (t1, p1) = _positionSamples[left];
                var (t2, p2) = _positionSamples[right];
                float t = (t2 - t1) > 0 ? (time - t1) / (t2 - t1) : 0;
                return Vector3.Lerp(p1, p2, t);
            }
        }

        /// <summary>
        /// 获取动画总位移（用于 AddImpulse 模式）
        /// </summary>
        public Vector3 GetTotalTranslation() => _totalTranslation;

        /// <summary>
        /// 获取平均速度（用于 AddImpulse 模式 Average 方式）
        /// </summary>
        public Vector3 GetAverageVelocity() =>
            _animationDuration > 0 ? _totalTranslation / _animationDuration : Vector3.Zero;

        /// <summary>
        /// 获取峰值速度（用于 AddImpulse 模式 Peak 方式）
        /// </summary>
        public Vector3 GetPeakVelocity() => _peakVelocity;
    }

    /// <summary>
    /// 根骨骼缩放数据缓存
    /// </summary>
    public class RootScaleCache
    {
        private readonly List<(float time, Vector3 scale)> _scaleSamples = new();
        private float _animationDuration;

        /// <summary>
        /// 是否有缩放数据
        /// </summary>
        public bool HasScaleData => _scaleSamples.Count > 0;

        /// <summary>
        /// 从动画数据构建缓存
        /// </summary>
        /// <param name="animation">动画数据</param>
        /// <param name="rootBoneName">根骨骼名称</param>
        public void BuildFromAnimation(ModelAnimation animation, string rootBoneName)
        {
            _scaleSamples.Clear();
            _animationDuration = animation.Duration;

            // 查找根骨骼的缩放通道
            ModelAnimation.AnimationChannel scaleChannel = null;
            foreach (var channel in animation.Channels)
            {
                if (channel.TargetBoneName == rootBoneName &&
                    channel.Property == ModelAnimation.AnimationProperty.Scale)
                {
                    scaleChannel = channel;
                    break;
                }
            }

            if (scaleChannel == null)
                return;

            var sampler = scaleChannel.Sampler;
            if (sampler.KeyTimes == null || sampler.Scales == null)
                return;

            int count = Math.Min(sampler.KeyTimes.Length, sampler.Scales.Length);
            for (int i = 0; i < count; i++)
            {
                _scaleSamples.Add((sampler.KeyTimes[i], sampler.Scales[i]));
            }
        }

        /// <summary>
        /// 采样指定时间的缩放值
        /// </summary>
        /// <param name="normalizedTime">归一化时间 (0-1)</param>
        /// <returns>缩放向量</returns>
        public Vector3 SampleScale(float normalizedTime)
        {
            if (_scaleSamples.Count == 0)
                return Vector3.One;
            if (_scaleSamples.Count == 1)
                return _scaleSamples[0].scale;

            float time = normalizedTime * _animationDuration;

            // 处理循环
            if (_animationDuration > 0)
            {
                time = time % _animationDuration;
                if (time < 0)
                    time += _animationDuration;
            }

            // 二分查找
            int left = 0, right = _scaleSamples.Count - 1;
            while (left < right - 1)
            {
                int mid = (left + right) / 2;
                if (_scaleSamples[mid].time <= time)
                    left = mid;
                else
                    right = mid;
            }

            if (left == right)
                return _scaleSamples[left].scale;

            var (t1, s1) = _scaleSamples[left];
            var (t2, s2) = _scaleSamples[right];
            float t = (t2 - t1) > 0 ? (time - t1) / (t2 - t1) : 0;
            return Vector3.Lerp(s1, s2, t);
        }
    }
}
