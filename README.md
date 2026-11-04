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

## License

[MIT](LICENSE)
