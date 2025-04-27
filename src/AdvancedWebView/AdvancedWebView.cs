namespace Mafesoft.Maui.Controls
{
    public class AdvancedWebView : WebView
    {
        public bool JavaScriptEnabled { get; set; }
        public static readonly BindableProperty JavaScriptEnabledProperty =
          BindableProperty.Create(
              propertyName: nameof(JavaScriptEnabled),
              returnType: typeof(bool),
              declaringType: typeof(AdvancedWebView),
              defaultValue: false,
              defaultBindingMode: BindingMode.TwoWay
              );

        public bool JavaScriptCanOpenWindowsAutomatically { get; set; }
        public static readonly BindableProperty JavaScriptCanOpenWindowsAutomaticallydProperty =
          BindableProperty.Create(
              propertyName: nameof(JavaScriptCanOpenWindowsAutomatically),
              returnType: typeof(bool),
              declaringType: typeof(AdvancedWebView),
              defaultValue: false,
              defaultBindingMode: BindingMode.TwoWay
          );

        public bool ScrollBouncesEnabled { get; set; }
        public static readonly BindableProperty ScrollBouncesEnabledProperty =
              BindableProperty.Create(
                  propertyName: nameof(ScrollBouncesEnabled),
                  returnType: typeof(bool),
                  declaringType: typeof(AdvancedWebView),
                  defaultValue: false,
                  defaultBindingMode: BindingMode.TwoWay
              );
    }
}
