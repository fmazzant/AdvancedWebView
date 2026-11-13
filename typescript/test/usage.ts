// Compile-only check of advancedwebview.d.ts: tsc --noEmit -p typescript/test

interface User {
    id: number;
    name: string;
}

async function checkTypings(): Promise<void> {
    const bridge: AdvancedWebViewBridge = await AdvancedWebView.ready;
    bridge.postMessage({ type: "checkout", total: 42.5 });
    window.AdvancedWebView.postMessage("plain text");

    const user = await AdvancedWebView.invoke<User>("getUser", { id: 1 });
    const name: string = user.name;
    await AdvancedWebView.invoke("logout");

    try {
        await AdvancedWebView.invoke("missing");
    } catch (e) {
        const err = e as AdvancedWebViewError;
        const code: AdvancedWebViewErrorCode = err.code;
        if (code === "MethodNotFound" || code === "NotFound") {
            console.error(err.message, err.details, name);
        }
    }

    const listener: AdvancedWebViewMessageListener<{ count: number }> = e => console.log(e.data.count);
    AdvancedWebView.addEventListener("message", listener);
    AdvancedWebView.removeEventListener("message", listener);
    AdvancedWebView.addEventListener<string>("message", e => console.log(e.data.toUpperCase()));

    window.addEventListener("advancedwebviewready", () => AdvancedWebView.retry());

    // @ts-expect-error only "message" events exist
    AdvancedWebView.addEventListener("click", () => { });
    // @ts-expect-error the bridge object is read-only
    window.AdvancedWebView = bridge;
    // @ts-expect-error the method name is required
    AdvancedWebView.invoke();
}

void checkTypings;
