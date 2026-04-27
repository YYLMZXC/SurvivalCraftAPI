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
        /// 应用骨骼旋转（增量旋转，叠加到当前旋转上）
        /// </summary>
        /// <remarks>
        /// 旋转应用顺序：newRot = rotation * currentRot
        /// 这表示 rotation 是一个增量旋转，在当前旋转之前应用。
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
        /// 计算骨骼的模型空间变换（从局部变换累积）
        /// </summary>
        public static Matrix ComputeBoneWorldTransform(Matrix?[] boneTransforms, int boneIndex, Model model) {
            ModelBone bone = model.m_bones[boneIndex];
            if (bone == null) {
                return Matrix.Identity;
            }

            // 收集从当前骨骼到根骨骼的路径
            List<int> path = new();
            ModelBone current = bone;
            while (current != null) {
                path.Add(current.Index);
                current = current.ParentBone;
            }

            // 从根骨骼向下累积变换
            Matrix worldTransform = Matrix.Identity;
            for (int i = path.Count - 1; i >= 0; i--) {
                int idx = path[i];
                Matrix localTransform = boneTransforms[idx].HasValue ? boneTransforms[idx].Value : model.m_bones[idx].Transform;
                worldTransform = localTransform * worldTransform;
            }
            return worldTransform;
        }

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