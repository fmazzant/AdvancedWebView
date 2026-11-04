using Mafesoft.Maui.Controls;

namespace AdvancedWebViewSample;

/// <summary>Shows the JavaScript bridge: messages, handlers, page functions, user scripts and allowed origins.</summary>
public partial class BridgePage : ContentPage
{
	const string HighlightCss = "button { outline: 3px solid #512BD4; outline-offset: 2px; }";

	// Runs on example.com (DocumentEnd) and calls the app. With example.com not allowed, the call fails with OriginNotAllowed.
	const string ExampleProbe = """
		(function () {
		  var banner = document.createElement("div");
		  banner.style.cssText = "position:fixed;left:0;right:0;bottom:0;padding:16px;font:16px sans-serif;background:#512BD4;color:#fff;z-index:99999";
		  banner.textContent = "Calling the app...";
		  document.body.appendChild(banner);
		  AdvancedWebView.invoke("getTime").then(
		    function (t) { banner.textContent = "Bridge allowed: app time is " + t; },
		    function (e) { banner.textContent = "Bridge blocked: " + e.code; });
		})();
		""";

	readonly List<string> log = [];
	int sent;

	public sealed record DivideRequest(double A, double B);

	public BridgePage()
	{
		InitializeComponent();

		// Handlers the page calls with AdvancedWebView.invoke(...)
		WebView.RegisterHandler("getTime", _ => Task.FromResult(DateTime.Now.ToString("T")));
		WebView.RegisterHandler<DivideRequest, double>("divide", (req, _) =>
		{
			if (req is null || req.B == 0)
				throw new BridgeException("DivideByZero", "The divisor cannot be zero.");
			return Task.FromResult(req.A / req.B);
		});

		WebView.UserScripts.Add(new UserScript(ExampleProbe) { AllowedOrigins = ["https://example.com"] });

		LoadDemoPage();
	}

	void OnMessageReceived(object? sender, WebMessageReceivedEventArgs e) =>
		Log($"page → app: {e.Message} (origin: {e.Origin?.ToString() ?? "local"})");

	async void OnSendClicked(object? sender, EventArgs e)
	{
		await WebView.PostMessageAsync(new { type = "cart-updated", count = ++sent });
		Log($"app → page: cart-updated #{sent}");
	}

	async void OnAddClicked(object? sender, EventArgs e) =>
		await Try(async () => Log($"demo.add(2, 3) = {await WebView.InvokeJavaScriptAsync<int>("demo.add", 2, 3)}"));

	async void OnGreetClicked(object? sender, EventArgs e) =>
		await Try(async () => Log(await WebView.InvokeJavaScriptAsync<string>("demo.greetLater", "MAUI") ?? "(null)"));

	async void OnFailClicked(object? sender, EventArgs e) =>
		await Try(() => WebView.InvokeJavaScriptAsync("demo.fail"));

	void OnToggleCssClicked(object? sender, EventArgs e)
	{
		if (!WebView.UserStyleSheets.Remove(HighlightCss))
			WebView.UserStyleSheets.Add(HighlightCss);
		WebView.Reload();
	}

	void OnDemoPageClicked(object? sender, EventArgs e) => LoadDemoPage();

	void OnExampleClicked(object? sender, EventArgs e) => WebView.Source = "https://example.com";

	void OnAllowExampleToggled(object? sender, ToggledEventArgs e)
	{
		if (e.Value)
			WebView.Bridge.AllowedOrigins.Add("https://example.com");
		else
			WebView.Bridge.AllowedOrigins.Remove("https://example.com");
		Log(e.Value ? "example.com allowed" : "example.com blocked");
	}

	async void LoadDemoPage()
	{
		using var stream = await FileSystem.OpenAppPackageFileAsync("bridge.html");
		using var reader = new StreamReader(stream);
		WebView.Source = new HtmlWebViewSource { Html = await reader.ReadToEndAsync() };
	}

	async Task Try(Func<Task> action)
	{
		try
		{
			await action();
		}
		catch (JavaScriptException ex)
		{
			Log($"JavaScriptException: {ex.Message}");
		}
		catch (Exception ex)
		{
			Log($"{ex.GetType().Name}: {ex.Message}");
		}
	}

	void Log(string message)
	{
		log.Insert(0, $"{DateTime.Now:HH:mm:ss} {message}");
		if (log.Count > 5)
			log.RemoveAt(log.Count - 1);
		LogLabel.Text = string.Join(Environment.NewLine, log);
	}
}
