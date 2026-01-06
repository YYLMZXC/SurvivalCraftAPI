#pragma warning disable CA1416
using System.Runtime.InteropServices.JavaScript;

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
        public static void OnMouseMove(float x, float y) { }

        [JSExport]
        public static void OnMouseDown(bool shift, bool ctrl, bool alt, int button) { }

        [JSExport]
        public static void OnMouseUp(bool shift, bool ctrl, bool alt, int button) { }

        public static event Action<Point2> CanvasResize;

        public static Point2 CanvasSize {
            get => field;
            private set {
                if (field != value) {
                    field = value;
                    CanvasResize?.Invoke(value);
                }
            }
        }

        [JSExport]
        public static void OnCanvasResize(float width, float height, float devicePixelRatio) {
            //Test.CanvasResized((int)width, (int)height);
            CanvasSize = new Point2((int)width, (int)height);
        }

        [JSExport]
        public static void SetRootUri(string uri) {
            //Test.BaseAddress = new Uri(uri);
        }

        [JSExport]
        public static void AddLocale(string locale) { }
    }
}