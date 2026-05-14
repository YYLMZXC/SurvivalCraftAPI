using Engine.Graphics;

namespace Engine.Animation {
    /// <summary>
    /// IK 算法共享工具方法
    /// </summary>
    public static class IKUtils {
        /// <summary>
        /// 计算从一个方向到另一个方向的旋转
        /// </summary>
        public static Quaternion RotationBetweenVectors(Vector3 from, Vector3 to) {
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);
            float dot = Vector3.Dot(from, to);

            // 如果方向几乎相同
            if (dot > 0.9999f) {
                return Quaternion.Identity;
            }

            // 如果方向相反
            if (dot < -0.9999f) {
                // 找一个垂直轴旋转 180 度
                Vector3 axis = Vector3.Cross(from, Vector3.UnitY);
                if (axis.LengthSquared() < 0.0001f) {
                    axis = Vector3.Cross(from, Vector3.UnitX);
                }
                if (axis.LengthSquared() < 0.0001f) {
                    axis = Vector3.Cross(from, Vector3.UnitZ);
                }
                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
            }

            // 一般情况
            Vector3 rotationAxis = Vector3.Cross(from, to);
            float s = MathF.Sqrt((1f + dot) * 2f);
            float invS = 1f / s;
            return new Quaternion(rotationAxis.X * invS, rotationAxis.Y * invS, rotationAxis.Z * invS, s * 0.5f);
        }

        /// <summary>
        /// 应用骨骼局部旋转增量（叠加到当前旋转上）
        /// </summary>
        /// <remarks>
        /// 旋转应用顺序：newRot = rotation * currentRot（增量旋转叠加）。
        /// 适用于 CCD/FABRIK 等逐步迭代算法，它们在局部空间计算旋转增量。
        /// <para>
        /// 注意：此方法不保证骨骼世界位置不变。对于需要精确控制末端位置的
        /// 场景（如 TwoBoneIK），应使用 <see cref="ApplyModelRotation"/>。
        /// </para>
        /// </remarks>
        public static void ApplyBoneRotation(Matrix?[] boneTransforms, int boneIndex, Quaternion rotation) {
            if (!boneTransforms[boneIndex].HasValue) {
                boneTransforms[boneIndex] = Matrix.CreateFromQuaternion(rotation);
            }
            else {
                Matrix current = boneTransforms[boneIndex].Value;
                current.Decompose(out Vector3 scale, out Quaternion currentRot, out Vector3 translation);
                boneTransforms[boneIndex] = Matrix.CreateScale(scale)
                    * Matrix.CreateFromQuaternion(rotation * currentRot)
                    * Matrix.CreateTranslation(translation);
            }
        }

        /// <summary>
        /// 应用模型空间旋转增量，保持骨骼世界位置不变
        /// </summary>
        /// <remarks>
        /// 在模型空间中围绕骨骼当前位置施加旋转，确保关节不发生位移。
        /// 用于 IK 求解器需要精确控制末端位置的解析算法（如 TwoBoneIK），
        /// 这些算法直接计算目标世界旋转而非逐步迭代。
        /// <para>
        /// 与 <see cref="ApplyBoneRotation"/> 的区别：
        /// ApplyBoneRotation 在局部空间叠加旋转增量，不保证世界位置不变；
        /// 此方法直接构造目标世界变换再转回局部空间，精确保持世界位置。
        /// </para>
        /// </remarks>
        public static void ApplyModelRotation(
            Matrix?[] boneTransforms, int boneIndex,
            Quaternion modelRotation, Vector3 worldPos, Model model) {
            // 当前世界变换
            Matrix worldTransform = ComputeBoneWorldTransform(boneTransforms, boneIndex, model);
            worldTransform.Decompose(out Vector3 scale, out Quaternion worldRot, out _);

            // 新世界旋转
            Quaternion newWorldRot = modelRotation * worldRot;

            // 新世界变换（位置保持 worldPos）
            Matrix newWorld = Matrix.CreateScale(scale)
                * Matrix.CreateFromQuaternion(newWorldRot)
                * Matrix.CreateTranslation(worldPos);

            // 转回局部空间
            ModelBone bone = model.m_bones[boneIndex];
            if (bone == null || bone.ParentBone == null) {
                boneTransforms[boneIndex] = newWorld;
            }
            else {
                Matrix parentWorld = ComputeBoneWorldTransform(boneTransforms, bone.ParentBone.Index, model);
                boneTransforms[boneIndex] = newWorld * InvertAffine(parentWorld);
            }
        }

        /// <summary>
        /// 求仿射矩阵的逆（Scale * Rotation * Translation 结构）
        /// 逆序：M^-1 = T^-1 * R^-1 * S^-1
        /// </summary>
        public static Matrix InvertAffine(Matrix m) {
            if (!m.Decompose(out Vector3 scale, out Quaternion rot, out Vector3 translation)) {
                return Matrix.Invert(m);
            }
            Quaternion invRot = Quaternion.Inverse(rot);
            Vector3 invScale = new Vector3(
                MathF.Abs(scale.X) > 0.0001f ? 1f / scale.X : 0f,
                MathF.Abs(scale.Y) > 0.0001f ? 1f / scale.Y : 0f,
                MathF.Abs(scale.Z) > 0.0001f ? 1f / scale.Z : 0f);
            Vector3 invTranslation = -translation;
            return Matrix.CreateTranslation(invTranslation)
                * Matrix.CreateFromQuaternion(invRot)
                * Matrix.CreateScale(invScale);
        }

        /// <summary>
        /// 计算骨骼的模型空间变换（从局部变换累积）
        /// </summary>
        public static Matrix ComputeBoneWorldTransform(Matrix?[] boneTransforms, int boneIndex, Model model) {
            ModelBone bone = model.m_bones[boneIndex];
            if (bone == null) {
                return Matrix.Identity;
            }

            // 从当前骨骼向上遍历到根，直接从根向下累积变换
            // 避免每次调用分配 List
            int depth = 0;
            ModelBone current = bone;
            while (current != null) {
                depth++;
                current = current.ParentBone;
            }

            // 复用栈缓冲区（绝大多数骨骼链不超过 64 层）
            int[] path = s_pathBuffer;
            if (path == null || path.Length < depth) {
                path = new int[depth];
                s_pathBuffer = path;
            }

            current = bone;
            for (int i = depth - 1; i >= 0; i--) {
                path[i] = current.Index;
                current = current.ParentBone;
            }

            Matrix worldTransform = Matrix.Identity;
            for (int i = 0; i < depth; i++) {
                int idx = path[i];
                Matrix localTransform = boneTransforms[idx].HasValue ? boneTransforms[idx].Value : model.m_bones[idx].Transform;
                worldTransform = localTransform * worldTransform;
            }
            return worldTransform;
        }

        // ComputeBoneWorldTransform 复用的路径缓冲区
        [ThreadStatic]
        private static int[] s_pathBuffer;

        /// <summary>
        /// 将模型空间旋转增量转换为骨骼局部旋转增量
        /// </summary>
        public static Quaternion ConvertModelRotationToLocal(Matrix?[] boneTransforms, int boneIndex, Quaternion modelRotation, Model model) {
            ModelBone bone = model.m_bones[boneIndex];
            if (bone == null
                || bone.ParentBone == null) {
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
    }
}