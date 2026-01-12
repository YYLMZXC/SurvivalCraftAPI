import { dotnet } from './_framework/dotnet.js'

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

        const pointerDown = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            switch (e.pointerType) {
                case "mouse":
                case "pen":
                    interop.OnMouseDown(e.button, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
                    checkAndRequestPointerLock();
                    break;
                case "touch":
                    interop.OnTouchDown(e.pointerId, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
                    break;
            }
        }

        const pointerMove = (e) => {
            const devicePixelRatio = window.devicePixelRatio || 1.0;
            switch (e.pointerType) {
                case "mouse":
                case "pen":
                    interop.OnMouseMove(e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio, e.movementX, e.movementY);
                    break;
                case "touch":
                    interop.OnTouchMove(e.pointerId, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
                    break
            }
        }

        const pointerUp = (e) => {
            switch (e.pointerType) {
                case "mouse":
                case "pen":
                    interop.OnMouseUp(e.button, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
                    break;
                case "touch":
                    interop.OnTouchUp(e.pointerId, e.offsetX * devicePixelRatio, e.offsetY * devicePixelRatio);
                    break;
            }
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

        const pointerLockChange = () => {
            if (needPointerLock && document.pointerLockElement !== canvas) {
                interop.OnKeyDown("Escape");
                interop.OnKeyUp("Escape");
            }
        }

        const drop = async (e) => {
            e.preventDefault();
            if (e.dataTransfer.files.length > 0) {
                const file = e.dataTransfer.files[0];
                const buffer = await file.arrayBuffer();
                interop.OnDrop(new Uint8Array(buffer), file.name);
            }
        }

        canvas.addEventListener("contextmenu", (e) => e.preventDefault(), false);
        canvas.addEventListener("keydown", keyDown, false);
        canvas.addEventListener("keyup", keyUp, false);
        canvas.addEventListener("pointerdown", pointerDown, false);
        canvas.addEventListener("pointermove", pointerMove, false);
        canvas.addEventListener("pointerup", pointerUp, false);
        canvas.addEventListener("wheel", mouseWheel, false);
        globalThis.addEventListener("gamepadconnected", gamepadConnected, false);
        globalThis.addEventListener("gamepaddisconnected", gamepadDisconnected, false);
        document.addEventListener("pointerlockchange", pointerLockChange, false);
        canvas.addEventListener("drop", drop, false);
        canvas.addEventListener("dragover", e => e.preventDefault(), false);
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
    },
    showOpenFilePicker: async (descAndExtArray, extCounts) => {
        let types = [];
        let index = 0;
        for (let i = 0; i < extCounts.length; i++) {
            const desc = descAndExtArray[index++];
            const count = extCounts[i];
            let extensions = [];
            for (let j = 0; j < count; j++) {
                extensions.push(descAndExtArray[index++]);
            }
            types.push({
                description: desc,
                accept: {
                    "*/*": extensions
                }
            });
        }
        let fileHandles = await globalThis.showOpenFilePicker({
            types: types,
            excludeAcceptAllOption: true,
            multiple: false
        });
        if (fileHandles.length > 0) {
            const fileHandle = fileHandles[0];
            return fileHandle.getFile();
        }
        return null;
    },
    getFileName: (file) => file?.name ?? "",
    getFileBytes: async (file) => {
        if(file === null) {
            return [];
        }
        const buffer = await file.arrayBuffer();
        return new Uint8Array(buffer);
    },
    returnSelf: value => value
});
await runMain(config.mainAssemblyName);