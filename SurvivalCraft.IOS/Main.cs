using Silk.NET.Windowing.Sdl.iOS;
// This is the main entry point of the application.
// If you want to use a different Application Delegate class from "AppDelegate"
// you can specify it here.
//UIApplication.Main(args, null, typeof(AppDelegate));





unsafe {
    SilkMobile.RunApp(["entry"], arg => {
        Game.Program.EntryPoint();
    });
}
