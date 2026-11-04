# Changelog

All notable changes to `Mafesoft.Maui.AdvancedWebView` are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [1.3.0] - Unreleased

The platform capabilities release.

### Added

- **`IsInspectable`:** debug the page with Chrome DevTools (Android) or the Safari Web Inspector (iOS 16.4 or later).

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
