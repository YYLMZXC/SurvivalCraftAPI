using Engine;
using Engine.Graphics;

namespace Game {
    /// <summary>
    /// 菜单阶段 VR Camera：空旷黑色空间中的固定视角。
    /// UI 渲染为世界空间幕布，不需要游戏场景渲染。
    /// </summary>
    public class VrMenuCamera : Camera {
        Matrix m_viewMatrix;
        Matrix m_projectionMatrix;
        Vector3 m_cameraPosition;

        public VrMenuCamera() : base(null) { }

        public void SetEye(VrEye eye, EyeFrame eyeFrame) {
            m_viewMatrix = eyeFrame.ViewMatrix;
            m_projectionMatrix = eyeFrame.ProjectionMatrix;
            m_cameraPosition = eyeFrame.CameraPosition;
        }

        public override Vector3 ViewPosition => m_cameraPosition;
        public override Vector3 ViewDirection => -m_viewMatrix.Forward;
        public override Vector3 ViewUp => m_viewMatrix.Up;
        public override Vector3 ViewRight => m_viewMatrix.Right;
        public override Matrix ViewMatrix => m_viewMatrix;
        public override Matrix ProjectionMatrix => m_projectionMatrix;

        public override Matrix InvertedViewMatrix => Matrix.Invert(m_viewMatrix);
        public override Matrix ScreenProjectionMatrix => m_projectionMatrix;
        public override Matrix InvertedProjectionMatrix => Matrix.Invert(m_projectionMatrix);
        public override Matrix ViewProjectionMatrix => m_viewMatrix * m_projectionMatrix;
        public override Vector2 ViewportSize => new(VrManager.SwapchainWidth, VrManager.SwapchainHeight);
        public override Matrix ViewportMatrix => Matrix.Identity;

        BoundingFrustum m_viewFrustum;
        bool m_viewFrustumValid;
        public override BoundingFrustum ViewFrustum {
            get {
                if (!m_viewFrustumValid) {
                    m_viewFrustum ??= new BoundingFrustum(ViewProjectionMatrix);
                    m_viewFrustum.Matrix = ViewProjectionMatrix;
                    m_viewFrustumValid = true;
                }
                return m_viewFrustum;
            }
        }

        public override bool UsesMovementControls => false;
        public override bool IsEntityControlEnabled => false;

        public override void Update(float dt) { }
        public override void PrepareForDrawing() {
            m_viewFrustumValid = false;
        }
    }

    /// <summary>
    /// 游戏阶段 VR Camera：包装现有游戏 Camera，叠加 HMD 追踪。
    /// ViewMatrix = Translate(-innerPosition) * VR_ViewMatrix
    /// ProjectionMatrix = VR 投影（不含 viewport 变换链）
    /// </summary>
    public class VrGameCamera : Camera {
        readonly BasePerspectiveCamera m_inner;
        Matrix m_vrViewMatrix;
        Vector3 m_vrCameraPosition;
        Matrix m_projectionMatrix;

        Matrix? m_cachedViewMatrix;
        Matrix? m_cachedInvertedViewMatrix;
        Matrix? m_cachedViewProjectionMatrix;
        Matrix? m_cachedInvertedProjectionMatrix;
        bool m_viewFrustumValid;
        BoundingFrustum m_viewFrustum;

        public VrGameCamera(BasePerspectiveCamera inner) : base(inner.GameWidget) {
            m_inner = inner;
        }

        public void SetEye(VrEye eye, EyeFrame eyeFrame) {
            m_vrViewMatrix = eyeFrame.ViewMatrix;
            m_vrCameraPosition = eyeFrame.CameraPosition;
            m_projectionMatrix = VrManager.GetProjectionMatrix(eye, 0.1f, 2048f);
        }

        public BasePerspectiveCamera InnerCamera => m_inner;

        public override Vector3 ViewPosition =>
            m_inner.m_viewPosition + m_vrCameraPosition;

        public override Vector3 ViewDirection => m_inner.ViewDirection;
        public override Vector3 ViewUp => m_inner.ViewUp;
        public override Vector3 ViewRight => m_inner.ViewRight;

        public override Matrix ViewMatrix {
            get {
                m_cachedViewMatrix ??= Matrix.CreateTranslation(-m_inner.m_viewPosition) * m_vrViewMatrix;
                return m_cachedViewMatrix.Value;
            }
        }

        public override Matrix ProjectionMatrix => m_projectionMatrix;

        public override Matrix InvertedViewMatrix {
            get {
                m_cachedInvertedViewMatrix ??= Matrix.Invert(ViewMatrix);
                return m_cachedInvertedViewMatrix.Value;
            }
        }

        public override Matrix ScreenProjectionMatrix => ProjectionMatrix;

        public override Matrix InvertedProjectionMatrix {
            get {
                m_cachedInvertedProjectionMatrix ??= Matrix.Invert(ProjectionMatrix);
                return m_cachedInvertedProjectionMatrix.Value;
            }
        }

        public override Matrix ViewProjectionMatrix {
            get {
                m_cachedViewProjectionMatrix ??= ViewMatrix * ProjectionMatrix;
                return m_cachedViewProjectionMatrix.Value;
            }
        }

        public override Vector2 ViewportSize => new(VrManager.SwapchainWidth, VrManager.SwapchainHeight);

        public override Matrix ViewportMatrix => Matrix.Identity;

        public override BoundingFrustum ViewFrustum {
            get {
                if (!m_viewFrustumValid) {
                    m_viewFrustum ??= new BoundingFrustum(ViewProjectionMatrix);
                    m_viewFrustum.Matrix = ViewProjectionMatrix;
                    m_viewFrustumValid = true;
                }
                return m_viewFrustum;
            }
        }

        public override bool UsesMovementControls => m_inner.UsesMovementControls;
        public override bool IsEntityControlEnabled => m_inner.IsEntityControlEnabled;

        public override void Update(float dt) => m_inner.Update(dt);

        public override void Activate(Camera previousCamera) => m_inner.Activate(previousCamera);

        public override void PrepareForDrawing() {
            m_cachedViewMatrix = null;
            m_cachedInvertedViewMatrix = null;
            m_cachedViewProjectionMatrix = null;
            m_cachedInvertedProjectionMatrix = null;
            m_viewFrustumValid = false;
        }
    }
}
