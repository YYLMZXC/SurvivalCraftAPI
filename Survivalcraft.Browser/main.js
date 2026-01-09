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
        const checkCanvasResize = (dispatch) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            const rect = canvas.getBoundingClientRect();
            const displayWidth = rect.width * devicePixelRatio;
            const displayHeight = rect.height * devicePixelRatio;

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

        const keyDown = (e) => {
            e.stopPropagation();
            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let repeat = e.repeat;
            let code = e.keyCode;

            interop.OnKeyDown(shift, ctrl, alt, repeat, code);
        }

        const keyUp = (e) => {
            e.stopPropagation();
            let shift = e.shiftKey;
            let ctrl = e.ctrlKey;
            let alt = e.altKey;
            let code = e.keyCode;

            interop.OnKeyUp(shift, ctrl, alt, code);
        }

        const mouseMove = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            interop.OnMouseMove(e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
        }

        const mouseDown = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            interop.OnMouseDown(e.button, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
        }

        const mouseUp = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            interop.OnMouseUp(e.button, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
        }

        const mouseWheel = (e) => {
            e.preventDefault();
            interop.OnMouseWheel(-e.deltaY);
        }

        const shouldIgnore = (e) => {
            e.preventDefault();
            return e.touches.length > 1 || e.type === "touchend" && e.touches.length > 0;
        }

        const touchStart = (e) => {
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

        const touchMove = (e) => {
            if (shouldIgnore(e)) return;

            let touch = e.changedTouches[0];
            let bcr = e.target.getBoundingClientRect();
            let x = touch.clientX - bcr.x;
            let y = touch.clientY - bcr.y;

            interop.OnMouseMove(x, y);
        }

        const touchEnd = (e) => {
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
        canvas.addEventListener("wheel", mouseWheel, false);
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
    setDocumentLang : (lang) => globalThis.document.documentElement.lang = lang
});
await runMain(config.mainAssemblyName);