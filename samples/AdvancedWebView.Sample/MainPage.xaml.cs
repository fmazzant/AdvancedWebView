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
		UrlEntry.Text = (WebView.Source as UrlWebViewSource)?.Url;
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

	void OnNavigated(object? sender, WebNavigatedEventArgs e)
	{
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
