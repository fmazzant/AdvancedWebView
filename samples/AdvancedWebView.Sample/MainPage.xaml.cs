using Mafesoft.Maui.Controls;

namespace AdvancedWebViewSample;

public partial class MainPage : ContentPage
{
	// Navigation to these hosts is cancelled in OnNavigating, to show how to block URLs.
	static readonly string[] BlockedHosts = ["example.com", "www.example.com"];

	readonly List<string> log = [];

	public MainPage()
	{
		InitializeComponent();

		CacheModePicker.ItemsSource = Enum.GetNames<WebViewCacheMode>();
		CacheModePicker.SelectedIndex = (int)WebView.CacheMode;
		NewWindowPicker.ItemsSource = Enum.GetNames<NewWindowAction>();
		NewWindowPicker.SelectedIndex = (int)WebView.NewWindowBehavior;
		ColorSchemePicker.ItemsSource = Enum.GetNames<WebViewColorScheme>();
		ColorSchemePicker.SelectedIndex = (int)WebView.PreferredColorScheme;
		DownloadPicker.ItemsSource = Enum.GetNames<DownloadBehavior>();
		DownloadPicker.SelectedIndex = (int)WebView.DownloadBehavior;
		PermissionPicker.ItemsSource = Enum.GetNames<PermissionDecision>();
		PermissionPicker.SelectedIndex = (int)WebView.PermissionRequestBehavior;
		UrlEntry.Text = (WebView.Source as UrlWebViewSource)?.Url;
	}

	void OnDownloadBehaviorChanged(object? sender, EventArgs e)
	{
		if (DownloadPicker.SelectedIndex >= 0)
			WebView.DownloadBehavior = (DownloadBehavior)DownloadPicker.SelectedIndex;
	}

	// With "Event", the app saves the file itself (here in the cache) and opens it.
	// With "SystemDownloadManager" (Android), returning without SaveToAsync lets the system download it.
	async void OnDownloadRequested(object? sender, DownloadRequestedEventArgs e)
	{
		Log($"Download: {e.SuggestedFileName} ({e.MimeType ?? "?"}, {e.ContentLength?.ToString() ?? "?"} bytes)");
		if (WebView.DownloadBehavior != DownloadBehavior.Event)
			return;

		var path = Path.Combine(FileSystem.CacheDirectory, e.SuggestedFileName);
		var lastStep = -1;
		var progress = new Progress<double>(p =>
		{
			var step = (int)(p * 4); // log at 0, 25, 50, 75 and 100 %
			if (step == lastStep)
				return;
			lastStep = step;
			Log($"Downloading {e.SuggestedFileName}: {p:P0}");
		});

		try
		{
			await e.SaveToAsync(path, progress);
			await Launcher.Default.OpenAsync(new OpenFileRequest(e.SuggestedFileName, new ReadOnlyFile(path, e.MimeType ?? "application/octet-stream")));
		}
		catch (Exception ex)
		{
			Log($"Download failed: {ex.Message}");
		}
	}

	void OnPermissionBehaviorChanged(object? sender, EventArgs e)
	{
		if (PermissionPicker.SelectedIndex >= 0)
			WebView.PermissionRequestBehavior = (PermissionDecision)PermissionPicker.SelectedIndex;
	}

	// getUserMedia and geolocation. With "Ask in app" on, the app asks the user, per site, with its own dialog:
	// the deferral keeps the page waiting until the dialog is closed.
	async void OnPermissionRequested(object? sender, PermissionRequestedEventArgs e)
	{
		var site = e.Origin?.Host ?? "This page";
		Log($"Permission ({e.Decision}): {e.Permissions} for {site}");

		if (!AskInAppSwitch.IsToggled)
			return;

		using var deferral = e.GetDeferral();
		var allow = await DisplayAlertAsync("Permission", $"{site} wants to use: {e.Permissions}", "Allow", "Deny");
		e.Decision = allow ? PermissionDecision.Grant : PermissionDecision.Deny;
	}

	// Android only. Not handled here: AdvancedWebView shows the system picker. Set e.Handled and call e.SetResult for your own.
	void OnFileChooserRequested(object? sender, FileChooserRequestedEventArgs e) =>
		Log($"File picker: accept [{string.Join(", ", e.AcceptTypes)}], multiple: {e.AllowsMultiple}, capture: {e.IsCaptureEnabled}");

	// Hide the app's bars while a video is fullscreen. On Android the back button leaves fullscreen.
	void OnFullscreenChanged(object? sender, FullscreenChangedEventArgs e)
	{
		Shell.SetNavBarIsVisible(this, !e.IsFullscreen);
		Shell.SetTabBarIsVisible(this, !e.IsFullscreen);
		Log(e.IsFullscreen ? "Fullscreen on" : "Fullscreen off");
	}

	// target="_blank" and window.open. e.Action starts as NewWindowBehavior (picker); change it here to decide per URL.
	void OnNewWindowRequested(object? sender, NewWindowRequestedEventArgs e) =>
		Log($"New window ({e.Action}, gesture: {e.IsUserGesture}): {e.Url}");

	void OnNewWindowBehaviorChanged(object? sender, EventArgs e)
	{
		if (NewWindowPicker.SelectedIndex >= 0)
			WebView.NewWindowBehavior = (NewWindowAction)NewWindowPicker.SelectedIndex;
	}

	// tel:, mailto:, intent:// ... and, with the switch on, sites outside microsoft.com. Set e.Cancel to handle them yourself.
	void OnExternalLinkOpening(object? sender, ExternalLinkOpeningEventArgs e) =>
		Log($"Opening outside the app ({e.Reason}): {e.Url}");

	// Shown instead of the platform's error page when the switch is on. Placeholders are HTML-encoded.
	const string ErrorPage = """
		<!DOCTYPE html>
		<html>
		<head>
		  <meta name="viewport" content="width=device-width, initial-scale=1">
		  <style>
		    body { font-family: -apple-system, Roboto, sans-serif; padding: 24px; text-align: center; }
		    button { padding: 12px 24px; font-size: 16px; }
		    code { word-break: break-all; }
		  </style>
		</head>
		<body>
		  <h1>Page not available</h1>
		  <p><b>{{error}}</b> {{statusCode}}</p>
		  <p>{{description}}</p>
		  <p><code>{{url}}</code></p>
		  <button onclick="AdvancedWebView.retry()">Retry</button>
		</body>
		</html>
		""";

	// httpbin.org echoes the request: the page shows the headers and the form the server received.
	void OnLoadWithHeadersClicked(object? sender, EventArgs e) =>
		WebView.LoadUrl("https://httpbin.org/headers", new Dictionary<string, string>
		{
			["Authorization"] = "Bearer sample-token",
			["X-Sample"] = "AdvancedWebView",
		});

	async void OnPostClicked(object? sender, EventArgs e)
	{
		try
		{
			await WebView.PostUrlAsync("https://httpbin.org/post", new FormUrlEncodedContent(new Dictionary<string, string>
			{
				["user"] = "mario",
				["note"] = "àèì & =",
			}));
		}
		catch (Exception ex)
		{
			Log($"{ex.GetType().Name}: {ex.Message}");
		}
	}

	// Cookie API. httpbin.org/cookies shows the cookies the server received.
	static readonly Uri CookieSite = new("https://httpbin.org/cookies");

	async void OnSetCookieClicked(object? sender, EventArgs e)
	{
		await WebView.CookieManager.SetCookieAsync(new System.Net.Cookie("advancedwebview", "sample-" + DateTime.Now.ToString("HHmmss"), "/", "httpbin.org") { Secure = true });
		Log("Cookie set for httpbin.org");
		WebView.Source = CookieSite.AbsoluteUri;
	}

	async void OnShowCookiesClicked(object? sender, EventArgs e)
	{
		if (!Uri.TryCreate(WebView.CurrentUrl, UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
		{
			Log("Open a web site first");
			return;
		}

		var cookies = await WebView.CookieManager.GetCookiesAsync(uri);
		Log($"{cookies.Count} cookie(s) for {uri.Host}: {string.Join(", ", cookies.Select(c => c.Name))}");
	}

	// The web session reused by HttpClient: the server sees the same cookies as the page.
	async void OnHttpClientClicked(object? sender, EventArgs e)
	{
		try
		{
			var container = new System.Net.CookieContainer();
			await WebView.CookieManager.CopyToAsync(container, CookieSite);
			using var client = new HttpClient(new HttpClientHandler { CookieContainer = container });
			var json = await client.GetStringAsync(CookieSite);
			Log($"HttpClient got: {json.Replace("\n", " ").Replace("  ", "")}");
		}
		catch (Exception ex)
		{
			Log($"HttpClient failed: {ex.Message}");
		}
	}

	async void OnClearDataClicked(object? sender, EventArgs e)
	{
		await WebView.ClearWebsiteDataAsync(WebsiteDataTypes.All);
		Log("Cookies, storage and cache cleared");
	}

	void OnErrorPageToggled(object? sender, ToggledEventArgs e) =>
		WebView.ErrorPageHtml = e.Value ? ErrorPage : null;

	void OnNavigationFailed(object? sender, NavigationFailedEventArgs e) =>
		Log($"Failed ({e.Error}{(e.HttpStatusCode is { } code ? $" {code}" : "")}): {e.Url} - {e.Description}");

	// Rejected (the default): a real app should accept a certificate only for its own development server.
	void OnServerCertificateError(object? sender, ServerCertificateErrorEventArgs e) =>
		Log($"Certificate rejected: {e.Url} - {e.Description}");

	void OnInternalHostsToggled(object? sender, ToggledEventArgs e)
	{
		WebView.InternalHosts = e.Value ? ["microsoft.com", "*.microsoft.com"] : [];
		Log(e.Value ? "Only microsoft.com stays inside" : "Every site stays inside");
	}

	void OnNavigating(object? sender, WebNavigatingEventArgs e)
	{
		if (Uri.TryCreate(e.Url, UriKind.Absolute, out var uri) && BlockedHosts.Contains(uri.Host))
		{
			e.Cancel = true;
			Log($"Blocked: {e.Url}");
			return;
		}

		Log($"Navigating: {e.Url}");
	}

	void OnPullToRefresh(object? sender, EventArgs e) => WebView.Reload();

	void OnColorSchemeChanged(object? sender, EventArgs e)
	{
		if (ColorSchemePicker.SelectedIndex >= 0)
			WebView.PreferredColorScheme = (WebViewColorScheme)ColorSchemePicker.SelectedIndex;
	}

	void OnNavigated(object? sender, WebNavigatedEventArgs e)
	{
		Refresh.IsRefreshing = false; // the reload started by pull to refresh is done
		if (e.Url.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			UrlEntry.Text = e.Url;

		Log($"Navigated ({e.Result}): {e.Url}");
	}

	void OnGoClicked(object? sender, EventArgs e)
	{
		var url = UrlEntry.Text?.Trim();
		if (string.IsNullOrEmpty(url))
			return;

		if (!url.Contains("://"))
			url = "https://" + url;

		WebView.Source = url;
	}

	void OnBackClicked(object? sender, EventArgs e)
	{
		if (WebView.CanGoBack)
			WebView.GoBack();
	}

	void OnForwardClicked(object? sender, EventArgs e)
	{
		if (WebView.CanGoForward)
			WebView.GoForward();
	}

	void OnReloadClicked(object? sender, EventArgs e) => WebView.Reload();

	void OnCacheModeChanged(object? sender, EventArgs e)
	{
		if (CacheModePicker.SelectedIndex >= 0)
			WebView.CacheMode = (WebViewCacheMode)CacheModePicker.SelectedIndex;
	}

	// ExportPdfAsync: saves the page as an A4 PDF in the cache folder and opens it with the system viewer.
	async void OnExportPdfClicked(object? sender, EventArgs e)
	{
		try
		{
			await using var pdf = await WebView.ExportPdfAsync(new WebPdfOptions { PaperSize = WebPaperSize.A4, MarginMillimeters = 12 });
			var path = Path.Combine(FileSystem.CacheDirectory, "page.pdf");
			await using (var file = File.Create(path))
				await pdf.CopyToAsync(file);

			Log($"PDF: {new FileInfo(path).Length / 1024} KB, {path}");
			await Launcher.Default.OpenAsync(new OpenFileRequest("PDF", new ReadOnlyFile(path, "application/pdf")));
		}
		catch (Exception ex)
		{
			Log($"PDF failed: {ex.Message}");
		}
	}

	async void OnPrintClicked(object? sender, EventArgs e)
	{
		try
		{
			await WebView.PrintAsync();
			Log("Print dialog shown");
		}
		catch (Exception ex)
		{
			Log($"Print failed: {ex.Message}");
		}
	}

	void OnCaptureClicked(object? sender, EventArgs e) => CaptureAsync(WebCaptureArea.Viewport);

	void OnCaptureFullPageClicked(object? sender, EventArgs e) => CaptureAsync(WebCaptureArea.FullPage);

	// CaptureAsync: saves a PNG in the cache folder and shows it.
	async void CaptureAsync(WebCaptureArea area)
	{
		try
		{
			var screenshot = await WebView.CaptureAsync(area);
			var path = Path.Combine(FileSystem.CacheDirectory, $"capture-{area}.png");
			await using (var file = File.Create(path))
				await screenshot.CopyToAsync(file);

			Log($"Captured {area}: {screenshot.Width} x {screenshot.Height} px, {new FileInfo(path).Length / 1024} KB, {path}");
			var image = new ScrollView { Content = new Image { Source = ImageSource.FromFile(path), Aspect = Aspect.AspectFit } };
			Grid.SetRow(image, 1);
			await Navigation.PushModalAsync(new ContentPage
			{
				Title = area.ToString(),
				Content = new Grid
				{
					RowDefinitions = [new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star)],
					Children =
					{
						new Button { Text = "Close", Command = new Command(() => Navigation.PopModalAsync()) },
						image,
					},
				},
			});
		}
		catch (Exception ex)
		{
			Log($"Capture failed: {ex.Message}");
		}
	}

	void OnClearCacheClicked(object? sender, EventArgs e)
	{
		WebView.ClearCache();
		Log("Cache cleared");
	}

	async void OnTestPageClicked(object? sender, EventArgs e)
	{
		using var stream = await FileSystem.OpenAppPackageFileAsync("testpage.html");
		using var reader = new StreamReader(stream);
		WebView.Source = new HtmlWebViewSource { Html = await reader.ReadToEndAsync() };
	}

	async void OnRunJavaScriptClicked(object? sender, EventArgs e)
	{
		var title = await WebView.EvaluateJavaScriptAsync("document.title");
		Log($"document.title = {title ?? "(null, is JavaScript disabled?)"}");
	}

	void Log(string message)
	{
		log.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
		if (log.Count > 3)
			log.RemoveAt(log.Count - 1);
		LogLabel.Text = string.Join(Environment.NewLine, log);
	}
}
