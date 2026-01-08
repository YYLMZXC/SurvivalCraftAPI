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

        [JSExport]
        public static void OnKeyDown(bool shift, bool ctrl, bool alt, bool repeat, int code) { }

        [JSExport]
        public static void OnKeyUp(bool shift, bool ctrl, bool alt, int code) { }

        [JSExport]
        public static void OnMouseMove(float x, float y) => Mouse.MouseMoveHandler(x, y);

        [JSExport]
        public static void OnMouseDown(int button, float x, float y) => Mouse.MouseDownHandler(button, x, y);

        [JSExport]
        public static void OnMouseUp(int button, float x, float y) => Mouse.MouseUpHandler(button, x, y);

        [JSExport]
        public static void OnMouseWheel(float value) => Mouse.MouseWheelHandler(value);

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
        public static void OnCanvasResize(float width, float height, float devicePixelRatio) {
            //Test.CanvasResized((int)width, (int)height);
            CanvasSize = new Point2((int)width, (int)height);
        }

        [JSExport]
        public static void SetHostedHref(string href) {
            //Test.BaseAddress = new Uri(uri);
            Window.HostedHref = href;
        }

        [JSExport]
        public static void AddLocale(string locale) { }
    }
}