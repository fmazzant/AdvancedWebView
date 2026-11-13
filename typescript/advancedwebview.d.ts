// TypeScript typings for the JavaScript bridge of AdvancedWebView (Mafesoft.Maui.AdvancedWebView).
// https://github.com/fmazzant/AdvancedWebView

/** A message posted by the app with `PostMessageAsync`. */
interface AdvancedWebViewMessageEvent<T = unknown> {
    /** The posted value: a string, or the object the app serialized as JSON. */
    readonly data: T;
}

/** A listener registered with `AdvancedWebView.addEventListener("message", ...)`. */
type AdvancedWebViewMessageListener<T = unknown> = (event: AdvancedWebViewMessageEvent<T>) => void;

/**
 * Error codes of a rejected `AdvancedWebView.invoke` Promise.
 * A handler can also throw `BridgeException(code, message)` with a code of its own.
 */
type AdvancedWebViewErrorCode =
    | "MethodNotFound"
    | "InvalidArguments"
    | "Timeout"
    | "HandlerError"
    | "OriginNotAllowed"
    | (string & {});

/** The error a rejected `AdvancedWebView.invoke` Promise carries. */
interface AdvancedWebViewError extends Error {
    /** Why the call failed: one of the codes above, or the code of a `BridgeException`. */
    code: AdvancedWebViewErrorCode;
    /** The full exception text, only when the app sets `Bridge.IncludeExceptionDetails = true`. */
    details?: string;
}

/** The bridge object `window.AdvancedWebView`, injected into every page before the page's own scripts run. */
interface AdvancedWebViewBridge {
    /**
     * Resolves with the bridge once it is available. Already resolved when the bridge is injected
     * at document start; on WebViews without document-start scripts, listen for the
     * `advancedwebviewready` window event instead.
     */
    readonly ready: Promise<AdvancedWebViewBridge>;

    /**
     * Sends a message to the app (`MessageReceived`). Strings arrive as they are; any other value
     * is serialized as JSON (`undefined` becomes `null`).
     */
    postMessage(data?: unknown): void;

    /**
     * Calls a C# handler registered with `RegisterHandler` and resolves with its result.
     * Rejects with an {@link AdvancedWebViewError}.
     * @param method The handler name.
     * @param args The request, serialized as JSON (`undefined` becomes `null`).
     */
    invoke<T = unknown>(method: string, args?: unknown): Promise<T>;

    /** Listens for messages posted by the app. Adding the same listener twice has no effect. */
    addEventListener<T = unknown>(type: "message", listener: AdvancedWebViewMessageListener<T>): void;

    /** Removes a listener added with `addEventListener`. */
    removeEventListener<T = unknown>(type: "message", listener: AdvancedWebViewMessageListener<T>): void;

    /**
     * On the error page (`ErrorPageHtml`), loads the URL that failed again; elsewhere, reloads the page.
     * A normal navigation, so `Navigating` and the external-link rules still apply.
     */
    retry(): void;
}

interface Window {
    /** The AdvancedWebView bridge. Not defined outside AdvancedWebView, for example in a desktop browser. */
    readonly AdvancedWebView: AdvancedWebViewBridge;
}

interface WindowEventMap {
    /** Raised on `window` when the bridge has been injected into the page. */
    advancedwebviewready: Event;
}

/** The AdvancedWebView bridge. Not defined outside AdvancedWebView, for example in a desktop browser. */
declare const AdvancedWebView: AdvancedWebViewBridge;
