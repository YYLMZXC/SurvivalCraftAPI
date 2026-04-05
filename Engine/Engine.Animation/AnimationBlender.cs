#nullable disable

using Engine.Graphics;

namespace Engine.Animation
{
    /// <summary>
    /// 动画混合器，负责合并多层骨骼变换
    /// </summary>
    public class AnimationBlender
    {
        // 预分配缓冲区，避免每帧 GC
        private Matrix?[] _layerTransformsBuffer;
        private int _bufferSize;

        /// <summary>
        /// 混合所有活动层的骨骼变换
        /// </summary>
        public void BlendLayers(
            AnimationLayer[] layers,
            Matrix?[] outputTransforms,
            Model model)
        {
            if (layers == null || outputTransforms == null || model == null)
                return;

            int boneCount = model.Bones.Count;

            // 确保缓冲区大小足够
            EnsureBufferSize(boneCount);

            // 清空输出
            Array.Clear(outputTransforms, 0, boneCount);

            foreach (var layer in layers)
            {
                if (layer == null || !layer.IsActive)
                    continue;

                // 使用预分配缓冲区
                Array.Clear(_layerTransformsBuffer, 0, boneCount);
                layer.SampleTransforms(_layerTransformsBuffer, model);

                for (int i = 0; i < boneCount; i++)
                {
                    // 检查骨骼是否在该层的遮罩中
                    if (!IsBoneInMask(i, layer.BoneMask, model))
                        continue;

                    if (!_layerTransformsBuffer[i].HasValue)
                        continue;

                    if (outputTransforms[i].HasValue)
                    {
                        // 混合已有变换
                        outputTransforms[i] = BlendTransforms(
                            outputTransforms[i].Value,
                            _layerTransformsBuffer[i].Value,
                            layer.BlendMode,
                            layer.Weight);
                    }
                    else
                    {
                        // 首次设置
                        outputTransforms[i] = _layerTransformsBuffer[i].Value;
                    }
                }
            }
        }

        /// <summary>
        /// 确保缓冲区大小足够
        /// </summary>
        void EnsureBufferSize(int requiredSize)
        {
            if (_layerTransformsBuffer == null || _bufferSize < requiredSize)
            {
                _bufferSize = Math.Max(requiredSize, 64); // 最小 64 个骨骼
                _layerTransformsBuffer = new Matrix?[_bufferSize];
            }
        }

        bool IsBoneInMask(int boneIndex, string[] boneMask, Model model)
        {
            if (boneMask == null || boneMask.Length == 0)
                return true; // null 表示所有骨骼

            var bone = model.Bones[boneIndex];
            foreach (var maskName in boneMask)
            {
                if (bone.Name == maskName)
                    return true;
            }
            return false;
        }

        Matrix BlendTransforms(
            Matrix existing,
            Matrix incoming,
            AnimationBlendMode mode,
            float weight)
        {
            if (mode == AnimationBlendMode.Override)
            {
                // Override: 按权重插值
                return BlendMatrix(existing, incoming, weight);
            }
            else
            {
                // Additive: 叠加变换
                return existing * Matrix.Lerp(Matrix.Identity, incoming, weight);
            }
        }

        Matrix BlendMatrix(Matrix a, Matrix b, float t)
        {
            // 分解为 T、R、S 分别插值
            DecomposeMatrix(a, out var tA, out var rA, out var sA);
            DecomposeMatrix(b, out var tB, out var rB, out var sB);

            return Matrix.CreateScale(Vector3.Lerp(sA, sB, t))
                 * Matrix.CreateFromQuaternion(Quaternion.Slerp(rA, rB, t))
                 * Matrix.CreateTranslation(Vector3.Lerp(tA, tB, t));
        }

        void DecomposeMatrix(Matrix m, out Vector3 translation, out Quaternion rotation, out Vector3 scale)
        {
            // 提取平移
            translation = m.Translation;

            // 提取缩放
            Vector3 right = new Vector3(m.M11, m.M12, m.M13);
            Vector3 up = new Vector3(m.M21, m.M22, m.M23);
            Vector3 forward = new Vector3(m.M31, m.M32, m.M33);

            float scaleX = right.Length();
            float scaleY = up.Length();
            float scaleZ = forward.Length();
            scale = new Vector3(scaleX, scaleY, scaleZ);

            // 提取旋转
            if (scaleX != 0) right /= scaleX;
            if (scaleY != 0) up /= scaleY;
            if (scaleZ != 0) forward /= scaleZ;

            Matrix rotationMatrix = new Matrix(
                right.X, right.Y, right.Z, 0,
                up.X, up.Y, up.Z, 0,
                forward.X, forward.Y, forward.Z, 0,
                0, 0, 0, 1);

            rotation = Quaternion.CreateFromRotationMatrix(rotationMatrix);

            // 处理负缩放
            if (scaleX * scaleY * scaleZ < 0)
            {
                scale = -scale;
            }
        }
    }
}
