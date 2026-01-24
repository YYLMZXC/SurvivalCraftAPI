#pragma warning disable CA1416
using System.Runtime.InteropServices.JavaScript;
using Engine.Input;

namespace Engine.Browser {
    public static partial class BrowserInterop {
        //main.js should be in the final project with <OutputType>Exe</OutputType>
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

        [JSImport("showOpenFilePicker", "main.js")]
        public static partial Task<JSObject> ShowOpenFilePicker(string[] descAndExtArray, int[] extCounts, string defaultPath);

        [JSImport("getFileName", "main.js")]
        public static partial string GetFileName(JSObject file);

        [JSImport("getFileBytes", "main.js")]
        public static partial Task<JSObject> GetFileBytes(JSObject file);

        [JSImport("returnSelf", "main.js")]
        public static partial byte[] JSObject2ByteArray(JSObject obj);

        [JSImport("showSaveFilePicker", "main.js")]
        public static partial Task<JSObject> ShowSaveFilePicker(string fileName, string mimeType);

        [JSImport("saveBytesToFileHandle", "main.js")]
        public static partial Task SaveBytesToFileHandle(JSObject fileHandle, byte[] bytes);

        [JSExport]
        public static async Task OnKeyDown(string code, string key) => Keyboard.KeyDownHandler(code, key);

        [JSExport]
        public static async Task OnKeyUp(string code) => Keyboard.KeyUpHandler(code);

        [JSExport]
        public static async Task OnMouseDown(int button, float x, float y) => Mouse.MouseDownHandler(button, x, y);

        [JSExport]
        public static async Task OnMouseMove(float x, float y, float deltaX, float deltaY) => Mouse.MouseMoveHandler(x, y, deltaX, deltaY);

        [JSExport]
        public static async Task OnMouseUp(int button, float x, float y) => Mouse.MouseUpHandler(button, x, y);

        [JSExport]
        public static async Task OnMouseWheel(float value) => Mouse.MouseWheelHandler(value);

        [JSExport]
        public static async Task OnTouchDown(int pointerId, float x, float y) => Touch.TouchDownHandler(pointerId, x, y);

        [JSExport]
        public static async Task OnTouchMove(int pointerId, float x, float y) => Touch.TouchMoveHandler(pointerId, x, y);

        [JSExport]
        public static async Task OnTouchUp(int pointerId, float x, float y) => Touch.TouchUpHandler(pointerId, x, y);

        [JSExport]
        public static async Task OnGamepadConnected(int index, string name) => GamePad.GamepadConnectedHandler(index, name);

        [JSExport]
        public static async Task OnGamepadDisconnected(int index) => GamePad.GamepadDisconnectedHandler(index);

        public static event Action<Point2> CanvasResizeCallback;

        public static Point2 CanvasSize {
            get => field;
            private set {
                if (field != value) {
                    field = value;
                    CanvasResizeCallback?.Invoke(value);
                }
            }
        }

        [JSExport]
        public static async Task OnCanvasResize(float width, float height, float devicePixelRatio) => CanvasSize = new Point2((int)width, (int)height);

        [JSExport]
        public static async Task OnDrop(byte[] data, string fileName) {
            Stream stream = new MemoryStream(data);
            stream.Position = 0;
            Window.FileDropHandler(stream, fileName);
        }

        [JSExport]
        public static async Task OnVisibilityChange(bool visible) => Window.FocusedChangedHandler(visible);

        [JSExport]
        public static async Task SetHostedHref(string href) => Window.HostedHref = href;

        [JSExport]
        public static async Task<IntPtr> GetGamepadBufferPtr() => GamePadBridge.GetGamepadBufferPtr();
    }
}