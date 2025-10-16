using GameTest;
using GLKit;
using OpenGLES;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl.iOS;
using System.Drawing;

// This is the main entry point of the application.
// If you want to use a different Application Delegate class from "AppDelegate"
// you can specify it here.
//UIApplication.Main(args, null, typeof(AppDelegate));
unsafe {
    GL gl = null;int c = 0;
    Window.ShouldLoadFirstPartyPlatforms(false);
    Window.TryAdd("Silk.NET.Windowing.Sdl");
    SilkMobile.RunApp(["a"], _ => {

        var api = new GraphicsAPI(ContextAPI.OpenGLES, ContextProfile.Compatability, ContextFlags.Debug, new APIVersion(2, 0));
        var opt = ViewOptions.Default with { API = api };
        IView? view = null;
        if (Window.IsViewOnly)
            view = Window.GetView(opt);
        else
            view = Window.Create(WindowOptions.Default);

        view.Load += () => {
                gl = GL.GetApi(view);
            };
        view.Update += (dt) => {
            if (gl != null) {
                if(c==1)
                gl.ClearColor(Color.Red);
                if (c == 2)
                    gl.ClearColor(Color.Pink);
                if (c == 3)
                    gl.ClearColor(Color.Green);
                view.SwapBuffers();
                c++;
                if (c > 4) c = 0;
            }
        };
        view.Run();
    });

}
