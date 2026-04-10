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
        /// 事件触发时间（归一化时间 0-1）
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
    /// 支持相位范围播放（StartPhase, EndPhase）和循环边界插值
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

        // Phase range support
        float _startPhase = 0f;
        float _endPhase = 1f;
        bool _preservePose = false;
        float _wrapOvershoot = 0f; // For loop boundary interpolation

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
        /// 起始相位 (0-1)，默认为 0
        /// </summary>
        public float StartPhase {
            get => _startPhase;
            set => _startPhase = Math.Clamp(value, 0f, 1f);
        }

        /// <summary>
        /// 结束相位 (0-1)，默认为 1
        /// </summary>
        public float EndPhase {
            get => _endPhase;
            set => _endPhase = Math.Clamp(value, 0f, 1f);
        }

        /// <summary>
        /// 是否在非播放状态下保持最后姿态
        /// </summary>
        public bool PreservePose {
            get => _preservePose;
            set => _preservePose = value;
        }

        /// <summary>
        /// 是否有有效的动画数据
        /// </summary>
        public bool HasValidAnimation => _animation != null && _animation.Duration > 0;

        /// <summary>
        /// 相位范围大小
        /// </summary>
        public float PhaseRange => Math.Abs(_endPhase - _startPhase);

        /// <summary>
        /// 相位方向：1 表示正向（EndPhase > StartPhase），-1 表示反向
        /// </summary>
        public int PhaseDirection => _endPhase > _startPhase ? 1 : -1;

        /// <summary>
        /// 实际播放方向：PhaseDirection × sign(Speed)
        /// </summary>
        public int ActualDirection => PhaseDirection * Math.Sign(Speed);

        /// <summary>
        /// 归一化时间 (0-1)，相对于相位范围
        /// </summary>
        public float NormalizedTime {
            get {
                if (!HasValidAnimation || PhaseRange <= 0f) return 0f;

                float effectiveTime = GetEffectiveTime();  // Time in seconds
                float normalizedPhase = effectiveTime / _animation.Duration;  // Convert to 0-1
                float normalizedProgress = (normalizedPhase - _startPhase) / PhaseRange;
                return Math.Clamp(normalizedProgress, 0f, 1f);
            }
        }

        /// <summary>
        /// 是否正在播放
        /// </summary>
        public bool IsPlaying => _playing;

        /// <summary>
        /// 是否循环播放
        /// </summary>
        public bool Loop
        {
            get => _looping;
            set => _looping = value;
        }

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
        /// 获取有效时间（基于相位）
        /// </summary>
        public float GetEffectiveTime() {
            if (!HasValidAnimation) return 0f;

            float duration = _animation.Duration;
            float startTime = _startPhase * duration;
            float endTime = _endPhase * duration;

            // 确保时间在相位范围内
            if (PhaseDirection > 0) {
                // 正向：StartPhase -> EndPhase
                return Math.Clamp(_time, startTime, endTime);
            } else {
                // 反向：EndPhase -> StartPhase
                return Math.Clamp(_time, endTime, startTime);
            }
        }

        /// <summary>
        /// 设置动画
        /// </summary>
        public void SetAnimation(Model model, ModelAnimation animation) {
            _model = model;
            _animation = animation;
            _time = 0f;
            _previousTime = 0f;
            _lastEventIndex = -1;
            _wrapOvershoot = 0f;
            BuildBoneIndexMap();
        }

        /// <summary>
        /// 设置相位范围
        /// </summary>
        /// <param name="startPhase">起始相位 (0-1)</param>
        /// <param name="endPhase">结束相位 (0-1)</param>
        public void SetPhaseRange(float startPhase, float endPhase) {
            _startPhase = Math.Clamp(startPhase, 0f, 1f);
            _endPhase = Math.Clamp(endPhase, 0f, 1f);
            _wrapOvershoot = 0f;
        }

        /// <summary>
        /// 添加动画事件
        /// </summary>
        /// <param name="eventName">事件名称</param>
        /// <param name="normalizedTime">触发时间（归一化时间 0-1）</param>
        /// <param name="parameter">可选参数</param>
        public void AddEvent(string eventName, float normalizedTime, object parameter = null) {
            _events.Add(new AnimationEvent(eventName, normalizedTime, parameter));
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
                _wrapOvershoot = 0f;
            }
        }

        /// <summary>
        /// 更新动画时间
        /// </summary>
        public void Update(float deltaTime) {
            if (!_playing) return;

            _previousTime = _time;

            // 如果有动画，处理相位范围逻辑
            if (!HasValidAnimation) return;

            float duration = _animation.Duration;
            float startTime = _startPhase * duration;
            float endTime = _endPhase * duration;

            // 速度为 0 时停在 StartPhase
            if (Speed == 0f) {
                _time = startTime;
                return;
            }

            // 更新时间
            _time += deltaTime * Speed;

            // 计算边界
            float minTime = Math.Min(startTime, endTime);
            float maxTime = Math.Max(startTime, endTime);

            if (_looping) {
                // 循环模式
                if (ActualDirection > 0) {
                    // 正向播放
                    if (_time > maxTime) {
                        // 计算超出的部分用于边界插值
                        _wrapOvershoot = _time - maxTime;
                        _time = minTime + _wrapOvershoot;
                        _wrapOvershoot = Math.Min(_wrapOvershoot, deltaTime * Math.Abs(Speed));
                        _lastEventIndex = -1;
                    } else {
                        _wrapOvershoot = 0f;
                    }
                } else {
                    // 反向播放
                    if (_time < minTime) {
                        _wrapOvershoot = minTime - _time;
                        _time = maxTime - _wrapOvershoot;
                        _wrapOvershoot = Math.Min(_wrapOvershoot, deltaTime * Math.Abs(Speed));
                        _lastEventIndex = -1;
                    } else {
                        _wrapOvershoot = 0f;
                    }
                }
            } else {
                // 非循环模式：停在边界
                if (ActualDirection > 0) {
                    if (_time >= maxTime) {
                        _time = maxTime;
                        _playing = false;
                    }
                } else {
                    if (_time <= minTime) {
                        _time = minTime;
                        _playing = false;
                    }
                }
                _wrapOvershoot = 0f;
            }

            // 检查事件
            CheckEvents(_previousTime, _time);
        }

        /// <summary>
        /// 检查并触发指定时间范围内的事件
        /// </summary>
        void CheckEvents(float fromTime, float toTime) {
            if (_events.Count == 0 || OnAnimationEvent == null) return;

            float duration = _animation?.Duration ?? 0f;
            if (duration <= 0f) return;

            // 将绝对时间转换为归一化时间
            float fromNormalized = fromTime / duration;
            float toNormalized = toTime / duration;

            for (int i = 0; i < _events.Count; i++) {
                var evt = _events[i];
                // 事件时间使用归一化时间 (0-1)
                // 检查事件时间是否在当前帧的时间范围内
                if (evt.Time > fromNormalized && evt.Time <= toNormalized) {
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

            // 检查是否需要边界插值
            if (_looping && _wrapOvershoot > 0f && PhaseRange > 0f) {
                SampleWithBoundaryInterpolation(boneTransforms);
                return;
            }

            SampleAtTimeInternal(_time, boneTransforms);
        }

        /// <summary>
        /// 带边界插值的采样（用于循环边界平滑过渡）
        /// </summary>
        void SampleWithBoundaryInterpolation(Matrix?[] boneTransforms) {
            float duration = _animation.Duration;
            float startTime = _startPhase * duration;
            float endTime = _endPhase * duration;
            float rangeDuration = PhaseRange * duration;

            if (rangeDuration <= 0f) return;

            // 计算插值权重
            float blendWeight = _wrapOvershoot / rangeDuration;
            blendWeight = Math.Clamp(blendWeight, 0f, 1f);

            // 采样当前时间的变换
            Matrix?[] currentTransforms = new Matrix?[boneTransforms.Length];
            SampleAtTimeInternal(_time, currentTransforms);

            // 采样边界另一端的变换
            float boundaryTime = ActualDirection > 0 ? startTime : endTime;
            Matrix?[] boundaryTransforms = new Matrix?[boneTransforms.Length];
            SampleAtTimeInternal(boundaryTime, boundaryTransforms);

            // 混合变换
            for (int i = 0; i < boneTransforms.Length; i++) {
                if (currentTransforms[i].HasValue && boundaryTransforms[i].HasValue) {
                    boneTransforms[i] = BlendMatrix(currentTransforms[i].Value, boundaryTransforms[i].Value, blendWeight);
                } else if (currentTransforms[i].HasValue) {
                    boneTransforms[i] = currentTransforms[i];
                } else if (boundaryTransforms[i].HasValue) {
                    boneTransforms[i] = boundaryTransforms[i];
                }
            }
        }

        /// <summary>
        /// 在指定时间采样骨骼变换
        /// </summary>
        public void SampleAtTime(float time, Matrix?[] boneTransforms) {
            SampleAtTimeInternal(time, boneTransforms);
        }

        /// <summary>
        /// 内部采样方法
        /// </summary>
        void SampleAtTimeInternal(float time, Matrix?[] boneTransforms) {
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
                            data.translation = SampleVector3(sampler.Translations, sampler.KeyTimes, time, sampler.Interpolation);
                        }
                        break;
                    case ModelAnimation.AnimationProperty.Rotation:
                        if (sampler.Rotations != null && sampler.Rotations.Length > 0) {
                            data.rotation = SampleQuaternion(sampler.Rotations, sampler.KeyTimes, time, sampler.Interpolation);
                        }
                        break;
                    case ModelAnimation.AnimationProperty.Scale:
                        if (sampler.Scales != null && sampler.Scales.Length > 0) {
                            data.scale = SampleVector3(sampler.Scales, sampler.KeyTimes, time, sampler.Interpolation);
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

        /// <summary>
        /// 在指定相位采样骨骼变换（用于 PreservePose）
        /// </summary>
        public void SampleBoneTransformsAtPhase(float phase, Matrix?[] boneTransforms) {
            if (_animation == null || boneTransforms == null) return;

            float time = Math.Clamp(phase, 0f, 1f) * _animation.Duration;
            SampleAtTimeInternal(time, boneTransforms);
        }

        /// <summary>
        /// 混合两个矩阵
        /// </summary>
        Matrix BlendMatrix(Matrix a, Matrix b, float t) {
            // 分解矩阵
            Vector3 scaleA, translationA;
            Quaternion rotationA;
            a.Decompose(out scaleA, out rotationA, out translationA);

            Vector3 scaleB, translationB;
            Quaternion rotationB;
            b.Decompose(out scaleB, out rotationB, out translationB);

            // 插值
            Vector3 blendedScale = Vector3.Lerp(scaleA, scaleB, t);
            Quaternion blendedRotation = Quaternion.Slerp(rotationA, rotationB, t);
            Vector3 blendedTranslation = Vector3.Lerp(translationA, translationB, t);

            // 重建矩阵
            return Matrix.CreateScale(blendedScale) *
                   Matrix.CreateFromQuaternion(blendedRotation) *
                   Matrix.CreateTranslation(blendedTranslation);
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
