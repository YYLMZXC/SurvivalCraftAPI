#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// 两骨骼 IK 解析解算法
    /// 适用于手臂、腿部等典型情况
    /// </summary>
    public class TwoBoneIK : IIKAlgorithm
    {
        public string Name => "TwoBoneIK";
        public bool SupportsAim => true;

        public void Solve(IKChain chain, IKTarget target,
            Matrix?[] boneTransforms, Vector3[] worldPositions, Model model,
            IKAlgorithmConfig config = null)
        {
            if (chain == null || chain.Length < 2)
                return;

            int[] indices = chain.BoneIndices;
            int rootIdx = indices[0];
            int endIdx = indices[indices.Length - 1];

            // 获取骨骼位置
            Vector3 rootPos = worldPositions[rootIdx];
            Vector3 endPos = worldPositions[endIdx];

            // 如果只有一个骨骼连接（链长度为 2），使用简化逻辑
            // 允许 AimDirection 或 Position
            if (chain.Length == 2)
            {
                if (!target.AimDirection.HasValue && !target.Position.HasValue)
                    return;
                SolveSingleBone(chain, target, boneTransforms, worldPositions, rootIdx, endIdx, rootPos, model);
                return;
            }

            // 链长度 > 2 时，必须有 Position
            if (!target.Position.HasValue)
                return;

            config ??= new IKAlgorithmConfig();

            int midIdx = indices[1];

            // 计算骨骼长度
            float len1 = Vector3.Distance(rootPos, worldPositions[midIdx]);
            float len2 = Vector3.Distance(worldPositions[midIdx], endPos);

            if (len1 < 0.0001f || len2 < 0.0001f)
                return;

            // 目标位置
            Vector3 targetPos = target.Position.Value;

            // 计算目标距离
            float targetDist = Vector3.Distance(rootPos, targetPos);

            // 计算骨骼链总长度
            float totalLen = len1 + len2;

            // 计算根骨骼到目标的方向
            Vector3 toTarget = targetPos - rootPos;
            Vector3 toTargetDir = toTarget.LengthSquared() > 0.0001f
                ? Vector3.Normalize(toTarget)
                : Vector3.UnitY;

            // 计算中间骨骼位置（解析解）
            Vector3 newMidPos;

            if (targetDist >= totalLen)
            {
                // 目标超出骨骼链长度：完全伸展
                newMidPos = rootPos + toTargetDir * len1;
            }
            else if (targetDist <= MathF.Abs(len1 - len2))
            {
                // 目标太近：折叠
                newMidPos = rootPos + toTargetDir * len1 * 0.5f;
            }
            else
            {
                // 使用余弦定理计算中间骨骼位置
                // cos(B) = (a² + c² - b²) / (2ac)
                // 其中 a = len1, b = len2, c = targetDist
                float cosB = (len1 * len1 + targetDist * targetDist - len2 * len2)
                            / (2f * len1 * targetDist);

                cosB = Math.Clamp(cosB, -1f, 1f);
                float angleB = MathF.Acos(cosB);

                // 计算中间骨骼相对目标的位置
                // 需要考虑弯曲方向（Hint）
                Vector3 bendDir = CalculateBendDirection(rootPos, worldPositions[midIdx], targetPos, target.Hint);

                // 构建中间骨骼位置
                // 使用向量旋转计算
                float midDist = len1 * MathF.Cos(angleB);
                float bendOffset = len1 * MathF.Sin(angleB);

                // 从根到目标的方向
                Vector3 forward = toTargetDir;

                // 弯曲方向的垂直分量
                Vector3 bendPerpendicular = Vector3.Cross(forward, bendDir);
                if (bendPerpendicular.LengthSquared() < 0.0001f)
                {
                    bendPerpendicular = Vector3.Cross(forward, Vector3.UnitY);
                    if (bendPerpendicular.LengthSquared() < 0.0001f)
                        bendPerpendicular = Vector3.Cross(forward, Vector3.UnitX);
                }
                bendPerpendicular = Vector3.Normalize(bendPerpendicular);

                // 计算新的中间位置
                newMidPos = rootPos + forward * midDist + bendPerpendicular * bendOffset;
            }

            // 计算根骨骼旋转
            Vector3 midPos = worldPositions[midIdx];
            Vector3 oldRootDiff = midPos - rootPos;
            Vector3 newRootDiff = newMidPos - rootPos;

            if (oldRootDiff.LengthSquared() < 0.0001f || newRootDiff.LengthSquared() < 0.0001f)
                return;

            Vector3 oldRootDir = Vector3.Normalize(oldRootDiff);
            Vector3 newRootDir = Vector3.Normalize(newRootDiff);

            Quaternion rootRotation = IKUtils.RotationBetweenVectors(oldRootDir, newRootDir);

            // 应用根骨骼旋转
            IKUtils.ApplyBoneRotation(boneTransforms, rootIdx, rootRotation);

            // 重新计算中间骨骼位置（基于新的根骨骼旋转）
            // 更新世界位置用于后续计算
            Vector3 newMidWorld = Vector3.Transform(
                midPos - rootPos,
                rootRotation) + rootPos;

            // 计算中间骨骼旋转
            Vector3 oldMidDir = Vector3.Normalize(endPos - midPos);
            Vector3 newMidDir = Vector3.Normalize(targetPos - newMidWorld);

            Quaternion midRotation = IKUtils.RotationBetweenVectors(oldMidDir, newMidDir);

            // 应用中间骨骼旋转
            IKUtils.ApplyBoneRotation(boneTransforms, midIdx, midRotation);

            // 应用关节限制
            ApplyJointLimits(chain, boneTransforms, model);

            // 处理方向约束（瞄准）
            if (target.AimDirection.HasValue && SupportsAim)
            {
                ApplyAimConstraint(chain, target, boneTransforms, worldPositions, model, indices);
            }
        }

        /// <summary>
        /// 单骨骼 IK：旋转骨骼链让 AimAxis 朝向目标方向
        /// 对于链长度为 2 的情况，旋转应用到根骨骼（脖子），以实现抬头效果
        /// </summary>
        private void SolveSingleBone(IKChain chain, IKTarget target,
            Matrix?[] boneTransforms, Vector3[] worldPositions,
            int rootIdx, int endIdx, Vector3 rootPos, Model model)
        {
            // 目标方向（模型空间）
            Vector3 targetDir;
            if (target.AimDirection.HasValue)
            {
                targetDir = Vector3.Normalize(target.AimDirection.Value);
            }
            else if (target.Position.HasValue)
            {
                targetDir = Vector3.Normalize(target.Position.Value - rootPos);
            }
            else
            {
                return;
            }

            // 始终使用末端骨骼（Head）的世界变换来计算当前 AimAxis 方向
            // 因为我们想控制的是"头看向哪里"
            Matrix endWorldTransform = ComputeBoneWorldTransform(boneTransforms, endIdx, model);
            Vector3 currentAimDir = Vector3.Normalize(Vector3.TransformNormal(chain.AimAxis, endWorldTransform));

            // 计算让 AimAxis 指向目标所需的旋转（模型空间）
            Quaternion modelRotation = IKUtils.RotationBetweenVectors(currentAimDir, targetDir);

            // 应用权重
            float weight = target.AimWeight;
            if (weight < 1.0f && weight > 0f)
            {
                modelRotation = Quaternion.Slerp(Quaternion.Identity, modelRotation, weight);
            }

            // 对于两骨骼链，旋转应用到根骨骼（脖子）
            // 需要将模型空间旋转转换为根骨骼的局部旋转
            int targetBoneIdx = (chain.Length == 2) ? rootIdx : endIdx;

            // 转换模型空间旋转到骨骼局部空间
            Quaternion localRotation = ConvertModelRotationToLocal(boneTransforms, targetBoneIdx, modelRotation, model);

            // 应用旋转
            IKUtils.ApplyBoneRotation(boneTransforms, targetBoneIdx, localRotation);
        }

        /// <summary>
        /// 将模型空间旋转增量转换为骨骼局部旋转增量
        /// </summary>
        private Quaternion ConvertModelRotationToLocal(Matrix?[] boneTransforms, int boneIndex, Quaternion modelRotation, Model model)
        {
            var bone = model.m_bones[boneIndex];
            if (bone == null || bone.ParentBone == null)
            {
                // 根骨骼或无父骨骼，模型空间旋转就是局部旋转
                return modelRotation;
            }

            // 获取父骨骼的世界旋转
            int parentIdx = bone.ParentBone.Index;
            Matrix parentWorldTransform = ComputeBoneWorldTransform(boneTransforms, parentIdx, model);
            parentWorldTransform.Decompose(out _, out Quaternion parentWorldRot, out _);

            // 模型空间旋转增量转换为局部空间：
            // 局部增量 = 父世界旋转的逆 * 模型空间增量 * 父世界旋转
            // 这样可以让旋转在正确的坐标系中执行
            Quaternion invParentWorldRot = Quaternion.Inverse(parentWorldRot);
            return invParentWorldRot * modelRotation * parentWorldRot;
        }

        /// <summary>
        /// 计算骨骼的模型空间变换（从局部变换累积）
        /// </summary>
        private Matrix ComputeBoneWorldTransform(Matrix?[] boneTransforms, int boneIndex, Model model)
        {
            var bone = model.m_bones[boneIndex];
            if (bone == null)
                return Matrix.Identity;

            // 收集从当前骨骼到根骨骼的路径
            var path = new List<int>();
            var current = bone;
            while (current != null)
            {
                path.Add(current.Index);
                current = current.ParentBone;
            }

            // 从根骨骼向下累积变换
            Matrix worldTransform = Matrix.Identity;
            for (int i = path.Count - 1; i >= 0; i--)
            {
                int idx = path[i];
                Matrix localTransform = boneTransforms[idx].HasValue
                    ? boneTransforms[idx].Value
                    : model.m_bones[idx].Transform;
                worldTransform = localTransform * worldTransform;
            }

            return worldTransform;
        }

        /// <summary>
        /// 计算弯曲方向
        /// </summary>
        private Vector3 CalculateBendDirection(Vector3 root, Vector3 mid, Vector3 target, Vector3? hint)
        {
            if (hint.HasValue)
            {
                return hint.Value;
            }

            // 默认使用当前弯曲方向
            Vector3 rootToMid = mid - root;
            Vector3 rootToTarget = target - root;

            // 使用叉积确定弯曲方向
            Vector3 bendDir = Vector3.Cross(rootToTarget, rootToMid);

            if (bendDir.LengthSquared() < 0.0001f)
            {
                // 如果共线，使用默认方向
                bendDir = Vector3.Cross(rootToTarget, Vector3.UnitY);
                if (bendDir.LengthSquared() < 0.0001f)
                    bendDir = Vector3.Cross(rootToTarget, Vector3.UnitX);
            }

            return Vector3.Normalize(bendDir);
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

        /// <summary>
        /// 应用瞄准约束（方向约束）
        /// </summary>
        private void ApplyAimConstraint(IKChain chain, IKTarget target,
            Matrix?[] boneTransforms, Vector3[] worldPositions, Model model, int[] indices)
        {
            if (!target.AimDirection.HasValue)
                return;

            int endIdx = indices[indices.Length - 1];

            // 获取瞄准轴
            Vector3 aimAxis = chain.AimAxis;

            // 目标方向
            Vector3 targetDir = Vector3.Normalize(target.AimDirection.Value);

            // 计算当前末端骨骼的朝向
            // 使用 AimAxis 作为骨骼的"前方"方向
            if (boneTransforms[endIdx].HasValue)
            {
                var currentTransform = boneTransforms[endIdx].Value;

                // 将 AimAxis 从骨骼局部空间变换到模型空间
                Vector3 currentAimDir = Vector3.Normalize(Vector3.TransformNormal(aimAxis, currentTransform));

                // 计算旋转
                Quaternion aimRotation = IKUtils.RotationBetweenVectors(currentAimDir, targetDir);

                // 应用权重
                if (target.AimWeight < 1.0f)
                {
                    aimRotation = Quaternion.Slerp(Quaternion.Identity, aimRotation, target.AimWeight);
                }

                // 应用旋转
                IKUtils.ApplyBoneRotation(boneTransforms, endIdx, aimRotation);
            }
        }
    }
}
