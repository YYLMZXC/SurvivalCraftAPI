#nullable disable

namespace Engine.Graphics
{
    /// <summary>
    /// 混合空间采样点 - 一维
    /// </summary>
    public class BlendSample
    {
        /// <summary>
        /// 参数值
        /// </summary>
        public float Value { get; set; }

        /// <summary>
        /// 对应的动画名称
        /// </summary>
        public string AnimationName { get; set; }

        /// <summary>
        /// 动画配置（可选）
        /// </summary>
        public AnimationSourceConfig AnimationConfig { get; set; }
    }

    /// <summary>
    /// 混合空间采样点 - 二维
    /// </summary>
    public class BlendSample2D
    {
        /// <summary>
        /// X 轴参数值
        /// </summary>
        public float ValueX { get; set; }

        /// <summary>
        /// Y 轴参数值
        /// </summary>
        public float ValueY { get; set; }

        /// <summary>
        /// 对应的动画名称
        /// </summary>
        public string AnimationName { get; set; }

        /// <summary>
        /// 动画配置（可选）
        /// </summary>
        public AnimationSourceConfig AnimationConfig { get; set; }
    }

    /// <summary>
    /// 一维混合空间定义
    /// </summary>
    public class BlendSpaceDefinition
    {
        /// <summary>
        /// 混合空间名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 参数名称（从 AnimationParameters 读取）
        /// </summary>
        public string ParameterName { get; set; }

        /// <summary>
        /// 采样点列表（按 Value 升序排列）
        /// </summary>
        public BlendSample[] Samples { get; set; }

        /// <summary>
        /// 是否同步动画时间（保持所有动画在相同的归一化时间）
        /// </summary>
        public bool SyncTime { get; set; } = true;
    }

    /// <summary>
    /// 二维混合空间定义
    /// </summary>
    public class BlendSpaceDefinition2D
    {
        /// <summary>
        /// 混合空间名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// X 轴参数名称
        /// </summary>
        public string ParameterNameX { get; set; }

        /// <summary>
        /// Y 轴参数名称
        /// </summary>
        public string ParameterNameY { get; set; }

        /// <summary>
        /// 采样点列表
        /// </summary>
        public BlendSample2D[] Samples { get; set; }

        /// <summary>
        /// 是否同步动画时间
        /// </summary>
        public bool SyncTime { get; set; } = true;
    }

    /// <summary>
    /// 混合空间动画来源 - 根据参数在多个动画间插值
    /// 支持一维和二维混合空间，类似于 Unity 的 BlendTree 或 Unreal 的 Blend Space
    /// </summary>
    public class BlendSpaceSource : IAnimationSource
    {
        public string Name { get; }

        private readonly BlendSpaceDefinition _definition1D;
        private readonly BlendSpaceDefinition2D _definition2D;
        private readonly ClipAnimationSource[] _sources;
        private readonly Model _model;
        private readonly bool _is2D;

        // 预分配缓冲区，避免每帧 GC
        private Matrix?[] _tempTransformsBuffer1;
        private Matrix?[] _tempTransformsBuffer2;
        private int _bufferSize;

        // 时间同步相关
        private float _syncedNormalizedTime;
        private readonly bool _syncTime;

        // 缓存的参数值（用于 SampleTransforms）
        private float _cachedParamValue;
        private float _cachedParamX;
        private float _cachedParamY;

        // 预分配的二维混合缓冲区（避免每帧 GC）
        private float[] _blend2DDistances;
        private int[] _blend2DSortedIndices;
        private float[] _blend2DWeights;
        private int _blend2DMaxSamples;

        // 有效源索引（过滤掉 null）
        private int[] _validSourceIndices;

        /// <summary>
        /// 创建一维混合空间
        /// </summary>
        public BlendSpaceSource(BlendSpaceDefinition definition, Model model)
        {
            _definition1D = definition ?? throw new ArgumentNullException(nameof(definition));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _is2D = false;
            _syncTime = definition.SyncTime;

            Name = definition.Name ?? "BlendSpace1D";

            if (definition.Samples == null || definition.Samples.Length == 0)
            {
                _sources = Array.Empty<ClipAnimationSource>();
                _validSourceIndices = Array.Empty<int>();
                return;
            }

            // 按值排序采样点，并更新定义
            definition.Samples = definition.Samples.OrderBy(s => s.Value).ToArray();

            _sources = new ClipAnimationSource[definition.Samples.Length];
            var validIndices = new List<int>();
            for (int i = 0; i < definition.Samples.Length; i++)
            {
                var sample = definition.Samples[i];
                var anim = model.Animations?.FirstOrDefault(a => a.Name == sample.AnimationName);
                if (anim != null)
                {
                    var config = sample.AnimationConfig ?? new AnimationSourceConfig { Loop = true };
                    config.Loop = true; // 混合空间中的动画必须循环
                    _sources[i] = new ClipAnimationSource(model, anim, config);
                    validIndices.Add(i);
                }
            }

            // 记录有效源索引（过滤掉 null）
            _validSourceIndices = validIndices.ToArray();

            // 预分配缓冲区
            if (model.Bones.Count > 0)
            {
                _bufferSize = model.Bones.Count;
                _tempTransformsBuffer1 = new Matrix?[_bufferSize];
                _tempTransformsBuffer2 = new Matrix?[_bufferSize];
            }

            // 预分配二维混合缓冲区
            AllocateBlend2DBuffers(definition.Samples.Length);
        }

        /// <summary>
        /// 创建二维混合空间
        /// </summary>
        public BlendSpaceSource(BlendSpaceDefinition2D definition, Model model)
        {
            _definition2D = definition ?? throw new ArgumentNullException(nameof(definition));
            _model = model ?? throw new ArgumentNullException(nameof(model));
            _is2D = true;
            _syncTime = definition.SyncTime;

            Name = definition.Name ?? "BlendSpace2D";

            if (definition.Samples == null || definition.Samples.Length == 0)
            {
                _sources = Array.Empty<ClipAnimationSource>();
                _validSourceIndices = Array.Empty<int>();
                return;
            }

            _sources = new ClipAnimationSource[definition.Samples.Length];
            var validIndices = new List<int>();
            for (int i = 0; i < definition.Samples.Length; i++)
            {
                var sample = definition.Samples[i];
                var anim = model.Animations?.FirstOrDefault(a => a.Name == sample.AnimationName);
                if (anim != null)
                {
                    var config = sample.AnimationConfig ?? new AnimationSourceConfig { Loop = true };
                    config.Loop = true;
                    _sources[i] = new ClipAnimationSource(model, anim, config);
                    validIndices.Add(i);
                }
            }

            // 记录有效源索引（过滤掉 null）
            _validSourceIndices = validIndices.ToArray();

            // 预分配缓冲区
            if (model.Bones.Count > 0)
            {
                _bufferSize = model.Bones.Count;
                _tempTransformsBuffer1 = new Matrix?[_bufferSize];
                _tempTransformsBuffer2 = new Matrix?[_bufferSize];
            }

            // 预分配二维混合缓冲区
            AllocateBlend2DBuffers(definition.Samples.Length);
        }

        public void Update(float deltaTime, AnimationParameters parameters)
        {
            if (_sources == null || _sources.Length == 0) return;

            // 缓存参数值，供 SampleTransforms 使用
            if (parameters != null)
            {
                if (_is2D)
                {
                    if (!string.IsNullOrEmpty(_definition2D.ParameterNameX))
                        _cachedParamX = parameters.GetFloat(_definition2D.ParameterNameX);
                    if (!string.IsNullOrEmpty(_definition2D.ParameterNameY))
                        _cachedParamY = parameters.GetFloat(_definition2D.ParameterNameY);
                }
                else
                {
                    if (!string.IsNullOrEmpty(_definition1D.ParameterName))
                        _cachedParamValue = parameters.GetFloat(_definition1D.ParameterName);
                }
            }

            // 时间同步模式：所有动画使用相同的归一化时间
            if (_syncTime)
            {
                // 更新同步时间
                _syncedNormalizedTime += deltaTime * GetFirstValidAnimationSpeed();
                if (_syncedNormalizedTime >= 1f) _syncedNormalizedTime -= 1f;
                else if (_syncedNormalizedTime < 0f) _syncedNormalizedTime += 1f;

                // 同步所有有效动画
                foreach (var idx in _validSourceIndices)
                {
                    _sources[idx].Player.SetNormalizedTime(_syncedNormalizedTime);
                }
            }
            else
            {
                // 独立更新每个动画
                foreach (var idx in _validSourceIndices)
                {
                    _sources[idx].Update(deltaTime, parameters);
                }
            }
        }

        private float GetFirstValidAnimationSpeed()
        {
            if (_validSourceIndices.Length > 0 && _sources[_validSourceIndices[0]] != null)
            {
                return _sources[_validSourceIndices[0]].Player.Speed;
            }
            return 1f;
        }

        public void SampleTransforms(Matrix?[] boneTransforms, Model model)
        {
            if (_sources == null || _sources.Length == 0 || boneTransforms == null) return;

            // 确保缓冲区大小足够
            EnsureBufferSize(boneTransforms.Length);

            if (_is2D)
            {
                SampleTransforms2D(boneTransforms, model);
            }
            else
            {
                SampleTransforms1D(boneTransforms, model);
            }
        }

        private void SampleTransforms1D(Matrix?[] boneTransforms, Model model)
        {
            // 使用缓存的参数值
            float paramValue = _cachedParamValue;

            // 找到相邻的两个采样点
            var (lowerIdx, upperIdx, t) = FindBlendSamples1D(paramValue);

            if (lowerIdx < 0 || _sources[lowerIdx] == null)
            {
                // 没有有效的动画
                return;
            }

            if (lowerIdx == upperIdx || t <= 0.001f)
            {
                // 只使用一个动画
                _sources[lowerIdx].SampleTransforms(boneTransforms, model);
            }
            else if (t >= 0.999f)
            {
                // 只使用另一个动画
                if (_sources[upperIdx] != null)
                    _sources[upperIdx].SampleTransforms(boneTransforms, model);
                else
                    _sources[lowerIdx].SampleTransforms(boneTransforms, model);
            }
            else
            {
                // 混合两个动画
                Array.Clear(_tempTransformsBuffer1, 0, _bufferSize);
                Array.Clear(_tempTransformsBuffer2, 0, _bufferSize);

                _sources[lowerIdx].SampleTransforms(_tempTransformsBuffer1, model);
                _sources[upperIdx].SampleTransforms(_tempTransformsBuffer2, model);

                BlendTransforms(boneTransforms, _tempTransformsBuffer1, _tempTransformsBuffer2, t);
            }
        }

        private void SampleTransforms2D(Matrix?[] boneTransforms, Model model)
        {
            // 使用缓存的参数值
            var (indices, weights) = FindBlendSamples2D(_cachedParamX, _cachedParamY);

            if (indices == null || weights == null || indices.Length == 0)
                return;

            // 只有一个有效采样点
            if (indices.Length == 1 && _sources[indices[0]] != null)
            {
                _sources[indices[0]].SampleTransforms(boneTransforms, model);
                return;
            }

            // 多个采样点加权混合
            // 先混合前两个
            if (indices.Length >= 2 && _sources[indices[0]] != null && _sources[indices[1]] != null)
            {
                Array.Clear(_tempTransformsBuffer1, 0, _bufferSize);
                Array.Clear(_tempTransformsBuffer2, 0, _bufferSize);

                _sources[indices[0]].SampleTransforms(_tempTransformsBuffer1, model);
                _sources[indices[1]].SampleTransforms(_tempTransformsBuffer2, model);

                // 归一化权重
                float totalWeight = weights[0] + weights[1];
                float t = totalWeight > 0 ? weights[1] / totalWeight : 0f;

                BlendTransforms(boneTransforms, _tempTransformsBuffer1, _tempTransformsBuffer2, t);

                // 继续混合剩余的采样点
                for (int i = 2; i < indices.Length; i++)
                {
                    if (_sources[indices[i]] == null) continue;

                    Array.Clear(_tempTransformsBuffer1, 0, _bufferSize);
                    _sources[indices[i]].SampleTransforms(_tempTransformsBuffer1, model);

                    // 计算新的混合权重（使用 for 循环替代 LINQ）
                    float prevTotal = 0f;
                    for (int j = 0; j < i; j++)
                    {
                        prevTotal += weights[j];
                    }
                    totalWeight = prevTotal + weights[i];
                    t = totalWeight > 0 ? weights[i] / totalWeight : 0f;

                    // 复制当前结果到 buffer2
                    Array.Copy(boneTransforms, _tempTransformsBuffer2, boneTransforms.Length);

                    BlendTransforms(boneTransforms, _tempTransformsBuffer2, _tempTransformsBuffer1, t);
                }
            }
        }

        /// <summary>
        /// 使用缓存的参数值进行一维混合采样
        /// </summary>
        public void SampleTransformsWithParam(Matrix?[] boneTransforms, Model model, float paramValue)
        {
            if (_sources == null || _sources.Length == 0 || boneTransforms == null) return;

            EnsureBufferSize(boneTransforms.Length);

            if (_is2D)
            {
                SampleTransforms2D(boneTransforms, model);
            }
            else
            {
                SampleTransforms1DWithParam(boneTransforms, model, paramValue);
            }
        }

        private void SampleTransforms1DWithParam(Matrix?[] boneTransforms, Model model, float paramValue)
        {
            var (lowerIdx, upperIdx, t) = FindBlendSamples1D(paramValue);

            if (lowerIdx < 0 || _sources[lowerIdx] == null)
                return;

            if (lowerIdx == upperIdx || t <= 0.001f)
            {
                _sources[lowerIdx].SampleTransforms(boneTransforms, model);
            }
            else if (t >= 0.999f)
            {
                if (_sources[upperIdx] != null)
                    _sources[upperIdx].SampleTransforms(boneTransforms, model);
                else
                    _sources[lowerIdx].SampleTransforms(boneTransforms, model);
            }
            else
            {
                Array.Clear(_tempTransformsBuffer1, 0, _bufferSize);
                Array.Clear(_tempTransformsBuffer2, 0, _bufferSize);

                _sources[lowerIdx].SampleTransforms(_tempTransformsBuffer1, model);
                _sources[upperIdx].SampleTransforms(_tempTransformsBuffer2, model);

                BlendTransforms(boneTransforms, _tempTransformsBuffer1, _tempTransformsBuffer2, t);
            }
        }

        /// <summary>
        /// 使用缓存的参数值进行二维混合采样
        /// </summary>
        public void SampleTransformsWithParam2D(Matrix?[] boneTransforms, Model model, float paramX, float paramY)
        {
            if (_sources == null || _sources.Length == 0 || boneTransforms == null) return;

            EnsureBufferSize(boneTransforms.Length);

            var (indices, weights) = FindBlendSamples2D(paramX, paramY);

            if (indices == null || weights == null || indices.Length == 0)
                return;

            if (indices.Length == 1 && _sources[indices[0]] != null)
            {
                _sources[indices[0]].SampleTransforms(boneTransforms, model);
                return;
            }

            // 累积混合
            if (indices.Length >= 2 && _sources[indices[0]] != null && _sources[indices[1]] != null)
            {
                Array.Clear(_tempTransformsBuffer1, 0, _bufferSize);
                Array.Clear(_tempTransformsBuffer2, 0, _bufferSize);

                _sources[indices[0]].SampleTransforms(_tempTransformsBuffer1, model);
                _sources[indices[1]].SampleTransforms(_tempTransformsBuffer2, model);

                float totalWeight = weights[0] + weights[1];
                float t = totalWeight > 0 ? weights[1] / totalWeight : 0f;

                BlendTransforms(boneTransforms, _tempTransformsBuffer1, _tempTransformsBuffer2, t);

                for (int i = 2; i < indices.Length; i++)
                {
                    if (_sources[indices[i]] == null) continue;

                    Array.Clear(_tempTransformsBuffer1, 0, _bufferSize);
                    _sources[indices[i]].SampleTransforms(_tempTransformsBuffer1, model);

                    // 计算新的混合权重（使用 for 循环替代 LINQ）
                    float prevTotal = 0f;
                    for (int j = 0; j < i; j++)
                    {
                        prevTotal += weights[j];
                    }
                    totalWeight = prevTotal + weights[i];
                    t = totalWeight > 0 ? weights[i] / totalWeight : 0f;

                    Array.Copy(boneTransforms, _tempTransformsBuffer2, boneTransforms.Length);
                    BlendTransforms(boneTransforms, _tempTransformsBuffer2, _tempTransformsBuffer1, t);
                }
            }
        }

        /// <summary>
        /// 查找一维混合采样点
        /// </summary>
        private (int lowerIdx, int upperIdx, float t) FindBlendSamples1D(float value)
        {
            if (_definition1D?.Samples == null || _sources == null)
                return (-1, -1, 0f);

            var samples = _definition1D.Samples;

            // 边界情况
            if (value <= samples[0].Value)
                return (0, 0, 0f);

            if (value >= samples[samples.Length - 1].Value)
                return (samples.Length - 1, samples.Length - 1, 0f);

            // 查找相邻采样点
            for (int i = 0; i < samples.Length - 1; i++)
            {
                if (value >= samples[i].Value && value <= samples[i + 1].Value)
                {
                    float range = samples[i + 1].Value - samples[i].Value;
                    float t = range > 0.0001f ? (value - samples[i].Value) / range : 0f;
                    return (i, i + 1, t);
                }
            }

            return (0, 0, 0f);
        }

        /// <summary>
        /// 查找二维混合采样点 - 返回最近的采样点及其权重
        /// </summary>
        private (int[] indices, float[] weights) FindBlendSamples2D(float paramX, float paramY)
        {
            if (_definition2D?.Samples == null || _sources == null)
                return (null, null);

            var samples = _definition2D.Samples;
            int sampleCount = samples.Length;

            // 确保缓冲区足够大
            EnsureBlend2DBuffers(sampleCount);

            // 计算到每个采样点的距离（使用预分配数组）
            for (int i = 0; i < sampleCount; i++)
            {
                float dx = paramX - samples[i].ValueX;
                float dy = paramY - samples[i].ValueY;
                _blend2DDistances[i] = dx * dx + dy * dy; // 使用距离平方
            }

            // 初始化排序索引
            for (int i = 0; i < sampleCount; i++)
            {
                _blend2DSortedIndices[i] = i;
            }

            // 部分排序：只找出最近的 4 个（使用简单的选择排序）
            int maxCount = Math.Min(4, sampleCount);
            for (int i = 0; i < maxCount; i++)
            {
                int minIdx = i;
                for (int j = i + 1; j < sampleCount; j++)
                {
                    if (_blend2DDistances[_blend2DSortedIndices[j]] < _blend2DDistances[_blend2DSortedIndices[minIdx]])
                    {
                        minIdx = j;
                    }
                }
                if (minIdx != i)
                {
                    int temp = _blend2DSortedIndices[i];
                    _blend2DSortedIndices[i] = _blend2DSortedIndices[minIdx];
                    _blend2DSortedIndices[minIdx] = temp;
                }
            }

            // 计算权重（距离的反比）
            float totalWeight = 0f;
            for (int i = 0; i < maxCount; i++)
            {
                float dist = _blend2DDistances[_blend2DSortedIndices[i]];
                if (dist < 0.0001f)
                {
                    // 非常接近某个采样点，只使用该点
                    _blend2DWeights[i] = 1f;
                    for (int j = 0; j < i; j++)
                    {
                        _blend2DWeights[j] = 0f;
                    }
                    totalWeight = 1f;
                    // 返回子数组
                    return (CreateResultArray(_blend2DSortedIndices, i + 1),
                            CreateResultArray(_blend2DWeights, i + 1));
                }
                _blend2DWeights[i] = 1f / dist;
                totalWeight += _blend2DWeights[i];
            }

            // 归一化权重
            if (totalWeight > 0)
            {
                for (int i = 0; i < maxCount; i++)
                {
                    _blend2DWeights[i] /= totalWeight;
                }
            }

            // 返回子数组
            return (CreateResultArray(_blend2DSortedIndices, maxCount),
                    CreateResultArray(_blend2DWeights, maxCount));
        }

        private void EnsureBlend2DBuffers(int requiredSize)
        {
            if (_blend2DMaxSamples < requiredSize)
            {
                _blend2DMaxSamples = requiredSize;
                _blend2DDistances = new float[requiredSize];
                _blend2DSortedIndices = new int[requiredSize];
                _blend2DWeights = new float[requiredSize];
            }
        }

        private void AllocateBlend2DBuffers(int initialSize)
        {
            _blend2DMaxSamples = initialSize;
            _blend2DDistances = new float[initialSize];
            _blend2DSortedIndices = new int[initialSize];
            _blend2DWeights = new float[initialSize];
        }

        private static T[] CreateResultArray<T>(T[] source, int count)
        {
            T[] result = new T[count];
            Array.Copy(source, result, count);
            return result;
        }

        /// <summary>
        /// 混合两组骨骼变换
        /// </summary>
        private void BlendTransforms(Matrix?[] output, Matrix?[] a, Matrix?[] b, float t)
        {
            int count = Math.Min(output.Length, Math.Min(a.Length, b.Length));

            for (int i = 0; i < count; i++)
            {
                if (a[i].HasValue && b[i].HasValue)
                {
                    output[i] = BlendMatrix(a[i].Value, b[i].Value, t);
                }
                else if (a[i].HasValue)
                {
                    output[i] = a[i].Value;
                }
                else if (b[i].HasValue)
                {
                    output[i] = b[i].Value;
                }
                else
                {
                    output[i] = null;
                }
            }
        }

        /// <summary>
        /// 混合两个矩阵（分解为 T、R、S 分别插值）
        /// </summary>
        private Matrix BlendMatrix(Matrix a, Matrix b, float t)
        {
            DecomposeMatrix(a, out var tA, out var rA, out var sA);
            DecomposeMatrix(b, out var tB, out var rB, out var sB);

            return Matrix.CreateScale(Vector3.Lerp(sA, sB, t))
                 * Matrix.CreateFromQuaternion(Quaternion.Slerp(rA, rB, t))
                 * Matrix.CreateTranslation(Vector3.Lerp(tA, tB, t));
        }

        private void DecomposeMatrix(Matrix m, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
        {
            translation = m.Translation;

            Vector3 right = new Vector3(m.M11, m.M12, m.M13);
            Vector3 up = new Vector3(m.M21, m.M22, m.M23);
            Vector3 forward = new Vector3(m.M31, m.M32, m.M33);

            float scaleX = right.Length();
            float scaleY = up.Length();
            float scaleZ = forward.Length();
            scale = new Vector3(scaleX, scaleY, scaleZ);

            if (scaleX != 0) right /= scaleX;
            if (scaleY != 0) up /= scaleY;
            if (scaleZ != 0) forward /= scaleZ;

            Matrix rotationMatrix = new Matrix(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1);

            rotation = Quaternion.CreateFromRotationMatrix(rotationMatrix);

            if (scaleX * scaleY * scaleZ < 0)
            {
                scale = -scale;
            }
        }

        private void EnsureBufferSize(int requiredSize)
        {
            if (_bufferSize < requiredSize)
            {
                _bufferSize = requiredSize;
                _tempTransformsBuffer1 = new Matrix?[_bufferSize];
                _tempTransformsBuffer2 = new Matrix?[_bufferSize];
            }
        }

        /// <summary>
        /// 获取当前混合参数值（用于调试）
        /// </summary>
        public float GetCurrentParameterValue(AnimationParameters parameters)
        {
            if (!_is2D && _definition1D != null && !string.IsNullOrEmpty(_definition1D.ParameterName))
            {
                return parameters?.GetFloat(_definition1D.ParameterName) ?? 0f;
            }
            return 0f;
        }

        /// <summary>
        /// 获取当前二维混合参数值（用于调试）
        /// </summary>
        public (float x, float y) GetCurrentParameterValues2D(AnimationParameters parameters)
        {
            if (_is2D && _definition2D != null)
            {
                float x = !string.IsNullOrEmpty(_definition2D.ParameterNameX)
                    ? parameters?.GetFloat(_definition2D.ParameterNameX) ?? 0f : 0f;
                float y = !string.IsNullOrEmpty(_definition2D.ParameterNameY)
                    ? parameters?.GetFloat(_definition2D.ParameterNameY) ?? 0f : 0f;
                return (x, y);
            }
            return (0f, 0f);
        }

        /// <summary>
        /// 获取同步的归一化时间
        /// </summary>
        public float GetSyncedNormalizedTime() => _syncedNormalizedTime;

        /// <summary>
        /// 设置所有动画的归一化时间
        /// </summary>
        public void SetNormalizedTime(float normalizedTime)
        {
            _syncedNormalizedTime = normalizedTime;
            foreach (var source in _sources)
            {
                source?.Player?.SetNormalizedTime(normalizedTime);
            }
        }
    }
}
