#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// CCD (Cyclic Coordinate Descent) IK 算法
    /// 适用于任意长度骨骼链（脊柱、尾巴、触手）
    /// </summary>
    public class CCD : IIKAlgorithm
    {
        public string Name => "CCD";
        public bool SupportsAim => false;

        // 世界位置缓存
        private Vector3[] _worldPosCache;

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
            int endIdx = indices[indices.Length - 1];

            Vector3 targetPos = target.Position.Value;

            // 初始化世界位置缓存
            int boneCount = model.m_bones.Count;
            if (_worldPosCache == null || _worldPosCache.Length != boneCount)
                _worldPosCache = new Vector3[boneCount];

            // 复制初始世界位置
            Array.Copy(worldPositions, _worldPosCache, boneCount);

            // 迭代求解
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // 从末端向根遍历每个骨骼
                for (int i = indices.Length - 2; i >= 0; i--)
                {
                    int boneIdx = indices[i];

                    // 更新世界位置缓存
                    UpdateWorldPositions(boneTransforms, model);

                    Vector3 bonePos = _worldPosCache[boneIdx];
                    Vector3 endPos = _worldPosCache[endIdx];

                    // 当前末端到目标的误差
                    Vector3 toEnd = endPos - bonePos;
                    Vector3 toTarget = targetPos - bonePos;

                    if (toEnd.LengthSquared() < 0.0001f || toTarget.LengthSquared() < 0.0001f)
                        continue;

                    toEnd = Vector3.Normalize(toEnd);
                    toTarget = Vector3.Normalize(toTarget);

                    // 计算旋转轴和角度
                    Vector3 rotationAxis = Vector3.Cross(toEnd, toTarget);

                    if (rotationAxis.LengthSquared() < 0.0001f)
                        continue;

                    rotationAxis = Vector3.Normalize(rotationAxis);

                    // 计算旋转角度
                    float cosAngle = Math.Clamp(Vector3.Dot(toEnd, toTarget), -1f, 1f);
                    float angle = MathF.Acos(cosAngle);

                    // 限制单次旋转角度（避免抖动）
                    const float maxAnglePerIteration = MathF.PI * 0.25f;
                    if (angle > maxAnglePerIteration)
                        angle = maxAnglePerIteration;

                    // 创建旋转四元数
                    Quaternion rotation = Quaternion.CreateFromAxisAngle(rotationAxis, angle);

                    // 应用旋转到骨骼
                    IKUtils.ApplyBoneRotation(boneTransforms, boneIdx, rotation);

                    // 应用关节限制
                    ApplyJointLimit(chain, boneTransforms, boneIdx, model);
                }

                // 更新最终世界位置
                UpdateWorldPositions(boneTransforms, model);

                // 检查收敛
                float error = Vector3.Distance(_worldPosCache[endIdx], targetPos);

                if (error < tolerance)
                    break;
            }
        }

        /// <summary>
        /// 更新所有骨骼的世界位置（基于当前局部变换）
        /// </summary>
        private void UpdateWorldPositions(Matrix?[] boneTransforms, Model model)
        {
            if (model.m_rootBone == null)
                return;

            void ComputeRecursive(ModelBone bone, Matrix parentWorld)
            {
                var local = boneTransforms[bone.Index] ?? bone.Transform;
                var world = local * parentWorld;
                _worldPosCache[bone.Index] = world.Translation;

                foreach (var child in bone.m_childBones)
                {
                    ComputeRecursive(child, world);
                }
            }

            ComputeRecursive(model.m_rootBone, Matrix.Identity);
        }

        /// <summary>
        /// 应用关节限制
        /// </summary>
        private void ApplyJointLimit(IKChain chain, Matrix?[] boneTransforms, int boneIndex, Model model)
        {
            var limit = chain.GetJointLimit(boneIndex, model);
            if (limit != null && boneTransforms[boneIndex].HasValue)
            {
                var transform = boneTransforms[boneIndex].Value;
                boneTransforms[boneIndex] = limit.ApplyLimit(transform);
            }
        }
    }
}
