# Changelog

All notable changes to `Mafesoft.Maui.AdvancedWebView` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.5.0] - Unreleased

### Changed

- **`LoadUrl` / `PostUrlAsync` headers go only to the origin of the URL** (scheme, host and port), on every platform ([#60](https://github.com/fmazzant/AdvancedWebView/issues/60)). Before, a redirect to another site received them too, `Authorization` included (Android, Windows; iOS removed only `Authorization`). Redirects to the same origin keep them. On Android this needs Android 7 (API 24) or later.
- **`Reload()` sends the headers again** while the page loaded by `LoadUrl` is on their origin, on every platform. Before, Windows dropped them on the first page of the web view and Android after a redirect. The request is sent again and the history does not change.
- **Windows:** back and forward navigation no longer send the headers again: the library now adds them to the requests itself instead of giving them to WebView2.

## [1.4.0] - Unreleased

The platforms release.

### Added

- **Mac Catalyst:** the library now targets `net10.0-maccatalyst` (Mac Catalyst 15 or later). It shares the iOS `WKWebView` implementation, so the iOS features work on the Mac. The README lists the App Sandbox entitlements the web view needs, and the sample app runs on the Mac.
- **Windows (WebView2):** the library now targets `net10.0-windows10.0.19041.0` (Windows 10 1809 or later). The bridge (iframes included), user scripts and styles, page state, new windows, external links, load failures and the error page, certificate errors, `LoadUrl` with headers and `PostUrlAsync`, downloads, permissions, fullscreen, `IsInspectable` (Edge DevTools), display options, cookies and `ClearWebsiteDataAsync` work on Windows. The README lists the differences. The Windows target builds on Windows only.
- **Screenshots:** `CaptureAsync()` and `CaptureAsync(WebCaptureArea)` capture the part of the page on screen (`Viewport`) or the whole page (`FullPage`) as MAUI's `IScreenshotResult` (PNG or JPEG), on every platform. Android needs `AdvancedWebView.EnableFullPageCapture()` for full pages.
- **PDF and printing:** `ExportPdfAsync(WebPdfOptions)` exports the page as a PDF split into pages (paper size, orientation, margin), without UI. `PrintAsync(jobName)` opens the system print dialog. Both work on every platform.

### Fixed

- Sample app: the script of the local test page stopped at its first line in Chromium web views (a global `status` variable is `window.status`), so its buttons did nothing.
- Docs: `LoadUrl` and `PostUrlAsync` said the headers go with the first request only. On Windows they are also sent on redirects (to other sites too), `Reload()` and back navigation, and `Reload()` drops them on the first page the web view loaded. The README and the XML docs now say so ([#60](https://github.com/fmazzant/AdvancedWebView/issues/60)).

## [1.3.0] - Unreleased

The platform capabilities release.

### Added

- **`IsInspectable`:** debug the page with Chrome DevTools (Android) or the Safari Web Inspector (iOS 16.4 or later).
- **Fullscreen:** `FullscreenEnabled`, `IsFullscreen` and the `FullscreenChanged` event. Android: the back button leaves fullscreen. iOS 16.4 or later: the HTML fullscreen API (`element.requestFullscreen`) is enabled.
- **File upload (Android):** `<input type="file" multiple>` returns every chosen file, and `capture` opens the camera (MAUI `FilePicker` and `MediaPicker`). `FileChooserRequested` lets the app show its own picker.
- **Web permissions:** `PermissionRequestBehavior` (`AskUser`, `Grant`, `Deny`) and the `PermissionRequested` event (`Origin`, `Permissions`, `Decision`, `GetDeferral()`) for the camera, the microphone and (Android) the location. Before, Android always refused them.
- **Downloads:** `DownloadBehavior` (`Event`, `SystemDownloadManager` on Android, `Ignore`) and the `DownloadRequested` event (`Url`, `SuggestedFileName`, `MimeType`, `ContentLength`, `Cancel`, `SaveToAsync(path, progress)`). Files are downloaded with the web view's cookies and user agent; `blob:` and `data:` URLs are supported.
- **Display options:** `ZoomEnabled`, `TextZoom`, `IsTransparent`, `TextSelectionEnabled`, `LinkPreviewEnabled` (iOS), `PreferredColorScheme` (iOS) and `AlgorithmicDarkening` (Android). Pull to refresh is documented with MAUI's `RefreshView`.
- **Cookies and website data:** `CookieManager` (`GetCookiesAsync`, `SetCookieAsync`, `DeleteCookiesAsync`, `ClearAllAsync`, `CopyToAsync` to share the web session with `HttpClient`) and `ClearWebsiteDataAsync(WebsiteDataTypes)`.

### Changed

- Android: pinch zoom is now on by default (`ZoomEnabled = true`), without the on-screen zoom buttons. Set `ZoomEnabled = false` for the previous behavior.

## [1.2.0] - Unreleased

The navigation release.

### Added

- **Page state as bindable properties:** `Progress` (0 to 1), `IsLoading`, `Title` and `CurrentUrl`, read-only. `Title` and `CurrentUrl` also follow `document.title` changes and `history.pushState`.
- **New windows:** `NewWindowBehavior` (`OpenInSameView`, `OpenInBrowser`, `Cancel`) and the `NewWindowRequested` event (`Url`, `IsUserGesture`, `Action`) for links with `target="_blank"` and `window.open`. Before, Android loaded them in the same view and iOS ignored `window.open`.
- **External links:** `ExternalSchemes` (default `tel`, `mailto`, `sms`, `geo`, `maps`, `whatsapp`, `intent`, `market`, `itms-apps`) are handed to the system instead of showing an error page; `InternalHosts` sends other web sites to the system browser; the `ExternalLinkOpening` event (`Url`, `Reason`, `Cancel`) lets the app handle them. Android `intent://` URLs are supported, with `browser_fallback_url`.
- **Load failures:** the `NavigationFailed` event (`Url`, `Error`, `HttpStatusCode`, `Description`, `ShowErrorPage`) with a normalized `WebNavigationError` (`NoInternet`, `Timeout`, `HostNotFound`, `SslError`, `HttpError`, `Cancelled`, `Unknown`), for network errors and HTTP 4xx/5xx of the top-level page.
- **Custom error page:** `ErrorPageHtml` with `{{url}}`, `{{description}}`, `{{error}}` and `{{statusCode}}` placeholders, and `AdvancedWebView.retry()` in the page to load the failed URL again.
- **Certificate errors:** the `ServerCertificateError` event. Invalid certificates are rejected unless the app sets `Proceed = true`.
- **Headers and POST:** `LoadUrl(url, headers)` and `PostUrlAsync(url, content, headers)`. Android posts form-urlencoded bodies only, without extra headers.

### Changed

- The web view now uses subclasses of MAUI's platform delegates: `MauiWebViewClient` and `MauiWebChromeClient` on Android, `MauiWebViewNavigationDelegate` and `MauiWebViewUIDelegate` on iOS. MAUI behaviour (navigation events, alerts, file pickers, fullscreen video) is unchanged.

### Fixed

- CI runs the test project again.

## [1.1.0] - Unreleased

The JavaScript bridge release: the page and the app can talk to each other, with local HTML and with the remote sites you allow.

### Added

- **Messages in both directions.** The page calls `AdvancedWebView.postMessage(data)` and the app receives `MessageReceived` (`Message`, `Json`, `GetJson<T>()`, `Origin`, `IsMainFrame`). The app calls `PostMessageAsync(...)` and the page receives it with `AdvancedWebView.addEventListener("message", ...)`.
- **C# handlers the page awaits as Promises.** `RegisterHandler(...)`, `RegisterRawHandler(...)` and `UnregisterHandler(...)` in C#; `await AdvancedWebView.invoke(name, args)` in JavaScript.
  - Throw `BridgeException(code, message)` to reject with your own `error.code`. Built-in codes: `MethodNotFound`, `InvalidArguments`, `Timeout`, `HandlerError`, `OriginNotAllowed`.
  - Calls still running when the page navigates away are cancelled through the handler's `CancellationToken`.
  - `Bridge.InvokeTimeout` (default 30 s) and `Bridge.IncludeExceptionDetails` (default off).
- **Typed calls to page functions:** `InvokeJavaScriptAsync(...)`, `InvokeJavaScriptAsync<T>(...)` and `InvokeJavaScriptRawAsync(...)`. Arguments are passed as JSON data, Promises are awaited, and JavaScript errors raise `JavaScriptException` with the JavaScript stack.
- **Injected scripts and styles:** `UserScripts` (inline or from `Resources/Raw`, at document start or end, main frame only or all frames, optionally limited to some origins) and `UserStyleSheets`.
- **Per-origin security:** `Bridge.AllowedOrigins` (`https://example.com`, `https://example.com:8443`, `https://*.example.com`, `*`) and `Bridge.AllowSubframes`.
- `AdvancedWebView.ready` (Promise) and the `advancedwebviewready` event in the page.
- Overloads that take `JsonTypeInfo<T>` for trimming and Native AOT.
- **Bridge** tab in the sample app.
- Test project `tests/AdvancedWebView.Tests`: 76 end-to-end tests that run the bridge script in Jint against the real C# core, also in CI.

### Security

- **The bridge is closed to remote sites by default.** Local content (`HtmlWebViewSource`, files packaged with the app) can always use it; remote origins must be listed in `Bridge.AllowedOrigins`. Messages from other origins are dropped with a warning in the log, and their `invoke` calls fail with `OriginNotAllowed`.
- Iframes cannot use the bridge unless `Bridge.AllowSubframes` is `true`; replies go back to the frame that made the call.

### Changed

- Android: new dependency `Xamarin.AndroidX.WebKit` 1.14.0.1, aligned with the AndroidX versions used by .NET MAUI 10. The bridge needs an Android System WebView that supports `WebMessageListener`; on older WebViews it is disabled and a warning is logged.
- CI runs the test project; super-linter removed. `global.json` opts into Microsoft.Testing.Platform for `dotnet test`.

## [1.0.0] - 2026-11-04

First release for .NET 10.

### Added

- `AdvancedWebView` control for .NET MAUI 10 on Android (API 21+) and iOS (15+), built on the MAUI `WebView` handler, so `Navigating`, `Navigated`, `Cookies`, `UserAgent` and JavaScript alerts keep working.
- Registration with `builder.UseAdvancedWebView()`.
- Properties: `JavaScriptEnabled`, `JavaScriptCanOpenWindowsAutomatically`, `ScrollBouncesEnabled`, `MediaPlaybackRequiresUserGesture`, `AllowsInlineMediaPlayback` (iOS), `AllowMixedContent` (Android), `AllowFileAccess` (Android), `CacheMode` (Android).
- `ClearCache()` on Android and iOS.
- NuGet package `Mafesoft.Maui.AdvancedWebView` with README, symbols and SourceLink.
- Sample app in `samples/AdvancedWebView.Sample`.
- CI: build and pack on macOS; publishing to NuGet when a `v*.*.*` tag is pushed.

### Security

- Mixed content and file access are off by default on Android.

[1.1.0]: https://github.com/fmazzant/AdvancedWebView/compare/v1.0.0...v1.1.0
[1.0.0]: https://github.com/fmazzant/AdvancedWebView/releases/tag/v1.0.0
