# AdvancedWebView

[![Build](https://github.com/fmazzant/AdvancedWebView/actions/workflows/build.yml/badge.svg)](https://github.com/fmazzant/AdvancedWebView/actions/workflows/build.yml)
[![NuGet](https://img.shields.io/nuget/v/Mafesoft.Maui.AdvancedWebView.svg)](https://www.nuget.org/packages/Mafesoft.Maui.AdvancedWebView)

AdvancedWebView is a lightweight `WebView` for .NET MAUI (Android and iOS). It extends the built-in MAUI `WebView`, so navigation events, cookies and user agent keep working, and it adds platform settings that the standard control does not expose.

## Requirements

- .NET 10
- Android 5.0 (API 21) or later
- iOS 15 or later

## Installation

```shell
dotnet add package Mafesoft.Maui.AdvancedWebView
```

Register the handler in `MauiProgram.cs`:

```csharp
using Mafesoft.Maui.Controls;

builder
    .UseMauiApp<App>()
    .UseAdvancedWebView();
```

## Usage

```xml
<ContentPage xmlns:awv="clr-namespace:Mafesoft.Maui.Controls;assembly=Mafesoft.Maui.AdvancedWebView">
    <awv:AdvancedWebView
        x:Name="WebView"
        Source="https://dotnet.microsoft.com"
        JavaScriptEnabled="True"
        ScrollBouncesEnabled="False"
        Navigating="OnNavigating" />
</ContentPage>
```

```csharp
// Block a URL
void OnNavigating(object sender, WebNavigatingEventArgs e)
{
    if (e.Url.StartsWith("https://ads.")) e.Cancel = true;
}

// Clear the HTTP cache
WebView.ClearCache();
```

## Properties

| Property | Default | Android | iOS | Notes |
|---|---|:-:|:-:|---|
| `JavaScriptEnabled` | `true` | ✔ | ✔ | iOS: read when the view is created |
| `JavaScriptCanOpenWindowsAutomatically` | `false` | ✔ | ✔ | iOS: read when the view is created |
| `ScrollBouncesEnabled` | `false` | ✔ | ✔ | Android: overscroll glow |
| `MediaPlaybackRequiresUserGesture` | `false` | ✔ | ✔ | iOS: read when the view is created |
| `AllowsInlineMediaPlayback` | `true` | | ✔ | Read when the view is created |
| `AllowMixedContent` | `false` | ✔ | | HTTP resources in HTTPS pages |
| `AllowFileAccess` | `false` | ✔ | | `file://` and `content://` URLs |
| `CacheMode` | `Default` | ✔ | | `Default`, `NoCache`, `CacheOnly`, `CacheElseNetwork` |

Inherited from the MAUI `WebView`: `Source`, `UserAgent`, `Cookies`, `Navigating`, `Navigated`, `EvaluateJavaScriptAsync`, `GoBack`, `GoForward`, `Reload`.

## Methods

| Method | Description |
|---|---|
| `ClearCache()` | Clears the HTTP cache (memory and disk). Cookies and local storage are kept. |

## JavaScript bridge

The page and the app can exchange messages and call each other, with any URL: remote sites as well as local HTML. The bridge object `window.AdvancedWebView` is injected into every page before the page's own scripts run.

### Messages

```csharp
// Page -> app
webView.MessageReceived += (s, e) =>
{
    string text = e.Message;              // a posted string, or the JSON of a posted object
    var order = e.GetJson<Order>();       // System.Text.Json, camelCase
    Uri? origin = e.Origin;               // e.g. https://shop.example.com
};

// App -> page
await webView.PostMessageAsync("plain text");
await webView.PostMessageAsync(new { type = "cart-updated", count = 3 });
```

```js
AdvancedWebView.postMessage({ type: "checkout", total: 42.5 });
AdvancedWebView.addEventListener("message", e => console.log(e.data));
```

### Calling C# from the page

```csharp
webView.RegisterHandler<UserRequest, User>("getUser", async (req, ct) =>
    await api.GetUserAsync(req!.Id, ct) ?? throw new BridgeException("NotFound", $"User {req.Id} does not exist."));

webView.RegisterHandler("logout", async ct => await auth.LogoutAsync());
```

```js
try {
  const user = await AdvancedWebView.invoke("getUser", { id: 1 });
} catch (err) {
  console.error(err.code, err.message); // "NotFound", "User 1 does not exist."
}
```

| `err.code` | When |
|---|---|
| `MethodNotFound` | No handler with that name |
| `InvalidArguments` | The arguments do not match the handler's request type |
| `Timeout` | The handler ran longer than `webView.Bridge.InvokeTimeout` (default 30 s) |
| `HandlerError` | The handler threw an exception; `err.message` is the exception message |
| your code | The handler threw `BridgeException(code, message)` |

Calls still running when the page navigates away are cancelled through the handler's `CancellationToken`.
Only the exception message reaches the page; set `webView.Bridge.IncludeExceptionDetails = true` while debugging to get the full text in `err.details`.

For trimming and Native AOT, every method has an overload that takes a `JsonTypeInfo<T>` from a `JsonSerializerContext`, and `RegisterRawHandler` works on JSON text.

### Notes

- Only the top-level page can use the bridge; messages from iframes are ignored.
- The bridge answers every site the web view loads. Load only content you trust; `e.Origin` tells you which site sent a message.
- Android needs an Android System WebView that supports `WebMessageListener` (any reasonably recent version); otherwise the bridge is disabled and a warning is logged. On WebViews without document-start scripts the bridge is injected when the page finishes loading: page scripts should wait for `AdvancedWebView.ready` or the `advancedwebviewready` event.

## Sample app

[`samples/AdvancedWebView.Sample`](samples/AdvancedWebView.Sample) is a small browser that shows every feature: switches for the properties, cache mode picker, `ClearCache()`, URL blocking in `Navigating`, `EvaluateJavaScriptAsync`, and a local test page for `alert()`, `confirm()` and `window.open()`.

```shell
dotnet build samples/AdvancedWebView.Sample -t:Run -f net10.0-android
```

## License

[MIT](LICENSE)
