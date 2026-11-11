using Mafesoft.Maui.Controls;

namespace HeaderRedirects;

public static class MauiProgram
{
	public static MauiApp CreateMauiApp()
	{
		var builder = MauiApp.CreateBuilder();
		builder.UseMauiApp<App>().UseAdvancedWebView();
		return builder.Build();
	}
}

public class App : Application
{
	protected override Window CreateWindow(IActivationState? activationState) =>
		new(new ScenarioPage());
}
