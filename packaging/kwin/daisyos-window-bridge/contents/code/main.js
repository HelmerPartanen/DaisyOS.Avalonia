// KWin owns window management. This script only publishes an authoritative,
// unprivileged snapshot to the DaisyOS session bus whenever the model changes.
// The shell consumes this to render its taskbar and switcher; it never infers
// windows from processes or X11 stacking state.

var bridgeService = "org.daisyos.Shell.WindowBridge";
var bridgePath = "/org/daisyos/Shell/WindowBridge";
var bridgeInterface = "org.daisyos.Shell.WindowBridge";
var lastSnapshot = "";

function stringValue(value) {
    return value === undefined || value === null ? "" : String(value);
}

function isShellWindow(window) {
    var appId = stringValue(window.desktopFileName).toLowerCase();
    var resource = stringValue(window.resourceClass).toLowerCase();
    var caption = stringValue(window.caption).toLowerCase();
    return appId.indexOf("daisyos-shell") >= 0 || resource.indexOf("daisyos-shell") >= 0 || caption === "daisyos shell";
}

function isApplicationWindow(window) {
    if (!window || window.internal || window.skipTaskbar || isShellWindow(window)) return false;
    // KWin exposes normal clients as managed windows. Keep dialogs visible too:
    // a dialog without a taskbar entry would otherwise be impossible to recover.
    return window.windowType === undefined || window.windowType === 0 || stringValue(window.windowType).toLowerCase().indexOf("normal") >= 0 || stringValue(window.windowType).toLowerCase().indexOf("dialog") >= 0;
}

function describe(window) {
    var desktopFileId = stringValue(window.desktopFileName);
    var appId = desktopFileId || stringValue(window.resourceClass) || stringValue(window.resourceName) || "application";
    return {
        id: stringValue(window.internalId),
        appId: appId,
        title: stringValue(window.caption),
        desktopFileId: desktopFileId || null,
        iconName: stringValue(window.icon) || null,
        outputName: window.output ? stringValue(window.output.name) : null,
        workspace: window.desktops && window.desktops.length > 0 ? Number(window.desktops[0].x11DesktopNumber || 0) : 0,
        isActive: workspace.activeWindow === window,
        isMinimized: !!window.minimized,
        isMaximized: !!window.maximized,
        isFullScreen: !!window.fullScreen,
        canClose: !!window.closeable
    };
}

function publishSnapshot() {
    var windows = workspace.windowList().filter(isApplicationWindow).map(describe);
    var snapshot = JSON.stringify({ windows: windows });
    if (snapshot === lastSnapshot) return;
    lastSnapshot = snapshot;
    callDBus(bridgeService, bridgePath, bridgeInterface, "PublishSnapshot", snapshot, function () {});
}

function observeWindow(window) {
    if (!window) return;
    var refresh = function () { publishSnapshot(); };
    if (window.minimizedChanged) window.minimizedChanged.connect(refresh);
    if (window.maximizedChanged) window.maximizedChanged.connect(refresh);
    if (window.fullScreenChanged) window.fullScreenChanged.connect(refresh);
    if (window.captionChanged) window.captionChanged.connect(refresh);
    if (window.desktopsChanged) window.desktopsChanged.connect(refresh);
    if (window.outputChanged) window.outputChanged.connect(refresh);
}

workspace.windowAdded.connect(function (window) { observeWindow(window); publishSnapshot(); });
workspace.windowRemoved.connect(publishSnapshot);
workspace.windowActivated.connect(publishSnapshot);
workspace.windowList().forEach(observeWindow);
publishSnapshot();
