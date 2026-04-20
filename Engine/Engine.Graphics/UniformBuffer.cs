using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Silk.NET.OpenGLES;

namespace Engine.Graphics {
    /// <summary>
    /// 通用 Uniform Buffer Object (UBO) 实现
    /// 使用 std140 布局
    /// </summary>
    public class UniformBuffer<T> : IDisposable where T : unmanaged {
        readonly uint _handle;
        readonly int _size;

        /// <summary>
        /// UBO 绑定点
        /// </summary>
        public int BindingPoint { get; }

        public unsafe UniformBuffer(int bindingPoint) {
            BindingPoint = bindingPoint;
            _size = Marshal.SizeOf<T>();
            _handle = GLWrapper.GL.GenBuffer();
            GLWrapper.GL.BindBuffer(BufferTargetARB.UniformBuffer, _handle);
            GLWrapper.GL.BufferData(BufferTargetARB.UniformBuffer, (nuint)_size, null, BufferUsageARB.DynamicDraw);
            GLWrapper.GL.BindBufferBase(BufferTargetARB.UniformBuffer, (uint)BindingPoint, _handle);
        }

        /// <summary>
        /// 更新 UBO 数据
        /// </summary>
        public unsafe void Update(ref T data) {
            GLWrapper.GL.BindBuffer(BufferTargetARB.UniformBuffer, _handle);
            fixed (T* ptr = &data) {
                GLWrapper.GL.BufferSubData(BufferTargetARB.UniformBuffer, 0, (nuint)_size, ptr);
            }
        }

        /// <summary>
        /// 绑定到指定着色器的 uniform block
        /// </summary>
        public void BindToShader(uint programHandle, string blockName) {
            uint blockIndex = GLWrapper.GL.GetUniformBlockIndex(programHandle, blockName);
            if (blockIndex != uint.MaxValue) {
                GLWrapper.GL.UniformBlockBinding(programHandle, blockIndex, (uint)BindingPoint);
            }
        }

        public void Dispose() {
            GLWrapper.GL.DeleteBuffer(_handle);
        }
    }
}