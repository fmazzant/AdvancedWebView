using Mafesoft.Maui.Controls;

namespace HeaderRedirects;

/// <summary>
/// Runs the scenarios of issue #60 once, on start, against the echo server, then posts /done.
/// The echo server records which requests carried the headers; this page only drives the web view and
/// logs Navigating/Navigated (to see whether a redirect is visible to the app, i.e. whether it could be intercepted).
/// </summary>
public class ScenarioPage : ContentPage
{
	// Android emulator: the host is 10.0.2.2. iOS simulator: the host's loopback.
	static readonly string Host = DeviceInfo.Platform == DevicePlatform.Android ? "10.0.2.2" : "127.0.0.1";
	static readonly string A = $"http://{Host}:5100";
	static readonly string B = $"http://{Host}:5101";

	static readonly Dictionary<string, string> Headers = new()
	{
		["Authorization"] = "Bearer proto-secret",
		["X-Proto-Header"] = "proto",
	};

	readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
	AdvancedWebView web = null!;
	TaskCompletionSource<string>? navigated;
	string scenario = "";
	bool started;

	protected override async void OnAppearing()
	{
		base.OnAppearing();
		if (started)
			return;
		started = true;

		try
		{
			await Log($"env platform={DeviceInfo.Platform} os={DeviceInfo.VersionString} model={DeviceInfo.Model} webview={WebViewVersion()} B={B}");
			await RunAll();
		}
		catch (Exception ex)
		{
			await Log($"error {ex}");
		}
		finally
		{
			try { await http.PostAsync($"{A}/done", null); } catch { }
		}
	}

	async Task RunAll()
	{
		// Reload when LoadUrl was the first navigation of the web view (no Source before it).
		await Scenario("s1-first-reload", withPreviousPage: false, async () =>
		{
			await Step("load", () => web.LoadUrl($"{A}/page/s1", Headers));
			await Step("reload", () => web.Reload());
		});

		await Scenario("s2-reload", withPreviousPage: true, async () =>
		{
			await Step("load", () => web.LoadUrl($"{A}/page/s2", Headers));
			await Step("reload", () => web.Reload());
		});

		var redirects = new[] { ("s3", "r302same"), ("s4", "r307same"), ("s5", "r302cross"), ("s6", "r307cross") };
		foreach (var (id, kind) in redirects)
		{
			await Scenario($"{id}-{kind}", withPreviousPage: true, async () =>
			{
				await Step("load", () => web.LoadUrl($"{A}/{kind}/{id}", Headers));
				await Step("reload", () => web.Reload());
			});
		}

		await Scenario("s7-back", withPreviousPage: true, async () =>
		{
			await Step("load", () => web.LoadUrl($"{A}/page/s7", Headers));
			await Step("next", () => web.Source = $"{A}/plain/s7-next");
			await Step("back", () => web.GoBack());
		});

		// Script click on the page link (the closest to a tap that can run unattended).
		await Scenario("s8-link", withPreviousPage: true, async () =>
		{
			await Step("load", () => web.LoadUrl($"{A}/page/s8", Headers));
			await Step("click", () => _ = web.EvaluateJavaScriptAsync("document.getElementById('next').click()"));
		});
	}

	async Task Scenario(string name, bool withPreviousPage, Func<Task> body)
	{
		scenario = name;
		await Log($"{name} begin");
		await NewView();
		if (withPreviousPage)
			await Step("previous-page", () => web.Source = $"{A}/plain/{name.Split('-')[0]}-pre");
		await body();
		await Log($"{name} end");
	}

	async Task NewView()
	{
		var view = new AdvancedWebView();
		view.Navigating += (_, e) => _ = Log($"{scenario} Navigating {e.Url} ({e.NavigationEvent})");
		view.Navigated += (_, e) =>
		{
			_ = Log($"{scenario} Navigated {e.Url} ({e.Result})");
			navigated?.TrySetResult(e.Url);
		};

		var attached = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
		view.HandlerChanged += (_, _) =>
		{
			if (view.Handler is not null)
				attached.TrySetResult();
		};

		Content = view;
		await attached.Task.WaitAsync(TimeSpan.FromSeconds(15));
		await Task.Delay(1000);
		web = view;
	}

	async Task Step(string name, Action action)
	{
		navigated = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
		await Log($"{scenario} step {name}");
		action();
		try
		{
			await navigated.Task.WaitAsync(TimeSpan.FromSeconds(15));
		}
		catch (TimeoutException)
		{
			await Log($"{scenario} step {name}: no Navigated within 15 s");
		}
		// Sub-resources and any late request.
		await Task.Delay(2000);
	}

	async Task Log(string line)
	{
		try
		{
			await http.PostAsync($"{A}/log", new StringContent(line));
		}
		catch
		{
			// The echo server is the only output; nothing else to do.
		}
	}

	static string WebViewVersion()
	{
#if ANDROID
		return OperatingSystem.IsAndroidVersionAtLeast(26)
			? Android.Webkit.WebView.CurrentWebViewPackage?.VersionName ?? "unknown"
			: "unknown";
#else
		return "WKWebView (system)";
#endif
	}
}
