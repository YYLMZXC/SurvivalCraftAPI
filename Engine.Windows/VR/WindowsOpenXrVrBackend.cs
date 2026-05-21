using System.Runtime.InteropServices;
using Engine;
using Silk.NET.OpenXR;

namespace Engine.VR {
    public unsafe class WindowsOpenXrVrBackend : OpenXrVrBackend {
        IntPtr m_hdc;

        protected override StructureType GraphicsBindingType => StructureType.GraphicsBindingOpenglWin32Khr;

        protected override int GetGraphicsBindingSize() => sizeof(GraphicsBindingOpenGLWin32KHR);

        protected override void PopulateGraphicsBinding(void* bindingPtr) {
            m_hdc = GetDC(IntPtr.Zero);
            GraphicsBindingOpenGLWin32KHR* binding = (GraphicsBindingOpenGLWin32KHR*)bindingPtr;
            binding->HDC = m_hdc;
            binding->HGlrc = wglGetCurrentContext();
            ReleaseDC(IntPtr.Zero, m_hdc);
        }

        [DllImport("user32.dll")]
        static extern IntPtr GetDC(IntPtr hWnd);

        [DllImport("user32.dll")]
        static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

        [DllImport("opengl32.dll")]
        static extern IntPtr wglGetCurrentContext();
    }
}
