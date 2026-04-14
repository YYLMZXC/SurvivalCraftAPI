#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// IK 算法共享工具方法
    /// </summary>
    public static class IKUtils
    {
        /// <summary>
        /// 计算从一个方向到另一个方向的旋转
        /// </summary>
        public static Quaternion RotationBetweenVectors(Vector3 from, Vector3 to)
        {
            from = Vector3.Normalize(from);
            to = Vector3.Normalize(to);

            float dot = Vector3.Dot(from, to);

            // 如果方向几乎相同
            if (dot > 0.9999f)
                return Quaternion.Identity;

            // 如果方向相反
            if (dot < -0.9999f)
            {
                // 找一个垂直轴旋转 180 度
                Vector3 axis = Vector3.Cross(from, Vector3.UnitY);
                if (axis.LengthSquared() < 0.0001f)
                    axis = Vector3.Cross(from, Vector3.UnitX);
                if (axis.LengthSquared() < 0.0001f)
                    axis = Vector3.Cross(from, Vector3.UnitZ);
                return Quaternion.CreateFromAxisAngle(Vector3.Normalize(axis), MathF.PI);
            }

            // 一般情况
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
        /// 应用骨骼旋转（增量旋转，叠加到当前旋转上）
        /// </summary>
        /// <remarks>
        /// 旋转应用顺序：newRot = rotation * currentRot
        /// 这表示 rotation 是一个增量旋转，在当前旋转之前应用。
        /// </remarks>
        public static void ApplyBoneRotation(Matrix?[] boneTransforms, int boneIndex, Quaternion rotation)
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
    }
}
