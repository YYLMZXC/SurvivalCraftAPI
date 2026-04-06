#nullable disable

using Engine.Graphics;

namespace Engine.Animation {
    /// <summary>
    /// 动画事件数据
    /// </summary>
    public class AnimationEvent {
        /// <summary>
        /// 事件名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 事件触发时间（秒）
        /// </summary>
        public float Time { get; set; }

        /// <summary>
        /// 事件参数（可选）
        /// </summary>
        public object Parameter { get; set; }

        public AnimationEvent(string name, float time, object parameter = null) {
            Name = name;
            Time = time;
            Parameter = parameter;
        }
    }

    /// <summary>
    /// 动画事件处理器委托
    /// </summary>
    /// <param name="animationEvent">触发的事件</param>
    public delegate void AnimationEventHandler(AnimationEvent animationEvent);

    /// <summary>
    /// 动画播放器，负责采样和插值
    /// </summary>
    public class AnimationPlayer {
        Model _model;
        ModelAnimation _animation;
        float _time;
        float _previousTime;
        bool _looping;
        bool _playing;
        Dictionary<string, int> _boneNameToIndex = new();
        List<AnimationEvent> _events = new();
        int _lastEventIndex = -1;

        /// <summary>
        /// 当前动画
        /// </summary>
        public ModelAnimation Animation => _animation;

        /// <summary>
        /// 当前播放时间
        /// </summary>
        public float Time {
            get => _time;
            set => _time = value;
        }

        /// <summary>
        /// 归一化时间 (0-1)
        /// </summary>
        public float NormalizedTime => _animation != null && _animation.Duration > 0
            ? _time / _animation.Duration
            : 0f;

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => _playing;

        /// <summary>
        /// 播放速度倍率
        /// </summary>
        public float Speed { get; set; } = 1.0f;

        /// <summary>
        /// 动画事件触发时调用
        /// </summary>
        public event AnimationEventHandler OnAnimationEvent;

        /// <summary>
        /// 获取动画事件列表
        /// </summary>
        public IReadOnlyList<AnimationEvent> Events => _events;

        /// <summary>
        /// 设置动画
        /// </summary>
        public void SetAnimation(Model model, ModelAnimation animation) {
            _model = model;
            _animation = animation;
            _time = 0f;
            _previousTime = 0f;
            _lastEventIndex = -1;
            BuildBoneIndexMap();
        }

        /// <summary>
        /// 添加动画事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="time">触发时间</param>
        /// <param name="parameter">可选参数</param>
        public void AddEvent(string eventName, float time, object parameter = null) {
            _events.Add(new AnimationEvent(eventName, time, parameter));
            // 按时间排序
            _events.Sort((a, b) => a.Time.CompareTo(b.Time));
        }

        /// <summary>
        /// 清除所有动画事件
        /// </summary>
        public void ClearEvents() {
            _events.Clear();
            _lastEventIndex = -1;
        }

        /// <summary>
        /// 开始播放
        /// </summary>
        public void Play(bool loop = true) {
            _playing = true;
            _looping = loop;
        }

        /// <summary>
        /// 停止播放
        /// </summary>
        public void Stop() {
            _playing = false;
        }

        /// <summary>
        /// 设置归一化时间 (0-1)
        /// </summary>
        public void SetNormalizedTime(float normalizedTime) {
            if (_animation != null && _animation.Duration > 0) {
                _time = normalizedTime * _animation.Duration;
                _previousTime = _time;
                _lastEventIndex = -1;
            }
        }

        /// <summary>
        /// 更新动画时间
        /// </summary>
        public void Update(float deltaTime) {
            if (!_playing) return;

            _previousTime = _time;
            _time += deltaTime * Speed;

            // 如果有动画，处理循环和结束逻辑
            if (_animation != null && _animation.Duration > 0) {
                if (_looping) {
                    // 循环模式下处理事件触发
                    if (_time >= _animation.Duration) {
                        // 先检测循环前的事件
                        CheckEvents(_previousTime, _animation.Duration);
                        _lastEventIndex = -1; // 重置事件索引

                        while (_time >= _animation.Duration) {
                            _time -= _animation.Duration;
                        }
                        // 检测循环后的事件（从0开始）
                        CheckEvents(0f, _time);
                    }
                    else {
                        CheckEvents(_previousTime, _time);
                    }
                }
                else if (_time >= _animation.Duration) {
                    _time = _animation.Duration;
                    _playing = false;
                    // 检测结束前的事件
                    CheckEvents(_previousTime, _time);
                }
                else {
                    CheckEvents(_previousTime, _time);
                }
            }
        }

        /// <summary>
        /// 检查并触发指定时间范围内的事件
        /// </summary>
        void CheckEvents(float fromTime, float toTime) {
            if (_events.Count == 0 || OnAnimationEvent == null) return;

            float duration = _animation?.Duration ?? 0f;
            if (duration <= 0f) return;

            for (int i = 0; i < _events.Count; i++) {
                var evt = _events[i];
                // 检查事件时间是否在当前帧的时间范围内
                if (evt.Time > fromTime && evt.Time <= toTime) {
                    // 确保每个事件只触发一次（通过索引跟踪）
                    if (i > _lastEventIndex) {
                        OnAnimationEvent?.Invoke(evt);
                        _lastEventIndex = i;
                    }
                }
            }
        }

        /// <summary>
        /// 采样当前时间的骨骼变换
        /// </summary>
        public void SampleBoneTransforms(Matrix?[] boneTransforms) {
            if (_animation == null || _model == null || boneTransforms == null) return;

            // 按骨骼分组通道，合并同一骨骼的所有属性
            Dictionary<int, (Vector3? translation, Quaternion? rotation, Vector3? scale)> boneTransformsData = new();

            foreach (var channel in _animation.Channels) {
                if (!_boneNameToIndex.TryGetValue(channel.TargetBoneName, out int boneIndex))
                    continue;

                var sampler = channel.Sampler;
                if (sampler == null || sampler.KeyTimes == null || sampler.KeyTimes.Length == 0)
                    continue;

                // 获取或创建该骨骼的变换数据
                if (!boneTransformsData.TryGetValue(boneIndex, out var data)) {
                    data = (null, null, null);
                }

                switch (channel.Property) {
                    case ModelAnimation.AnimationProperty.Translation:
                        if (sampler.Translations != null && sampler.Translations.Length > 0) {
                            data.translation = SampleVector3(sampler.Translations, sampler.KeyTimes, _time, sampler.Interpolation);
                        }
                        break;
                    case ModelAnimation.AnimationProperty.Rotation:
                        if (sampler.Rotations != null && sampler.Rotations.Length > 0) {
                            data.rotation = SampleQuaternion(sampler.Rotations, sampler.KeyTimes, _time, sampler.Interpolation);
                        }
                        break;
                    case ModelAnimation.AnimationProperty.Scale:
                        if (sampler.Scales != null && sampler.Scales.Length > 0) {
                            data.scale = SampleVector3(sampler.Scales, sampler.KeyTimes, _time, sampler.Interpolation);
                        }
                        break;
                }

                boneTransformsData[boneIndex] = data;
            }

            // 为每个骨骼构建完整的变换矩阵
            // 关键：当动画没有提供某个分量时，使用骨骼原始变换的对应分量
            foreach (var kvp in boneTransformsData) {
                int boneIndex = kvp.Key;
                var (animTranslation, animRotation, animScale) = kvp.Value;

                // 获取骨骼原始变换并分解
                ModelBone bone = _model.m_bones[boneIndex];
                Vector3 origScale, origTranslation;
                Quaternion origRotation;
                bone.Transform.Decompose(out origScale, out origRotation, out origTranslation);

                // 使用动画值或原始值
                Vector3 finalScale = animScale ?? origScale;
                Quaternion finalRotation = animRotation ?? origRotation;
                Vector3 finalTranslation = animTranslation ?? origTranslation;

                // 构建变换矩阵: Scale * Rotation * Translation
                Matrix transform = Matrix.CreateScale(finalScale) *
                                   Matrix.CreateFromQuaternion(finalRotation) *
                                   Matrix.CreateTranslation(finalTranslation);

                boneTransforms[boneIndex] = transform;
            }
        }

        Vector3 SampleVector3(Vector3[] values, float[] times, float time, ModelAnimation.InterpolationType interpolation) {
            if (values == null || values.Length == 0) return Vector3.Zero;
            if (values.Length == 1) return values[0];

            int idx = FindKeyIndex(times, time);
            if (idx < 0) return values[0];
            if (idx >= values.Length - 1) return values[values.Length - 1];

            float t0 = times[idx];
            float t1 = times[idx + 1];
            float alpha = (time - t0) / (t1 - t0);

            return interpolation switch {
                ModelAnimation.InterpolationType.Step => values[idx],
                ModelAnimation.InterpolationType.CubicSpline => CubicSplineInterpolate(values, idx, alpha), // 简化处理
                _ => Vector3.Lerp(values[idx], values[idx + 1], alpha)
            };
        }

        Quaternion SampleQuaternion(Quaternion[] values, float[] times, float time, ModelAnimation.InterpolationType interpolation) {
            if (values == null || values.Length == 0) return Quaternion.Identity;
            if (values.Length == 1) return values[0];

            int idx = FindKeyIndex(times, time);
            if (idx < 0) return values[0];
            if (idx >= values.Length - 1) return values[values.Length - 1];

            float t0 = times[idx];
            float t1 = times[idx + 1];
            float alpha = (time - t0) / (t1 - t0);

            return interpolation switch {
                ModelAnimation.InterpolationType.Step => values[idx],
                ModelAnimation.InterpolationType.CubicSpline => values[idx], // 简化处理
                _ => Quaternion.Slerp(values[idx], values[idx + 1], alpha)
            };
        }

        int FindKeyIndex(float[] times, float time) {
            if (times == null || times.Length == 0) return -1;

            for (int i = 0; i < times.Length - 1; i++) {
                if (time >= times[i] && time < times[i + 1])
                    return i;
            }

            return times.Length - 1;
        }

        Vector3 CubicSplineInterpolate(Vector3[] values, int idx, float t) {
            // 简化的三次样条插值
            return Vector3.Lerp(values[idx], values[Math.Min(idx + 1, values.Length - 1)], t);
        }

        void BuildBoneIndexMap() {
            _boneNameToIndex.Clear();
            if (_model == null) return;

            foreach (var bone in _model.m_bones) {
                _boneNameToIndex[bone.Name] = bone.Index;
            }
        }
    }
}
