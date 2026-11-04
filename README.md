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
| `IsInspectable` | `false` | ✔ | ✔ | Debug with Chrome DevTools / Safari Web Inspector; iOS 16.4 or later. See [Debugging the page](#debugging-the-page) |
| `FullscreenEnabled` | `true` | ✔ | ✔ | Fullscreen video and `element.requestFullscreen`. See [Fullscreen](#fullscreen) |
| `PermissionRequestBehavior` | `AskUser` | ✔ | ✔ | Camera, microphone and location asked by the page: `AskUser`, `Grant`, `Deny`. See [Permissions](#permissions) |
| `DownloadBehavior` | `Event` | ✔ | ✔ | `Event`, `SystemDownloadManager` (Android), `Ignore`. See [Downloads](#downloads) |
| `ZoomEnabled` | `true` | ✔ | ✔ | Pinch zoom. Android: built-in zoom without the +/- buttons. iOS: `user-scalable=no` when off |
| `TextZoom` | `100` | ✔ | ✔ | Text size in percent (10–500). iOS scales the whole page (`pageZoom`, iOS 14 or later) |
| `IsTransparent` | `false` | ✔ | ✔ | The app shows through where the page has no background |
| `TextSelectionEnabled` | `true` | ✔ | ✔ | Off: no text selection (form fields stay editable); iOS also loses the long-press menu |
| `LinkPreviewEnabled` | `true` | | ✔ | Link preview on long press |
| `PreferredColorScheme` | `Auto` | | ✔ | `prefers-color-scheme`: `Auto`, `Light`, `Dark`. Android follows the app's theme |
| `AlgorithmicDarkening` | `false` | ✔ | | Darkens pages without a dark theme while the app's theme is dark |

### Pull to refresh

Wrap the web view in a MAUI `RefreshView`. MAUI starts the refresh only when the page is scrolled to the top:

```xml
<RefreshView x:Name="Refresh" Refreshing="OnRefreshing">
    <awv:AdvancedWebView x:Name="WebView" Navigated="OnNavigated" />
</RefreshView>
```

```csharp
void OnRefreshing(object sender, EventArgs e) => WebView.Reload();
void OnNavigated(object sender, WebNavigatedEventArgs e) => Refresh.IsRefreshing = false;
```

### File upload

`<input type="file">` works on both platforms, with `accept`, `multiple` and `capture`:

- **Android:** MAUI's picker handles a single file. For `multiple` and `capture`, AdvancedWebView uses MAUI `FilePicker` and `MediaPicker` instead. MAUI's own picker returns only one file and ignores `capture`. Handle `FileChooserRequested` to show your own picker:

  ```csharp
  webView.FileChooserRequested += async (s, e) =>
  {
      // e.AcceptTypes, e.AllowsMultiple, e.IsCaptureEnabled
      e.Handled = true; // before the first await
      var photo = await MediaPicker.Default.CapturePhotoAsync();
      e.SetResult(photo is null ? null : [photo.FullPath]); // always call it, null when cancelled
  };
  ```

- **iOS:** WKWebView shows the system picker (photo library, camera, files), which already supports `accept`, `multiple` and `capture`. `FileChooserRequested` is not raised.

Manifest and Info.plist entries for the camera and the photo library:

| Platform | Entries |
|---|---|
| Android | `<uses-permission android:name="android.permission.CAMERA" />`, and `<queries>` with the `android.media.action.IMAGE_CAPTURE` and `VIDEO_CAPTURE` intents (needed by `MediaPicker` on Android 11 or later) |
| iOS | `NSCameraUsageDescription`, `NSPhotoLibraryUsageDescription`, `NSMicrophoneUsageDescription` (for videos) |

### Downloads

A download starts in several ways:

- a link to a file the web view cannot show, for example a ZIP;
- an answer with `Content-Disposition: attachment`;
- a link with the `download` attribute, also to `blob:` and `data:` URLs.

```csharp
webView.DownloadBehavior = DownloadBehavior.Event; // default; SystemDownloadManager (Android), Ignore

webView.DownloadRequested += async (s, e) =>
{
    // e.Url, e.SuggestedFileName, e.MimeType, e.ContentLength
    var path = Path.Combine(FileSystem.CacheDirectory, e.SuggestedFileName);
    await e.SaveToAsync(path, new Progress<double>(p => DownloadProgress = p));
};
```

- `SaveToAsync` downloads with the web view's cookies (HttpOnly ones too) and user agent, so files behind a login work.
  - For `blob:` URLs it reads the blob from the page through the bridge, so the page must not revoke the blob URL right after the click.
  - For `data:` URLs it decodes the data.
  - If the download fails or is cancelled, the partial file is deleted.
- `SystemDownloadManager` (Android): when the handler does not call `SaveToAsync` (or set `e.Cancel`), Android's download manager saves the file to the public Downloads folder and shows a notification. It handles http and https only. Android 9 and earlier need `WRITE_EXTERNAL_STORAGE`. On iOS it behaves like `Event`.
- Before, Android ignored downloads, and on iOS a file WebKit could not show made the navigation fail. A PDF without `Content-Disposition: attachment` is still shown in the page on iOS, as in Safari.

### Permissions

Pages ask for the camera and the microphone with `getUserMedia`, and for the location with `navigator.geolocation`:

```csharp
webView.PermissionRequestBehavior = PermissionDecision.AskUser; // default; Grant, Deny

webView.PermissionRequested += async (s, e) =>
{
    // e.Origin, e.Permissions (Camera | Microphone | Geolocation), e.Decision
    using var deferral = e.GetDeferral(); // before the first await: the page waits for the answer
    e.Decision = await page.DisplayAlertAsync("Camera", $"{e.Origin?.Host} wants to use the camera", "Allow", "Deny")
        ? PermissionDecision.Grant
        : PermissionDecision.Deny;
};
```

| Decision | Android | iOS |
|---|---|---|
| `AskUser` | Like `Grant`: Android has no per-site dialog. Handle `PermissionRequested` to show your own | WebKit asks the user for the site |
| `Grant` | The page gets what the app has. The OS permission dialog appears the first time | The page gets it without WebKit's dialog. The OS dialog appears the first time |
| `Deny` | The page's request fails | The page's request fails |

- Before, on Android, these requests were always refused.
- iOS has no hook for the location: WebKit asks the user for each site, and `PermissionRequested` is raised only for the camera and the microphone.
- `getUserMedia` needs a secure context (`https://`).

Manifest and Info.plist entries:

| Platform | Entries |
|---|---|
| Android | `CAMERA`, `RECORD_AUDIO`, `MODIFY_AUDIO_SETTINGS`, `ACCESS_COARSE_LOCATION`, `ACCESS_FINE_LOCATION` |
| iOS | `NSCameraUsageDescription`, `NSMicrophoneUsageDescription`, `NSLocationWhenInUseUsageDescription` |

### Fullscreen

```csharp
webView.FullscreenChanged += (s, e) =>
{
    Shell.SetNavBarIsVisible(this, !e.IsFullscreen);
    Shell.SetTabBarIsVisible(this, !e.IsFullscreen);
};
```

- `IsFullscreen` (read-only, bindable) and `FullscreenChanged` report fullscreen of the page.
- **Android:** a video player's fullscreen button and `element.requestFullscreen()` show the content over the whole app and hide the system bars (MAUI does this). AdvancedWebView adds the back button, which leaves fullscreen, and leaves fullscreen when the view goes away. With `FullscreenEnabled = false` the request is refused.
- **iOS:** `FullscreenEnabled` turns on the HTML fullscreen API (`element.requestFullscreen()`, iOS 16.4 or later). It is read when the view is created. Videos also have the native player's fullscreen. WebKit does not report it, so `FullscreenChanged` is raised only for element fullscreen (iOS 16 or later).

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

### External links

The web view cannot load `tel:`, `mailto:` and other app links, and shows an error page for them. AdvancedWebView hands them to the system instead. It can also send web sites you do not own to the system browser:

```csharp
webView.ExternalSchemes = ["tel", "mailto", "sms", "geo", "maps", "whatsapp", "intent", "market", "itms-apps"]; // default
webView.InternalHosts = ["example.com", "*.example.com"]; // default: empty, every site stays inside

webView.ExternalLinkOpening += (s, e) =>
{
    // e.Url, e.Reason (Scheme or Host)
    e.Cancel = true; // the system does not open it, and the web view does not load it: handle it yourself
};
```

- `Navigating` is raised first. A URL cancelled there is not opened outside either.
- `InternalHosts`: `"example.com"` matches that host only, `"*.example.com"` its subdomains only. Only the top-level page is checked, so iframes from other hosts still load. Links opened as new windows in the same view follow the same rules.
- Android `intent://` URLs open the app they name, limited to activities that accept browser links (as Chrome does). When no app handles them, the web view loads their `S.browser_fallback_url`.
- Links leave the app through MAUI `Launcher.OpenAsync`. On iOS a scheme like `whatsapp:` opens only if the app is installed. You do not need `LSApplicationQueriesSchemes` or Android `<queries>` entries for this.

### Load failures and error page

```csharp
webView.NavigationFailed += (s, e) =>
{
    // e.Url, e.Error, e.HttpStatusCode, e.Description
    if (e.Error == WebNavigationError.HttpError && e.HttpStatusCode == 404)
        e.ShowErrorPage = false; // keep the server's own 404 page
};

webView.ErrorPageHtml = """
    <h1>Page not available</h1>
    <p>{{description}}</p>
    <button onclick="AdvancedWebView.retry()">Retry</button>
    """;

webView.ServerCertificateError += (s, e) => e.Proceed = false; // default: reject
```

| `WebNavigationError` | Android | iOS (`NSURLErrorDomain`) |
|---|---|---|
| `NoInternet` | `net::ERR_INTERNET_DISCONNECTED` | `-1009`, `-1020`, `-1005` |
| `Timeout` | `ERROR_TIMEOUT` | `-1001` |
| `HostNotFound` | `ERROR_HOST_LOOKUP`, `ERROR_CONNECT` | `-1003`, `-1004`, `-1006` |
| `SslError` | `ERROR_FAILED_SSL_HANDSHAKE`, rejected certificate | `-1200` … `-1206` |
| `HttpError` | HTTP status ≥ 400 | HTTP status ≥ 400 |
| `Cancelled` | `net::ERR_ABORTED` | `-999`, WebKit `102` |
| `Unknown` | anything else | anything else |

- Only the top-level page is reported. Failures of iframes and of resources such as images are not.
- `ErrorPageHtml` placeholders: `{{url}}`, `{{description}}`, `{{error}}`, `{{statusCode}}`. They are HTML-encoded. The page is shown for every error except `Cancelled`. Set `e.ShowErrorPage = false` to keep the current page.
- `AdvancedWebView.retry()` loads the failed URL again as a normal navigation, so it raises `Navigating`. It needs JavaScript and the bridge. The error page is local content, so it can use the bridge whatever `Bridge.AllowedOrigins` says.
- Invalid certificates are rejected unless a `ServerCertificateError` handler sets `e.Proceed = true`. Do this only during development, for example for a local server with a self-signed certificate. Accepting every certificate exposes the connection to interception, and Google Play rejects apps that do it.

### Platform delegates

On Android, AdvancedWebView sets subclasses of MAUI's `MauiWebViewClient` and `MauiWebChromeClient`; on iOS, subclasses of `MauiWebViewNavigationDelegate` and `MauiWebViewUIDelegate`. If your app replaces them, page state, new windows, external links and error handling stop working.

## Methods

| Method | Description |
|---|---|
| `ClearCache()` | Clears the HTTP cache (memory and disk). Cookies and local storage are kept. |
| `LoadUrl(url, headers)` | Loads a page with extra HTTP headers. |
| `PostUrlAsync(url, content, headers)` | Loads a page with a POST request. |

### Headers and POST

```csharp
webView.LoadUrl("https://example.com/account", new Dictionary<string, string>
{
    ["Authorization"] = $"Bearer {token}",
});

await webView.PostUrlAsync("https://example.com/login", new FormUrlEncodedContent(new Dictionary<string, string>
{
    ["user"] = "mario",
}));
```

- `Navigating` and `Navigated` are raised as for a link, and the external-link rules apply.
- The headers go with this request only. They are not sent with the page's resources (images, scripts, API calls) or with later navigations, such as links the user taps. For an authenticated session, prefer a cookie (`Cookies`).
- Android only posts `application/x-www-form-urlencoded` bodies, and cannot add headers to a POST. `PostUrlAsync` throws `NotSupportedException` otherwise. iOS supports any content type and headers.
- Call these methods after the control is loaded (for example in its `Loaded` event). Before that, they throw `InvalidOperationException`.

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

## Debugging the page

Enable `IsInspectable`, in debug builds only:

```csharp
#if DEBUG
webView.IsInspectable = true;
#endif
```

- **Android:** connect the device with USB debugging on (or start the emulator), open `chrome://inspect` in Chrome on your computer, and choose the page under the app's name. The setting is global to the app: debugging stays on for every web view while at least one `AdvancedWebView` with `IsInspectable` is on screen.
- **iOS 16.4 or later:** on the device, turn on *Settings > Safari > Advanced > Web Inspector*. On the Mac, turn on *Safari > Settings > Advanced > Show features for web developers*, connect the device (or start the simulator), and choose the page in Safari's *Develop* menu. On earlier iOS versions, only debug builds can be inspected, and `IsInspectable` has no effect.

## Sample app

[`samples/AdvancedWebView.Sample`](samples/AdvancedWebView.Sample) has two tabs:

- **Browser**: page title, URL and progress bar (`Title`, `CurrentUrl`, `Progress`, `IsLoading`), pull to refresh (`RefreshView`), switches for the properties (including `IsInspectable`, `FullscreenEnabled` and the display options), cache mode and new-window pickers, a downloads picker, switches for `InternalHosts` and `ErrorPageHtml`, `ClearCache()`, `LoadUrl` with headers and `PostUrlAsync` (against httpbin.org, which echoes the request), URL blocking in `Navigating`, `EvaluateJavaScriptAsync`, and a local test page for `alert()`, `confirm()`, downloads (PDF, ZIP, `data:` and `blob:`), file upload (single, multiple, PDF only, camera), permissions (`getUserMedia`, geolocation; picker and "Ask in app" switch), fullscreen (video and `requestFullscreen`), a `target="_blank"` link, `window.open()`, load failures (HTTP 404 and 500, unknown host, expired certificate, timeout), external links (`tel:`, `mailto:`, `geo:`, `intent://`), `document.title` changes and `history.pushState`.
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
