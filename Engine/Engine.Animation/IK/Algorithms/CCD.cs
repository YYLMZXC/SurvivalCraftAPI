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

            // 迭代求解
            for (int iter = 0; iter < maxIterations; iter++)
            {
                // 从末端向根遍历每个骨骼
                for (int i = indices.Length - 2; i >= 0; i--)
                {
                    int boneIdx = indices[i];
                    int endBoneIdx = indices[indices.Length - 1];

                    Vector3 bonePos = GetBoneWorldPosition(boneTransforms, boneIdx, worldPositions, model);
                    Vector3 endPos = GetBoneWorldPosition(boneTransforms, endBoneIdx, worldPositions, model);

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
                    ApplyBoneRotation(boneTransforms, boneIdx, rotation, model);

                    // 应用关节限制
                    ApplyJointLimit(chain, boneTransforms, boneIdx, model);
                }

                // 检查收敛
                Vector3 currentEndPos = GetBoneWorldPosition(boneTransforms, endIdx, worldPositions, model);
                float error = Vector3.Distance(currentEndPos, targetPos);

                if (error < tolerance)
                    break;
            }
        }

        /// <summary>
        /// 获取骨骼的世界位置
        /// </summary>
        private Vector3 GetBoneWorldPosition(Matrix?[] boneTransforms, int boneIndex,
            Vector3[] worldPositions, Model model)
        {
            if (boneTransforms[boneIndex].HasValue)
            {
                // 如果有局部变换，需要重新计算世界位置
                // 这里简化处理，使用原始世界位置加上变换的位移
                var localTransform = boneTransforms[boneIndex].Value;
                return worldPositions[boneIndex] + localTransform.Translation;
            }
            return worldPositions[boneIndex];
        }

        /// <summary>
        /// 应用骨骼旋转
        /// </summary>
        private void ApplyBoneRotation(Matrix?[] boneTransforms, int boneIndex, Quaternion rotation, Model model)
        {
            if (!boneTransforms[boneIndex].HasValue)
            {
                boneTransforms[boneIndex] = Matrix.CreateFromQuaternion(rotation);
            }
            else
            {
                var current = boneTransforms[boneIndex].Value;
                current.Decompose(out var scale, out var currentRot, out var translation);
                boneTransforms[boneIndex] = Matrix.CreateScale(scale)
                    * Matrix.CreateFromQuaternion(rotation * currentRot)
                    * Matrix.CreateTranslation(translation);
            }
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
