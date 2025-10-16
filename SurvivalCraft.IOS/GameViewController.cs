using Engine;
using GLKit;
using OpenGLES;
using Silk.NET.OpenGLES;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SurvivalCraft.IOS {
    class GameViewController:GLKViewController {


        private GLKView gLKView;
        public override void ViewDidLoad() {
            base.ViewDidLoad();
            gLKView = (GLKView)View;
            gLKView.Context = new EAGLContext(EAGLRenderingAPI.OpenGLES3);
            EAGLContext.SetCurrentContext(gLKView.Context);



        }
    }
}
