using System.Runtime.InteropServices;

namespace Engine.Browser {

    public static unsafe class GamePadBridge {
        static readonly float* _sharedPtr;
        static readonly int _bufferSize = 88 * sizeof(float);

        static GamePadBridge() {
            // 申请非托管内存（不会被 GC 移动）
            _sharedPtr = (float*)NativeMemory.AlignedAlloc((nuint)_bufferSize, 16);
        }

        public static IntPtr GetGamepadBufferPtr() => (IntPtr)_sharedPtr;

        // 提供给 C# 内部读取的 Span
        public static ReadOnlySpan<float> DataSpan => new(_sharedPtr, 88);
    }
}