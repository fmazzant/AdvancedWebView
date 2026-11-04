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
| `NewWindowBehavior` | `OpenInSameView` | ✔ | ✔ | `target="_blank"` and `window.open`: `OpenInSameView`, `OpenInBrowser`, `Cancel` |

Inherited from the MAUI `WebView`: `Source`, `UserAgent`, `Cookies`, `Navigating`, `Navigated`, `EvaluateJavaScriptAsync`, `GoBack`, `GoForward`, `Reload`.

### Page state

Read-only bindable properties, updated by the web view on Android and iOS:

| Property | Type | Description |
|---|---|---|
| `Progress` | `double` | Load progress of the current page, from 0 to 1 |
| `IsLoading` | `bool` | Whether a page is loading |
| `Title` | `string?` | `document.title`; `null` when the page has none |
| `CurrentUrl` | `string?` | URL of the current page; also follows `history.pushState`, which does not raise `Navigated` |

```xml
<ProgressBar Progress="{Binding Progress, Source={x:Reference WebView}}"
             IsVisible="{Binding IsLoading, Source={x:Reference WebView}}" />
<Label Text="{Binding Title, Source={x:Reference WebView}}" />
<Label Text="{Binding CurrentUrl, Source={x:Reference WebView}}" />
```

### New windows

Links with `target="_blank"` and calls to `window.open(url)` follow `NewWindowBehavior`. Handle `NewWindowRequested` to decide per URL:

```csharp
webView.NewWindowBehavior = NewWindowAction.OpenInSameView; // default

webView.NewWindowRequested += (s, e) =>
{
    // e.Url, e.IsUserGesture
    if (!e.Url.StartsWith("https://shop.example.com"))
        e.Action = NewWindowAction.OpenInBrowser; // OpenInSameView, OpenInBrowser, Cancel
};
```

- `OpenInSameView` raises `Navigating` as a normal navigation, so URLs you block there stay blocked.
- `window.open` without a user gesture is blocked by the web view while `JavaScriptCanOpenWindowsAutomatically` is `false` (the default). `window.open()` without a URL is ignored.
- iOS does not report the user gesture of `window.open`. `IsUserGesture` is `true` for links, and for `window.open` while `JavaScriptCanOpenWindowsAutomatically` is `false`; otherwise `window.open` is reported as `false`.
- Android: a form with `target="_blank"` and `method="post"` opened in the same view is loaded as a GET, without its body. iOS keeps the POST.

### Platform delegates

On Android, AdvancedWebView sets subclasses of MAUI's `MauiWebViewClient` and `MauiWebChromeClient`; on iOS, subclasses of `MauiWebViewNavigationDelegate` and `MauiWebViewUIDelegate`. If your app replaces them, page state and new-window handling stop working.

## Methods

| Method | Description |
|---|---|
| `ClearCache()` | Clears the HTTP cache (memory and disk). Cookies and local storage are kept. |

## JavaScript bridge

The page and the app can exchange messages and call each other, with local HTML and with the remote sites you allow. The bridge object `window.AdvancedWebView` is injected into every page before the page's own scripts run.

### Allowed origins (read this first)

Local content (an `HtmlWebViewSource` or files packaged with the app) can always use the bridge. **Remote sites cannot, until you list them:**

```csharp
webView.Bridge.AllowedOrigins = ["https://shop.example.com", "https://*.example.com"];
webView.Bridge.AllowSubframes = false; // default: iframes cannot use the bridge
```

Rules are `https://example.com`, `https://example.com:8443`, `https://*.example.com` (subdomains only, not `example.com`) or `*` (any site, not recommended). Messages from other origins are dropped, with a warning in the log, and their `invoke` calls fail with `OriginNotAllowed`.

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
| `OriginNotAllowed` | The page's origin is not in `Bridge.AllowedOrigins` |
| your code | The handler threw `BridgeException(code, message)` |

Calls still running when the page navigates away are cancelled through the handler's `CancellationToken`.
Only the exception message reaches the page; set `webView.Bridge.IncludeExceptionDetails = true` while debugging to get the full text in `err.details`.

For trimming and Native AOT, every method has an overload that takes a `JsonTypeInfo<T>` from a `JsonSerializerContext`, and `RegisterRawHandler` works on JSON text.

### Calling page functions from C#

```csharp
int total = await webView.InvokeJavaScriptAsync<int>("cart.getTotal");
await webView.InvokeJavaScriptAsync("app.showToast", "Saved", 3000);

// Trimming/AOT: arguments as a JSON array, result through a JsonSerializerContext
User? user = await webView.InvokeJavaScriptAsync("app.getUser", AppJsonContext.Default.User, "[42]");
```

Arguments are passed as JSON data, never concatenated into the script, so quotes and newlines are safe. If the function returns a Promise it is awaited. Errors thrown by the function (or a rejected Promise, or a missing function) raise `JavaScriptException` with the message and `JavaScriptStack`; no answer within `Bridge.InvokeTimeout` raises `TimeoutException`. This works on any page, whatever `AllowedOrigins` says, because the app makes the call.

### Injecting scripts and styles

```csharp
webView.UserScripts.Add(new UserScript("document.documentElement.classList.add('in-app');"));
webView.UserScripts.Add(new UserScript
{
    File = "inject.js",                              // Resources/Raw/inject.js
    InjectionTime = UserScriptInjectionTime.DocumentStart,
    AllowedOrigins = ["https://*.example.com"],      // empty = every page
});
webView.UserStyleSheets.Add(".cookie-banner { display: none !important; }");
```

```xml
<awv:AdvancedWebView.UserScripts>
    <awv:UserScript File="inject.js" InjectionTime="DocumentStart" />
</awv:AdvancedWebView.UserScripts>
```

Scripts run on every page load; changes apply from the next load. `DocumentStart` scripts run before the page's own scripts, `DocumentEnd` scripts (the default) when the DOM is ready. `MainFrameOnly` (default `true`) keeps a script out of iframes. A script restricted with `AllowedOrigins` is wrapped in an `if` block: `var` and functions stay global, top-level `let`/`const` become block-scoped.

### Notes

- Android needs an Android System WebView that supports `WebMessageListener` (any reasonably recent version); otherwise the bridge is disabled and a warning is logged. On WebViews without document-start scripts the bridge is injected when the page finishes loading: page scripts should wait for `AdvancedWebView.ready` or the `advancedwebviewready` event.

## Sample app

[`samples/AdvancedWebView.Sample`](samples/AdvancedWebView.Sample) has two tabs:

- **Browser**: page title, URL and progress bar (`Title`, `CurrentUrl`, `Progress`, `IsLoading`), switches for the properties, cache mode and new-window pickers, `ClearCache()`, URL blocking in `Navigating`, `EvaluateJavaScriptAsync`, and a local test page for `alert()`, `confirm()`, a `target="_blank"` link, `window.open()`, `document.title` changes and `history.pushState`.
- **Bridge**: messages both ways, handlers with results and error codes, `InvokeJavaScriptAsync` (plain, Promise, error), a user style sheet, and a user script on example.com that shows the bridge blocked or allowed by `AllowedOrigins`.

```shell
dotnet build samples/AdvancedWebView.Sample -t:Run -f net10.0-android
```

## Tests

The bridge core and its JavaScript side are tested end to end without a device: the script runs in [Jint](https://github.com/sebastienros/jint) and talks to the real C# core.

```shell
dotnet test --project tests/AdvancedWebView.Tests/AdvancedWebView.Tests.csproj
```

## License

[MIT](LICENSE)
