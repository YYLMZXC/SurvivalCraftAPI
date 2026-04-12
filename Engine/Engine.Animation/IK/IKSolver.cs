#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// IK 求解器主类，管理链、目标、求解调度
    /// </summary>
    public class IKSolver
    {
        // IK 链注册表（按名称索引）
        private readonly Dictionary<string, IKChain> _chains = new();

        // 运行时目标
        private readonly Dictionary<string, IKTarget> _targets = new();

        // 算法工厂
        private readonly Dictionary<string, IIKAlgorithm> _algorithms = new();

        // 骨骼世界位置缓存
        private Vector3[] _worldPositions;

        // 上一次有效的变换结果（用于 UseLastValidResult 策略）
        private readonly Dictionary<string, Matrix?[]> _lastValidTransforms = new();

        // 默认算法配置
        private readonly IKAlgorithmConfig _defaultConfig = new();

        /// <summary>
        /// 是否启用调试日志
        /// </summary>
        public bool EnableDebugLogging { get; set; } = false;

        public IKSolver()
        {
            // 注册默认算法
            RegisterAlgorithm(new TwoBoneIK());
            RegisterAlgorithm(new CCD());
            RegisterAlgorithm(new FABRIK());
        }

        /// <summary>
        /// 注册 IK 算法
        /// </summary>
        public void RegisterAlgorithm(IIKAlgorithm algorithm)
        {
            if (algorithm != null)
            {
                _algorithms[algorithm.Name] = algorithm;
            }
        }

        /// <summary>
        /// 注册 IK 链（首次调用时构建，后续复用）
        /// </summary>
        /// <param name="name">链名称</param>
        /// <param name="endBoneName">末端骨骼名称</param>
        /// <param name="algorithm">算法实例（可选）</param>
        /// <param name="maxChainLength">最大链长度（从末端向上遍历）</param>
        public void RegisterChain(string name, string endBoneName,
            IIKAlgorithm algorithm, int maxChainLength = 3)
        {
            if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(endBoneName))
                return;

            // 链信息先存储，在 Solve 时根据 Model 构建实际骨骼链
            // 这里只存储配置信息，延迟到 Solve 时构建
            var pendingChain = new PendingChainInfo
            {
                Name = name,
                EndBoneName = endBoneName,
                Algorithm = algorithm,
                MaxChainLength = maxChainLength
            };

            _pendingChains[name] = pendingChain;
        }

        /// <summary>
        /// 注册 IK 链（使用算法名称）
        /// </summary>
        /// <param name="name">链名称</param>
        /// <param name="endBoneName">末端骨骼名称</param>
        /// <param name="algorithmName">算法名称（TwoBoneIK/CCD/FABRIK），null 则自动选择</param>
        /// <param name="maxChainLength">最大链长度</param>
        public void RegisterChainByName(string name, string endBoneName,
            string algorithmName = null, int maxChainLength = 3)
        {
            IIKAlgorithm algorithm = null;
            if (!string.IsNullOrEmpty(algorithmName))
            {
                _algorithms.TryGetValue(algorithmName, out algorithm);
            }

            RegisterChain(name, endBoneName, algorithm, maxChainLength);
        }

        // 待构建的链信息
        private readonly Dictionary<string, PendingChainInfo> _pendingChains = new();

        private class PendingChainInfo
        {
            public string Name;
            public string EndBoneName;
            public IIKAlgorithm Algorithm;
            public int MaxChainLength;
        }

        /// <summary>
        /// 构建 IK 链（从末端骨骼向上遍历）
        /// </summary>
        private IKChain BuildChainFromEndBone(string name, string endBoneName, int maxLength, Model model)
        {
            var endBone = model.FindBone(endBoneName, false);
            if (endBone == null)
            {
                Log.Warning($"IK chain: end bone '{endBoneName}' not found.");
                return null;
            }

            var indices = new List<int>();
            var current = endBone;

            while (current != null && indices.Count < maxLength)
            {
                indices.Insert(0, current.Index);
                current = current.ParentBone;
            }

            // 至少需要 2 个骨骼才能做 IK
            if (indices.Count < 2)
            {
                Log.Warning($"IK chain '{endBoneName}': chain too short ({indices.Count} bones), minimum 2 required");
                return null;
            }

            return new IKChain(name, indices.ToArray(), endBoneName);
        }

        /// <summary>
        /// 设置 IK 目标（位置约束）
        /// </summary>
        public void SetIKTarget(string chainName, Vector3? targetPosition, float weight = 1.0f)
        {
            if (string.IsNullOrEmpty(chainName))
                return;

            if (!targetPosition.HasValue)
            {
                ClearIKTarget(chainName);
                return;
            }

            if (!_targets.TryGetValue(chainName, out var target))
            {
                target = new IKTarget();
                _targets[chainName] = target;
            }

            target.Position = targetPosition;
            target.PositionWeight = weight;
        }

        /// <summary>
        /// 设置 IK 方向约束
        /// </summary>
        public void SetIKAim(string chainName, Vector3? aimDirection, float weight = 1.0f)
        {
            if (string.IsNullOrEmpty(chainName))
                return;

            if (!aimDirection.HasValue)
            {
                if (_targets.TryGetValue(chainName, out var target))
                {
                    target.AimDirection = null;
                }
                return;
            }

            if (!_targets.TryGetValue(chainName, out var existingTarget))
            {
                existingTarget = new IKTarget();
                _targets[chainName] = existingTarget;
            }

            existingTarget.AimDirection = aimDirection;
            existingTarget.AimWeight = weight;
        }

        /// <summary>
        /// 设置完整 IK 目标（位置 + 方向）
        /// </summary>
        public void SetIKTarget(string chainName, IKTarget target)
        {
            if (string.IsNullOrEmpty(chainName))
                return;

            if (target == null || !target.IsActive)
            {
                ClearIKTarget(chainName);
                return;
            }

            _targets[chainName] = target;
        }

        /// <summary>
        /// 清除 IK 目标
        /// </summary>
        public void ClearIKTarget(string chainName)
        {
            if (string.IsNullOrEmpty(chainName))
                return;

            _targets.Remove(chainName);
        }

        /// <summary>
        /// 获取 IK 链
        /// </summary>
        public IKChain GetChain(string chainName)
        {
            return _chains.TryGetValue(chainName, out var chain) ? chain : null;
        }

        /// <summary>
        /// 获取 IK 目标
        /// </summary>
        public IKTarget GetTarget(string chainName)
        {
            return _targets.TryGetValue(chainName, out var target) ? target : null;
        }

        /// <summary>
        /// 获取 IK 算法
        /// </summary>
        public IIKAlgorithm GetAlgorithm(string algorithmName)
        {
            return _algorithms.TryGetValue(algorithmName, out var algorithm) ? algorithm : null;
        }

        /// <summary>
        /// 求解所有活动链并应用到骨骼变换
        /// </summary>
        public void Solve(Matrix?[] boneTransforms, Model model)
        {
            if (model == null || boneTransforms == null)
                return;

            // 1. 构建待构建的链
            BuildPendingChains(model);

            // 2. 计算骨骼世界位置
            var worldPositions = ComputeBoneWorldPositions(boneTransforms, model);

            // 3. 对每个活动链求解
            foreach (var (name, target) in _targets)
            {
                if (!target.IsActive)
                    continue;

                if (!_chains.TryGetValue(name, out var chain))
                    continue;

                // 获取算法
                var algorithm = chain.Algorithm ?? GetDefaultAlgorithm(chain);
                if (algorithm == null)
                    continue;

                // 检查是否是 Aim-only 目标
                bool isAimOnlyTarget = !target.Position.HasValue && target.AimDirection.HasValue;

                // 创建求解用的目标副本
                IKTarget solveTarget = target;

                // 如果只有 Aim 目标没有 Position
                if (isAimOnlyTarget)
                {
                    // 如果算法支持 Aim（如 AimIK），直接使用原目标
                    if (algorithm.SupportsAim)
                    {
                        solveTarget = target;
                    }
                    else
                    {
                        // 算法需要 Position，自动生成
                        int rootIdx = chain.BoneIndices[0];
                        Vector3 bindPoseRootPos = model.m_bones[rootIdx].Transform.Translation;
                        float chainLength = CalculateChainLength(chain, worldPositions);
                        Vector3 aimDir = Vector3.Normalize(target.AimDirection.Value);
                        Vector3 generatedPos = bindPoseRootPos + aimDir * chainLength;

                        solveTarget = new IKTarget
                        {
                            Position = generatedPos,
                            PositionWeight = target.AimWeight,
                            AimDirection = target.AimDirection,
                            AimWeight = target.AimWeight,
                            PositionSmoothTime = target.PositionSmoothTime,
                            AimSmoothTime = target.AimSmoothTime,
                            _smoothInitialized = true,
                            _smoothedPosition = generatedPos,
                            _smoothedAimDirection = target.AimDirection.Value
                        };
                    }
                }

                // 应用平滑过渡
                var smoothedTarget = ApplySmoothing(chain, solveTarget, worldPositions);

                // 对于 Aim-only 目标，跳过可达性检查
                if (!isAimOnlyTarget && !IsTargetReachable(chain, smoothedTarget, worldPositions))
                {
                    HandleUnreachableTarget(chain, smoothedTarget, boneTransforms, worldPositions, model);
                    continue;
                }

                try
                {
                    // 求解
                    algorithm.Solve(chain, smoothedTarget, boneTransforms, worldPositions, model,
                        chain.AlgorithmConfig ?? _defaultConfig);

                    // 保存有效结果
                    SaveLastValidResult(chain, boneTransforms);
                }
                catch (System.Exception ex)
                {
                    Log.Error($"[IK] Solve failed for chain {chain.Name}: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// 构建待构建的链
        /// </summary>
        private void BuildPendingChains(Model model)
        {
            foreach (var (name, info) in _pendingChains)
            {
                if (_chains.ContainsKey(name))
                    continue;

                var chain = BuildChainFromEndBone(info.Name, info.EndBoneName, info.MaxChainLength, model);
                if (chain != null)
                {
                    chain.Algorithm = info.Algorithm ?? GetDefaultAlgorithm(chain);
                    _chains[name] = chain;
                }
            }
        }

        /// <summary>
        /// 获取默认算法
        /// </summary>
        private IIKAlgorithm GetDefaultAlgorithm(IKChain chain)
        {
            // 根据链长度选择算法
            int length = chain.Length;

            if (length == 2)
            {
                return _algorithms.TryGetValue("TwoBoneIK", out var algo) ? algo : null;
            }
            else
            {
                return _algorithms.TryGetValue("CCD", out var algo) ? algo : null;
            }
        }

        /// <summary>
        /// 计算骨骼世界位置
        /// </summary>
        private Vector3[] ComputeBoneWorldPositions(Matrix?[] localTransforms, Model model)
        {
            int boneCount = model.m_bones.Count;
            if (_worldPositions == null || _worldPositions.Length != boneCount)
            {
                _worldPositions = new Vector3[boneCount];
            }

            // 递归计算世界位置
            void ComputeRecursive(ModelBone bone, Matrix parentWorld)
            {
                var local = localTransforms[bone.Index] ?? bone.Transform;
                var world = local * parentWorld;
                _worldPositions[bone.Index] = world.Translation;

                foreach (var child in bone.m_childBones)
                {
                    ComputeRecursive(child, world);
                }
            }

            if (model.m_rootBone != null)
            {
                ComputeRecursive(model.m_rootBone, Matrix.Identity);
            }

            return _worldPositions;
        }

        /// <summary>
        /// 应用目标平滑过渡
        /// </summary>
        /// <remarks>
        /// 此方法会修改原始 target 的平滑状态（_smoothedPosition、_positionVelocity 等），
        /// 以便在下一帧保持平滑连续性。返回的 smoothedTarget 包含平滑后的值。
        /// </remarks>
        private IKTarget ApplySmoothing(IKChain chain, IKTarget target, Vector3[] worldPositions)
        {
            if (!target._smoothInitialized)
            {
                // 首次求解，初始化平滑状态
                target._smoothedPosition = target.Position ?? Vector3.Zero;
                target._smoothedAimDirection = target.AimDirection ?? Vector3.UnitZ;
                target._smoothInitialized = true;
                return target;
            }

            // 创建平滑后的目标副本
            var smoothedTarget = new IKTarget
            {
                PositionWeight = target.PositionWeight,
                AimWeight = target.AimWeight,
                Hint = target.Hint,
                PositionSmoothTime = target.PositionSmoothTime,
                AimSmoothTime = target.AimSmoothTime,
                _smoothInitialized = true,
                _smoothedPosition = target._smoothedPosition,
                _positionVelocity = target._positionVelocity,
                _smoothedAimDirection = target._smoothedAimDirection,
                _aimVelocity = target._aimVelocity
            };

            // 位置平滑
            if (target.Position.HasValue && target.PositionSmoothTime > 0)
            {
                smoothedTarget._smoothedPosition = SmoothDampVector3(
                    smoothedTarget._smoothedPosition,
                    target.Position.Value,
                    ref smoothedTarget._positionVelocity,
                    target.PositionSmoothTime);
                smoothedTarget.Position = smoothedTarget._smoothedPosition;

                // 更新原始目标的平滑状态（用于下一帧）
                target._smoothedPosition = smoothedTarget._smoothedPosition;
                target._positionVelocity = smoothedTarget._positionVelocity;
            }
            else
            {
                smoothedTarget.Position = target.Position;
            }

            // 方向平滑
            if (target.AimDirection.HasValue && target.AimSmoothTime > 0)
            {
                var smoothedDir = SmoothDampVector3(
                    smoothedTarget._smoothedAimDirection,
                    target.AimDirection.Value,
                    ref smoothedTarget._aimVelocity,
                    target.AimSmoothTime);
                smoothedTarget._smoothedAimDirection = Vector3.Normalize(smoothedDir);
                smoothedTarget.AimDirection = smoothedTarget._smoothedAimDirection;

                // 更新原始目标的平滑状态（用于下一帧）
                target._smoothedAimDirection = smoothedTarget._smoothedAimDirection;
                target._aimVelocity = smoothedTarget._aimVelocity;
            }
            else
            {
                smoothedTarget.AimDirection = target.AimDirection;
            }

            return smoothedTarget;
        }

        /// <summary>
        /// 检查目标是否可达
        /// </summary>
        private bool IsTargetReachable(IKChain chain, IKTarget target, Vector3[] worldPositions)
        {
            if (!target.Position.HasValue)
                return true;

            // 计算骨骼链总长度
            float chainLength = CalculateChainLength(chain, worldPositions);

            // 计算根骨骼到目标的距离
            float targetDistance = Vector3.Distance(
                worldPositions[chain.BoneIndices[0]],
                target.Position.Value);

            return targetDistance <= chainLength;
        }

        /// <summary>
        /// 计算骨骼链的总长度
        /// </summary>
        private float CalculateChainLength(IKChain chain, Vector3[] worldPositions)
        {
            float length = 0;

            for (int i = 0; i < chain.BoneIndices.Length - 1; i++)
            {
                int idx1 = chain.BoneIndices[i];
                int idx2 = chain.BoneIndices[i + 1];
                length += Vector3.Distance(worldPositions[idx1], worldPositions[idx2]);
            }

            return length;
        }

        /// <summary>
        /// 处理不可达目标
        /// </summary>
        private void HandleUnreachableTarget(IKChain chain, IKTarget target,
            Matrix?[] boneTransforms, Vector3[] worldPositions, Model model)
        {
            switch (chain.UnreachableStrategy)
            {
                case UnreachableStrategy.ExtendTowardTarget:
                    // 算法内部会自动处理伸展
                    var algorithm = chain.Algorithm ?? GetDefaultAlgorithm(chain);
                    if (algorithm != null)
                    {
                        algorithm.Solve(chain, target, boneTransforms, worldPositions, model,
                            chain.AlgorithmConfig ?? _defaultConfig);
                    }
                    break;

                case UnreachableStrategy.KeepCurrentPose:
                    // 保持当前姿势，不做任何修改
                    break;

                case UnreachableStrategy.UseLastValidResult:
                    // 使用上一次有效结果
                    if (_lastValidTransforms.TryGetValue(chain.Name, out var lastValid))
                    {
                        for (int i = 0; i < chain.BoneIndices.Length; i++)
                        {
                            boneTransforms[chain.BoneIndices[i]] = lastValid[i];
                        }
                    }
                    break;
            }
        }

        /// <summary>
        /// 保存有效的 IK 结果
        /// </summary>
        private void SaveLastValidResult(IKChain chain, Matrix?[] boneTransforms)
        {
            var transforms = new Matrix?[chain.BoneIndices.Length];
            for (int i = 0; i < chain.BoneIndices.Length; i++)
            {
                transforms[i] = boneTransforms[chain.BoneIndices[i]];
            }
            _lastValidTransforms[chain.Name] = transforms;
        }

        /// <summary>
        /// 清除所有链和目标
        /// </summary>
        public void Clear()
        {
            _chains.Clear();
            _targets.Clear();
            _pendingChains.Clear();
            _lastValidTransforms.Clear();
        }

        /// <summary>
        /// 平滑阻尼向量（临界阻尼平滑）
        /// </summary>
        private static Vector3 SmoothDampVector3(Vector3 current, Vector3 target, ref Vector3 velocity, float smoothTime)
        {
            // 临界阻尼平滑算法
            // smoothTime 是到达目标的大约时间
            float omega = 2f / smoothTime;
            float x = omega * Time.FrameDuration;
            float exp = 1f / (1f + x + 0.48f * x * x + 0.235f * x * x * x);

            Vector3 change = current - target;

            Vector3 temp = (velocity + omega * change) * Time.FrameDuration;
            velocity = (velocity - omega * temp) * exp;
            Vector3 result = target + (change + temp) * exp;

            // 确保不会越过目标
            if ((target - current).LengthSquared() < (result - current).LengthSquared())
            {
                result = target;
                velocity = Vector3.Zero;
            }

            return result;
        }
    }
}
