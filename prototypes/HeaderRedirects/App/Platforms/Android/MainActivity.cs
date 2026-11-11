using Android.App;
using Android.Content.PM;

namespace HeaderRedirects;

// Fixed name: the CI job starts it with adb shell am start.
[Activity(Name = "com.mafesoft.headerredirects.MainActivity", Theme = "@style/Maui.MainTheme.NoActionBar", MainLauncher = true, Exported = true,
	ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
