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

            // 计算骨骼长度
            float[] boneLengths = new float[n - 1];
            for (int i = 0; i < n - 1; i++)
            {
                boneLengths[i] = Vector3.Distance(
                    worldPositions[indices[i]],
                    worldPositions[indices[i + 1]]);
            }

            // 计算总链长度
            float totalLength = 0;
            foreach (float len in boneLengths)
                totalLength += len;

            // 检查目标是否可达
            float distToTarget = Vector3.Distance(rootPos, targetPos);

            // 初始化位置数组
            Vector3[] positions = new Vector3[n];
            for (int i = 0; i < n; i++)
            {
                positions[i] = worldPositions[indices[i]];
            }

            // 如果目标不可达，伸展到最大
            if (distToTarget > totalLength)
            {
                Vector3 dir = Vector3.Normalize(targetPos - rootPos);
                for (int i = 1; i < n; i++)
                {
                    positions[i] = positions[i - 1] + dir * boneLengths[i - 1];
                }
            }
            else
            {
                // FABRIK 迭代
                for (int iter = 0; iter < maxIterations; iter++)
                {
                    // 前向阶段：从末端向根
                    positions[n - 1] = targetPos;
                    for (int i = n - 2; i >= 0; i--)
                    {
                        Vector3 diff = positions[i] - positions[i + 1];
                        float dist = diff.Length();
                        if (dist < 0.0001f)
                        {
                            positions[i] = positions[i + 1];
                        }
                        else
                        {
                            Vector3 dir = diff / dist;
                            positions[i] = positions[i + 1] + dir * boneLengths[i];
                        }
                    }

                    // 后向阶段：从根向末端
                    positions[0] = rootPos;
                    for (int i = 1; i < n; i++)
                    {
                        Vector3 diff = positions[i] - positions[i - 1];
                        float dist = diff.Length();
                        if (dist < 0.0001f)
                        {
                            positions[i] = positions[i - 1];
                        }
                        else
                        {
                            Vector3 dir = diff / dist;
                            positions[i] = positions[i - 1] + dir * boneLengths[i - 1];
                        }
                    }

                    // 检查收敛
                    float error = Vector3.Distance(positions[n - 1], targetPos);
                    if (error < tolerance)
                        break;
                }
            }

            // 从位置计算骨骼旋转
            CalculateBoneRotations(chain, positions, boneTransforms, worldPositions, model);

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
                Quaternion rotation = RotationBetweenVectors(oldDir, newDir);

                // 应用旋转
                ApplyBoneRotation(boneTransforms, boneIdx, rotation, model);
            }
        }

        /// <summary>
        /// 计算从一个方向到另一个方向的旋转
        /// </summary>
        private static Quaternion RotationBetweenVectors(Vector3 from, Vector3 to)
        {
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);

            float dot = Vector3.Dot(from, to);

            if (dot > 0.9999f)
                return Quaternion.Identity;

            if (dot < -0.9999f)
            {
                Vector3 axis = Vector3.Cross(from, Vector3.UnitY);
                if (axis.LengthSquared() < 0.0001f)
                    axis = Vector3.Cross(from, Vector3.UnitX);
                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
            }

            Vector3 rotationAxis = Vector3.Cross(from, to);
            float s = MathF.Sqrt((1f + dot) * 2f);
            float invS = 1f / s;

            return new Quaternion(
                rotationAxis.X * invS,
                rotationAxis.Y * invS,
                rotationAxis.Z * invS,
                s * 0.5f);
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
