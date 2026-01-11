import { dotnet } from './_framework/dotnet.js'
globalThis.dotnet = dotnet;
const { setModuleImports, getAssemblyExports, getConfig, runMain } = await dotnet.withDiagnosticTracing(false).withApplicationArgumentsFromQuery().create();

const config = getConfig();
const engineExports = await getAssemblyExports("Engine.dll");
const interop = engineExports.Engine.Browser.BrowserInterop;

let document = globalThis.document;
let canvas = document.getElementById("canvas");
dotnet.instance.Module["canvas"] = canvas;

let needPointerLock = false;
function checkAndRequestPointerLock(){
    if (needPointerLock) {
        if (document.pointerLockElement !== canvas) {
            return canvas.requestPointerLock({ unadjustedMovement: true }).catch(error => {
                if (error?.name === "NotSupportedError") {
                    // 有些平台可能不支持未调整的移动，尝试重新请求常规指针锁定。
                    return canvas.requestPointerLock();
                }
            });
        }
    }
    else if (document.pointerLockElement === canvas) {
        document.exitPointerLock();
    }
}

let connectedGamepadCount = 0;

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

        function frame() {
            checkCanvasResize(false);
            requestAnimationFrame(frame);
        }

        const keyDown = (e) => {
            e.stopPropagation();
            interop.OnKeyDown(e.code);
            checkAndRequestPointerLock();
        }

        const keyUp = (e) => {
            e.stopPropagation();
            interop.OnKeyUp(e.code);
        }

        const mouseMove = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            interop.OnMouseMove(e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio, e.movementX, e.movementY);
        }

        const mouseDown = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            interop.OnMouseDown(e.button, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
            checkAndRequestPointerLock();
        }

        const mouseUp = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            interop.OnMouseUp(e.button, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
        }

        const mouseWheel = (e) => {
            e.preventDefault();
            interop.OnMouseWheel(-e.deltaY);
        }

        const gamepadConnected = (e) => {
            let gamepad = e.gamepad;
            if (gamepad !== null) {
                connectedGamepadCount++;
                interop.OnGamepadConnected(gamepad.index, gamepad.id, gamepad.buttons.length - 2, gamepad.axes.length / 2, 2);
            }
        }

        const gamepadDisconnected = (e) => {
            let gamepad = e.gamepad;
            if (gamepad !== null) {
                connectedGamepadCount--;
                interop.OnGamepadDisconnected(gamepad.index);
            }
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

        const pointerLockChange = () => {
            if (needPointerLock && document.pointerLockElement !== canvas) {
                interop.OnKeyDown("Escape");
                interop.OnKeyUp("Escape");
            }
        }

        canvas.addEventListener("contextmenu", (e) => e.preventDefault(), false);
        canvas.addEventListener("keydown", keyDown, false);
        canvas.addEventListener("keyup", keyUp, false);
        canvas.addEventListener("mousemove", mouseMove, false);
        canvas.addEventListener("mousedown", mouseDown, false);
        canvas.addEventListener("mouseup", mouseUp, false);
        canvas.addEventListener("wheel", mouseWheel, false);
        globalThis.addEventListener("gamepadconnected", gamepadConnected, false);
        globalThis.addEventListener("gamepaddisconnected", gamepadDisconnected, false);
        canvas.addEventListener("touchstart", touchStart, false);
        canvas.addEventListener("touchmove", touchMove, false);
        canvas.addEventListener("touchend", touchEnd, false);
        document.addEventListener("pointerlockchange", pointerLockChange, false);
        checkCanvasResize(true);
        frame();

        canvas.tabIndex = 1000;

        interop.SetHostedHref(window.location.href);
    },
    getTitle: () => document.title,
    setTitle: (title) => document.title = title,
    getLanguage: () => globalThis.navigator.language,
    close: () => globalThis.close(),
    reload: () => globalThis.location.reload(),
    setDocumentLang : (lang) => document.documentElement.lang = lang,
    openUrlInNewTab: (url) => globalThis.open(url),
    setNeedPointerLock: (need) => {
        needPointerLock = need;
        checkAndRequestPointerLock();
    },
    getGamepadStates: () => {
        if (connectedGamepadCount === 0) {
            return null;
        }
        const gamepads = globalThis.navigator.getGamepads();
        const result = [];
        for (let i = 0; i < gamepads.length; i++) {
            let gamepad = gamepads[i];
            if (gamepad === null || !gamepad.connected || gamepad.mapping !== "standard") {
                continue;
            }
            result.push(gamepad.index);
            for (let j = 0; j < gamepad.buttons.length; j++) {//17
                result.push(gamepad.buttons[j].value);
            }
            for (let j = 0; j < gamepad.axes.length; j ++) {//4
                result.push(gamepad.axes[j]);
            }
        }
        return result;
    }
});
await runMain(config.mainAssemblyName);