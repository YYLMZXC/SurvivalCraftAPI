using Engine;
using Engine.Graphics;

namespace Game {
    public class VrManager {
        static IVrBackend _backend;

        public static void SetBackend(IVrBackend backend) => _backend = backend;

        public static bool IsVrAvailable => _backend?.IsAvailable ?? false;

        public static bool IsVrStarted => _backend?.IsStarted ?? false;

        public static RenderTarget2D VrRenderTarget => _backend?.VrRenderTarget;

        public static Matrix HmdMatrix => _backend?.HmdMatrix ?? default;

        public static Matrix HmdMatrixInverted => _backend?.HmdMatrixInverted ?? default;

        public static Vector3 HmdMatrixYpr => _backend?.HmdMatrixYpr ?? default;

        public static Matrix HmdLastMatrix => _backend?.HmdLastMatrix ?? default;

        public static Matrix HmdLastMatrixInverted => _backend?.HmdLastMatrixInverted ?? default;

        public static Vector3 HmdLastMatrixYpr => _backend?.HmdLastMatrixYpr ?? default;

        public static Vector2 HeadMove => _backend?.HeadMove ?? default;

        public static Vector2 WalkingVelocity => _backend?.WalkingVelocity ?? default;

        public static void Initialize() {
            Window.Closed += Shutdown;
            _backend?.Initialize();
        }

        public static void StartVr() => _backend?.StartVr();

        public static void StopVr() => _backend?.StopVr();

        public static void WaitGetPoses() { }

        public static void SubmitEyeTexture(VrEye eye, Texture2D texture) { }

        public static Matrix GetEyeToHeadTransform(VrEye eye) => _backend?.GetEyeToHeadTransform(eye) ?? default;

        public static Matrix GetProjectionMatrix(VrEye eye, float near, float far) => _backend?.GetProjectionMatrix(eye, near, far) ?? default;

        public static bool IsControllerPresent(VrController controller) => _backend?.IsControllerPresent(controller) ?? false;

        public static Matrix GetControllerMatrix(VrController controller) => _backend?.GetControllerMatrix(controller) ?? default;

        public static Vector2 GetStickPosition(VrController controller, float deadZone = 0f) => _backend?.GetStickPosition(controller, deadZone) ?? default;

        public static Vector2? GetTouchpadPosition(VrController controller, float deadZone = 0f) => _backend?.GetTouchpadPosition(controller, deadZone);

        public static float GetTriggerPosition(VrController controller, float deadZone = 0f) => _backend?.GetTriggerPosition(controller, deadZone) ?? 0f;

        public static bool IsButtonDown(VrController controller, VrControllerButton button) => _backend?.IsButtonDown(controller, button) ?? false;

        public static bool IsButtonDownOnce(VrController controller, VrControllerButton button) => _backend?.IsButtonDownOnce(controller, button) ?? false;

        public static TouchInput? GetTouchInput(VrController controller) => null; // TODO: VR 控制器触摸板输入未实现

        public static bool BeginFrame() => _backend?.BeginFrame() ?? false;

        public static EyeFrame GetEyeFrame(VrEye eye) => _backend?.GetEyeFrame(eye) ?? default;

        public static void ReleaseEye(VrEye eye) => _backend?.ReleaseEye(eye);

        public static void EndFrame() => _backend?.EndFrame();

        public static void Update() => _backend?.Update();

        public static void Shutdown() {
            if (_backend == null) return;
            try {
                Program.DisableVrCameras();
                _backend.Dispose();
            }
            catch (Exception ex) {
                Log.Error($"VR shutdown error: {ex}");
            }
            _backend = null;
        }

        public static int SwapchainWidth => _backend?.SwapchainWidth ?? 0;

        public static int SwapchainHeight => _backend?.SwapchainHeight ?? 0;
    }
}
