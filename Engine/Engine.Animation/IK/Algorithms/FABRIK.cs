#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// FABRIK (Forward And Backward Reaching Inverse Kinematics) 算法
    /// 收敛快，结果更自然
    /// </summary>
    public class FABRIK : IIKAlgorithm
    {
        public string Name => "FABRIK";
        public bool SupportsAim => false;

        // 位置缓存
        private Vector3[] _positionsCache;
        // 骨骼长度缓存
        private float[] _boneLengthsCache;

        public void Solve(IKChain chain, IKTarget target,
            Matrix?[] boneTransforms, Vector3[] worldPositions, Model model,
            IKAlgorithmConfig config = null)
        {
            if (chain == null || chain.Length < 2 || !target.Position.HasValue)
                return;

            config ??= new IKAlgorithmConfig();
            int maxIterations = config.MaxIterations;
            float tolerance = config.Tolerance;

            int[] indices = chain.BoneIndices;
            int n = indices.Length;

            Vector3 targetPos = target.Position.Value;
            Vector3 rootPos = worldPositions[indices[0]];

            // 确保缓存足够大
            if (_positionsCache == null || _positionsCache.Length < n)
                _positionsCache = new Vector3[n];
            if (_boneLengthsCache == null || _boneLengthsCache.Length < n - 1)
                _boneLengthsCache = new float[n - 1];

            // 计算骨骼长度
            for (int i = 0; i < n - 1; i++)
            {
                _boneLengthsCache[i] = Vector3.Distance(
                    worldPositions[indices[i]],
                    worldPositions[indices[i + 1]]);
            }

            // 计算总链长度
            float totalLength = 0;
            for (int i = 0; i < n - 1; i++)
                totalLength += _boneLengthsCache[i];

            // 检查目标是否可达
            float distToTarget = Vector3.Distance(rootPos, targetPos);

            // 初始化位置数组
            for (int i = 0; i < n; i++)
            {
                _positionsCache[i] = worldPositions[indices[i]];
            }

            // 如果目标不可达，伸展到最大
            if (distToTarget > totalLength)
            {
                Vector3 dir = Vector3.Normalize(targetPos - rootPos);
                for (int i = 1; i < n; i++)
                {
                    _positionsCache[i] = _positionsCache[i - 1] + dir * _boneLengthsCache[i - 1];
                }
            }
            else
            {
                // FABRIK 迭代
                for (int iter = 0; iter < maxIterations; iter++)
                {
                    // 前向阶段：从末端向根
                    _positionsCache[n - 1] = targetPos;
                    for (int i = n - 2; i >= 0; i--)
                    {
                        Vector3 diff = _positionsCache[i] - _positionsCache[i + 1];
                        float dist = diff.Length();
                        if (dist < 0.0001f)
                        {
                            _positionsCache[i] = _positionsCache[i + 1];
                        }
                        else
                        {
                            Vector3 dir = diff / dist;
                            _positionsCache[i] = _positionsCache[i + 1] + dir * _boneLengthsCache[i];
                        }
                    }

                    // 后向阶段：从根向末端
                    _positionsCache[0] = rootPos;
                    for (int i = 1; i < n; i++)
                    {
                        Vector3 diff = _positionsCache[i] - _positionsCache[i - 1];
                        float dist = diff.Length();
                        if (dist < 0.0001f)
                        {
                            _positionsCache[i] = _positionsCache[i - 1];
                        }
                        else
                        {
                            Vector3 dir = diff / dist;
                            _positionsCache[i] = _positionsCache[i - 1] + dir * _boneLengthsCache[i - 1];
                        }
                    }

                    // 检查收敛
                    float error = Vector3.Distance(_positionsCache[n - 1], targetPos);
                    if (error < tolerance)
                        break;
                }
            }

            // 从位置计算骨骼旋转
            CalculateBoneRotations(chain, _positionsCache, boneTransforms, worldPositions, model);

            // 应用关节限制
            ApplyJointLimits(chain, boneTransforms, model);
        }

        /// <summary>
        /// 从位置数组计算骨骼旋转
        /// </summary>
        private void CalculateBoneRotations(IKChain chain, Vector3[] positions,
            Matrix?[] boneTransforms, Vector3[] worldPositions, Model model)
        {
            int[] indices = chain.BoneIndices;
            int n = indices.Length;

            for (int i = 0; i < n - 1; i++)
            {
                int boneIdx = indices[i];

                // 原始方向
                Vector3 oldDiff = worldPositions[indices[i + 1]] - worldPositions[boneIdx];
                float oldDist = oldDiff.Length();
                if (oldDist < 0.0001f) continue;
                Vector3 oldDir = oldDiff / oldDist;

                // 新方向
                Vector3 newDiff = positions[i + 1] - positions[i];
                float newDist = newDiff.Length();
                if (newDist < 0.0001f) continue;
                Vector3 newDir = newDiff / newDist;

                // 计算旋转
                Quaternion rotation = IKUtils.RotationBetweenVectors(oldDir, newDir);

                // 应用旋转
                IKUtils.ApplyBoneRotation(boneTransforms, boneIdx, rotation);
            }
        }

        /// <summary>
        /// 应用关节限制
        /// </summary>
        private void ApplyJointLimits(IKChain chain, Matrix?[] boneTransforms, Model model)
        {
            if (chain.JointLimits == null || model == null)
                return;

            foreach (int boneIdx in chain.BoneIndices)
            {
                var limit = chain.GetJointLimit(boneIdx, model);
                if (limit != null && boneTransforms[boneIdx].HasValue)
                {
                    var transform = boneTransforms[boneIdx].Value;
                    boneTransforms[boneIdx] = limit.ApplyLimit(transform);
                }
            }
        }
    }
}
