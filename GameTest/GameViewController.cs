using GLKit;
using OpenGLES;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.iOS;
using System.Drawing;


namespace GameTest {
    class GameViewController : GLKViewController{
        public override void ViewDidLoad() {
            base.ViewDidLoad();
            var gLKView = (GLKView)View;
            gLKView.Context = new EAGLContext(EAGLRenderingAPI.OpenGLES3);
            EAGLContext.SetCurrentContext(gLKView.Context);

            Silk.NET.Windowing.Window.ShouldLoadFirstPartyPlatforms(false);
            Silk.NET.Windowing.Window.TryAdd("Silk.NET.Windowing.Sdl");
            var window = Window.GetView(ViewOptions.Default);
            var gl = GL.GetApi(window);
            unsafe {
                SilkMobile.RunApp(0, null, arg => {

                    gl.ClearColor(Color.AliceBlue);
                });

            }
        }
    }
}
