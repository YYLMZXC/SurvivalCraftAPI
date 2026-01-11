#pragma warning disable CA1416
using System.Runtime.InteropServices.JavaScript;
using Engine.Input;

namespace Engine.Browser {
    public static partial class BrowserInterop {
        [JSImport("initialize", "main.js")]
        public static partial void Initialize();

        [JSImport("getTitle", "main.js")]
        public static partial string GetTitle();

        [JSImport("setTitle", "main.js")]
        public static partial void SetTitle(string title);

        [JSImport("getLanguage", "main.js")]
        public static partial string GetLanguage();

        [JSImport("close", "main.js")]
        public static partial void Close();

        [JSImport("reload", "main.js")]
        public static partial void Reload();

        [JSImport("setDocumentLang", "main.js")]
        public static partial void SetDocumentLang(string lang);

        [JSImport("openUrlInNewTab", "main.js")]
        public static partial void OpenUrlInNewTab(string url);

        [JSImport("setNeedPointerLock", "main.js")]
        public static partial void SetNeedPointerLock(bool need);

        [JSImport("getGamepadStates", "main.js")]
        public static partial double[] GetGamepadStates();

        [JSExport]
        public static void OnKeyDown(string code) => Keyboard.KeyDownHandler(code);

        [JSExport]
        public static void OnKeyUp(string code) => Keyboard.KeyUpHandler(code);

        [JSExport]
        public static void OnMouseMove(float x, float y, float deltaX, float deltaY) => Mouse.MouseMoveHandler(x, y, deltaX, deltaY);

        [JSExport]
        public static void OnMouseDown(int button, float x, float y) => Mouse.MouseDownHandler(button, x, y);

        [JSExport]
        public static void OnMouseUp(int button, float x, float y) => Mouse.MouseUpHandler(button, x, y);

        [JSExport]
        public static void OnMouseWheel(float value) => Mouse.MouseWheelHandler(value);

        [JSExport]
        public static void OnGamepadConnected(int index, string name) => GamePad.GamepadConnectedHandler(index, name);

        [JSExport]
        public static void OnGamepadDisconnected(int index) => GamePad.GamepadDisconnectedHandler(index);

        public static event Action<Point2> CanvasResizeCallback;

        public static Point2 CanvasSize {
            get => field;
            private set {
                if (field != value) {
                    Console.WriteLine($"CanvasSize: old {field.X},{field.Y}; new {value.X},{value.Y}; event count: {CanvasResizeCallback?.GetInvocationList().Length}");//输出：CanvasSize: old 0,0; new 3295,1853; event count:
                    Console.WriteLine("CanvasSize " + typeof(BrowserInterop).Assembly.FullName);
                    field = value;
                    CanvasResizeCallback?.Invoke(value);
                }
            }
        }

        [JSExport]
        public static void OnCanvasResize(float width, float height, float devicePixelRatio) => CanvasSize = new Point2((int)width, (int)height);

        [JSExport]
        public static void SetHostedHref(string href) => Window.HostedHref = href;
    }
}