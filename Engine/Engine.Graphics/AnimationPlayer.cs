#nullable disable

namespace Engine.Graphics {
    /// <summary>
    /// 动画播放器，负责采样和插值
    /// </summary>
    public class AnimationPlayer {
        Model _model;
        ModelAnimation _animation;
        float _time;
        bool _looping;
        bool _playing;
        Dictionary<string, int> _boneNameToIndex = new();

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
        /// 设置动画
        /// </summary>
        public void SetAnimation(Model model, ModelAnimation animation) {
            _model = model;
            _animation = animation;
            _time = 0f;
            BuildBoneIndexMap();
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
        /// 更新动画时间
        /// </summary>
        public void Update(float deltaTime) {
            if (!_playing) return;

            _time += deltaTime;

            // 如果有动画，处理循环和结束逻辑
            if (_animation != null && _animation.Duration > 0) {
                if (_looping) {
                    while (_time >= _animation.Duration) {
                        _time -= _animation.Duration;
                    }
                }
                else if (_time >= _animation.Duration) {
                    _time = _animation.Duration;
                    _playing = false;
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
            foreach (var kvp in boneTransformsData) {
                int boneIndex = kvp.Key;
                var (translation, rotation, scale) = kvp.Value;

                // 构建变换矩阵: Scale * Rotation * Translation
                Matrix transform = Matrix.Identity;
                if (scale.HasValue) {
                    transform *= Matrix.CreateScale(scale.Value);
                }
                if (rotation.HasValue) {
                    transform *= Matrix.CreateFromQuaternion(rotation.Value);
                }
                if (translation.HasValue) {
                    transform *= Matrix.CreateTranslation(translation.Value);
                }

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
