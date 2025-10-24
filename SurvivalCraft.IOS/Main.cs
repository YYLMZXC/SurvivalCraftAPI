using OpenGLES;
using Silk.NET.OpenGLES;
using Silk.NET.Windowing;
using Silk.NET.Windowing.Sdl;
using Silk.NET.Windowing.Sdl.iOS;
using SurvivalCraft.IOS;
using System.Drawing;
// This is the main entry point of the application.
// If you want to use a different Application Delegate class from "AppDelegate"
// you can specify it here.
//UIApplication.Main(args, null, typeof(AppDelegate));




unsafe {
    SilkMobile.RunApp(["a"], _ => {
        Engine.Window.Created += () => {
            var kitValue = Engine.Window.m_view.Native.UIKit.Value;
            var window = ObjCRuntime.Runtime.GetNSObject<UIWindow>(kitValue.Window);
            var uiView = window.RootViewController.View;
            uiView.Add(new TouchView(uiView));
        };
        Game.Program.EntryPoint();
    });
}


