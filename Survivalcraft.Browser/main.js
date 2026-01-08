import { dotnet } from './_framework/dotnet.js'
globalThis.dotnet = dotnet;
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.withDiagnosticTracing(false).withApplicationArgumentsFromQuery().create();

const config = getConfig();
const engineExports = await getAssemblyExports("Engine.dll");
const interop = engineExports.Engine.Browser.BrowserInterop;

let canvas = globalThis.document.getElementById("canvas");
dotnet.instance.Module["canvas"] = canvas;

setModuleImports("main.js", {
    initialize: () => {

        let checkCanvasResize = (dispatch) => {
            let devicePixelRatio = window.devicePixelRatio || 1.0;
            let displayWidth = canvas.clientWidth * devicePixelRatio;
            let displayHeight = canvas.clientHeight * devicePixelRatio;

            if (canvas.width !== displayWidth || canvas.height !== displayHeight) {
                canvas.width = displayWidth;
                canvas.height = displayHeight;
                dispatch = true;
            }

            if (dispatch) interop.OnCanvasResize(displayWidth, displayHeight, devicePixelRatio);
        }

        function checkCanvasResizeFrame() {
            checkCanvasResize(false);
            requestAnimationFrame(checkCanvasResizeFrame);
        }

        let keyDown = (e) => {
            e.stopPropagation();
            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let repeat = e.repeat;
            let code = e.keyCode;

            interop.OnKeyDown(shift, ctrl, alt, repeat, code);
        }

        let keyUp = (e) => {
            e.stopPropagation();
            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let code = e.keyCode;

            interop.OnKeyUp(shift, ctrl, alt, code);
        }

        let mouseMove = (e) => {
            let x = e.offsetX;
            let y = e.offsetY;
            interop.OnMouseMove(x, y);
        }

        let mouseDown = (e) => {
            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let button = e.button;

            interop.OnMouseDown(shift, ctrl, alt, button);
        }

        let mouseUp = (e) => {
            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let button = e.button;

            interop.OnMouseUp(shift, ctrl, alt, button);
        }

        let shouldIgnore = (e) => {
            e.preventDefault();
            return e.touches.length > 1 || e.type === "touchend" && e.touches.length > 0;
        }

        let touchStart = (e) => {
            if (shouldIgnore(e)) return;

            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let button = 0;
            let touch = e.changedTouches[0];
            let bcr = e.target.getBoundingClientRect();
            let x = touch.clientX - bcr.x;
            let y = touch.clientY - bcr.y;

            interop.OnMouseMove(x, y);
            interop.OnMouseDown(shift, ctrl, alt, button);
        }

        let touchMove = (e) => {
            if (shouldIgnore(e)) return;

            let touch = e.changedTouches[0];
            let bcr = e.target.getBoundingClientRect();
            let x = touch.clientX - bcr.x;
            let y = touch.clientY - bcr.y;

            interop.OnMouseMove(x, y);
        }

        let touchEnd = (e) => {
            if (shouldIgnore(e)) return;

            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let button = 0;
            let touch = e.changedTouches[0];
            let bcr = e.target.getBoundingClientRect();
            let x = touch.clientX - bcr.x;
            let y = touch.clientY - bcr.y;

            interop.OnMouseMove(x, y);
            interop.OnMouseUp(shift, ctrl, alt, button);
        }

        //canvas.addEventListener("contextmenu", (e) => e.preventDefault(), false);
        canvas.addEventListener("keydown", keyDown, false);
        canvas.addEventListener("keyup", keyUp, false);
        canvas.addEventListener("mousemove", mouseMove, false);
        canvas.addEventListener("mousedown", mouseDown, false);
        canvas.addEventListener("mouseup", mouseUp, false);
        canvas.addEventListener("touchstart", touchStart, false);
        canvas.addEventListener("touchmove", touchMove, false);
        canvas.addEventListener("touchend", touchEnd, false);
        checkCanvasResize(true);
        checkCanvasResizeFrame();

        canvas.tabIndex = 1000;

        interop.SetHostedHref(window.location.href);
    },
    getTitle: () => globalThis.document.title,
    setTitle: (title) => globalThis.document.title = title,
    getLanguage: () => globalThis.navigator.language,
    close: () => globalThis.close(),
    reload: () => globalThis.location.reload(),
});
await runMain(config.mainAssemblyName);
