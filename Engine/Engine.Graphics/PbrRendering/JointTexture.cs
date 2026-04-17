using System;
using System.Numerics;
using Silk.NET.OpenGLES;

namespace Engine.Graphics {
    /// <summary>
    /// 骨骼动画纹理，用于存储关节矩阵
    /// 使用 RGBA32F 纹理存储 mat4 矩阵数组，无骨骼数量限制
    /// </summary>
    public class JointTexture : IDisposable {
        bool _disposed;
        readonly float[] _textureData;
        readonly Matrix4x4[] _normalMatrices;

        /// <summary>
        /// 纹理句柄
        /// </summary>
        public uint TextureHandle { get; private set; }

        /// <summary>
        /// 纹理尺寸（宽高相等）
        /// </summary>
        public int TextureSize { get; }

        /// <summary>
        /// 最大关节数量
        /// </summary>
        public int MaxJointCount { get; }

        /// <summary>
        /// 创建骨骼纹理
        /// </summary>
        /// <param name="maxJoints">最大关节数</param>
        public JointTexture(int maxJoints) {
            MaxJointCount = maxJoints;

            // 每个关节需要 2 个 mat4（jointMatrix + normalMatrix）
            // 每个 mat4 需要 4 个像素（每个像素 RGBA32F = vec4）
            // 所以每个关节需要 8 个像素
            TextureSize = (int)Math.Ceiling(Math.Sqrt(maxJoints * 8));

            _textureData = new float[TextureSize * TextureSize * 4];
            _normalMatrices = new Matrix4x4[maxJoints];
            CreateTexture();
        }

        unsafe void CreateTexture() {
            TextureHandle = GLWrapper.GL.GenTexture();
            GLWrapper.GL.BindTexture(TextureTarget.Texture2D, TextureHandle);

            GLWrapper.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.ClampToEdge);
            GLWrapper.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.ClampToEdge);
            GLWrapper.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Nearest);
            GLWrapper.GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Nearest);

            GLWrapper.GL.TexImage2D(
                TextureTarget.Texture2D,
                0,
                InternalFormat.Rgba32f,
                (uint)TextureSize,
                (uint)TextureSize,
                0,
                PixelFormat.Rgba,
                PixelType.Float,
                null
            );
            GLWrapper.GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        /// <summary>
        /// 更新骨骼纹理数据
        /// </summary>
        /// <param name="jointMatrices">关节矩阵数组（已与逆绑定矩阵相乘）</param>
        public unsafe void Update(Matrix4x4[] jointMatrices) {
            if (jointMatrices == null || jointMatrices.Length == 0) return;
            fixed (Matrix4x4* ptr = jointMatrices) {
                UpdateCore(ptr, Math.Min(jointMatrices.Length, MaxJointCount));
            }
        }

        /// <summary>
        /// 更新骨骼纹理数据（使用 span 避免数组分配）
        /// </summary>
        /// <param name="jointMatrices">关节矩阵 span</param>
        public unsafe void Update(ReadOnlySpan<Matrix4x4> jointMatrices) {
            if (jointMatrices.IsEmpty) return;
            fixed (Matrix4x4* ptr = jointMatrices) {
                UpdateCore(ptr, Math.Min(jointMatrices.Length, MaxJointCount));
            }
        }

        unsafe void UpdateCore(Matrix4x4* matricesPtr, int count) {
            GLWrapper.GL.BindTexture(TextureTarget.Texture2D, TextureHandle);

            for (int i = 0; i < count; i++) {
                Matrix4x4 jointMatrix = matricesPtr[i];

                // 计算法线矩阵（逆转置）
                Matrix4x4.Invert(jointMatrix, out _normalMatrices[i]);
                _normalMatrices[i] = Matrix4x4.Transpose(_normalMatrices[i]);

                // 写入 jointMatrix（offset = i * 32 floats）
                int offset = i * 32;
                WriteMatrixToTextureData(_textureData, offset, jointMatrix);

                // 写入 normalMatrix（offset = i * 32 + 16 floats）
                WriteMatrixToTextureData(_textureData, offset + 16, _normalMatrices[i]);
            }

            fixed (float* ptr = _textureData) {
                GLWrapper.GL.TexSubImage2D(
                    TextureTarget.Texture2D,
                    0,
                    0,
                    0,
                    (uint)TextureSize,
                    (uint)TextureSize,
                    PixelFormat.Rgba,
                    PixelType.Float,
                    ptr
                );
            }
            GLWrapper.GL.BindTexture(TextureTarget.Texture2D, 0);
        }

        static void WriteMatrixToTextureData(float[] data, int offset, Matrix4x4 matrix) {
            // OpenGL 使用列主序存储矩阵，Matrix4x4 是行主序，需要转置
            data[offset + 0] = matrix.M11;
            data[offset + 1] = matrix.M12;
            data[offset + 2] = matrix.M13;
            data[offset + 3] = matrix.M14;
            data[offset + 4] = matrix.M21;
            data[offset + 5] = matrix.M22;
            data[offset + 6] = matrix.M23;
            data[offset + 7] = matrix.M24;
            data[offset + 8] = matrix.M31;
            data[offset + 9] = matrix.M32;
            data[offset + 10] = matrix.M33;
            data[offset + 11] = matrix.M34;
            data[offset + 12] = matrix.M41;
            data[offset + 13] = matrix.M42;
            data[offset + 14] = matrix.M43;
            data[offset + 15] = matrix.M44;
        }

        /// <summary>
        /// 绑定骨骼纹理到指定纹理单元
        /// </summary>
        public void Bind(int textureSlot) {
            GLWrapper.GL.ActiveTexture(TextureUnit.Texture0 + textureSlot);
            GLWrapper.GL.BindTexture(TextureTarget.Texture2D, TextureHandle);
        }

        public void Dispose() {
            if (_disposed) return;

            if (TextureHandle != 0) {
                GLWrapper.GL.DeleteTexture(TextureHandle);
                TextureHandle = 0;
            }
            _disposed = true;
        }
    }
}
