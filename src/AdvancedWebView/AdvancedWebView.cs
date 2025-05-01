using Microsoft.Maui;
using System.Windows.Input;

namespace Mafesoft.Maui.Controls
{
    public class AdvancedWebView : WebView
    {
        public static readonly BindableProperty JavaScriptEnabledProperty =
          BindableProperty.Create(
              propertyName: nameof(JavaScriptEnabled),
              returnType: typeof(bool),
              declaringType: typeof(AdvancedWebView),
              defaultValue: false,
              defaultBindingMode: BindingMode.TwoWay
              );
        public bool JavaScriptEnabled
        {
            get => (bool)GetValue(JavaScriptEnabledProperty);
            set => SetValue(JavaScriptEnabledProperty, value);
        }

        public static readonly BindableProperty JavaScriptCanOpenWindowsAutomaticallydProperty =
          BindableProperty.Create(
              propertyName: nameof(JavaScriptCanOpenWindowsAutomatically),
              returnType: typeof(bool),
              declaringType: typeof(AdvancedWebView),
              defaultValue: false,
              defaultBindingMode: BindingMode.TwoWay
          );
        public bool JavaScriptCanOpenWindowsAutomatically
        {
            get => (bool)GetValue(JavaScriptCanOpenWindowsAutomaticallydProperty);
            set => SetValue(JavaScriptCanOpenWindowsAutomaticallydProperty, value);
        }


        public static readonly BindableProperty ScrollBouncesEnabledProperty =
              BindableProperty.Create(
                  propertyName: nameof(ScrollBouncesEnabled),
                  returnType: typeof(bool),
                  declaringType: typeof(AdvancedWebView),
                  defaultValue: false,
                  defaultBindingMode: BindingMode.TwoWay
              );
        public bool ScrollBouncesEnabled
        {
            get => (bool)GetValue(ScrollBouncesEnabledProperty);
            set => SetValue(ScrollBouncesEnabledProperty, value);
        }

        #region Properties

        public static readonly BindableProperty AllowJavaScriptProperty =
            BindableProperty.Create(nameof(AllowJavaScript), typeof(bool), typeof(AdvancedWebView), true);

        public bool AllowJavaScript
        {
            get => (bool)GetValue(AllowJavaScriptProperty);
            set => SetValue(AllowJavaScriptProperty, value);
        }

        public static readonly BindableProperty EnableCookiesProperty =
            BindableProperty.Create(nameof(EnableCookies), typeof(bool), typeof(AdvancedWebView), true);

        public bool EnableCookies
        {
            get => (bool)GetValue(EnableCookiesProperty);
            set => SetValue(EnableCookiesProperty, value);
        }

        public static readonly BindableProperty UserAgentProperty =
            BindableProperty.Create(nameof(UserAgent), typeof(string), typeof(AdvancedWebView), string.Empty);

        public string UserAgent
        {
            get => (string)GetValue(UserAgentProperty);
            set => SetValue(UserAgentProperty, value);
        }

        public static readonly BindableProperty CacheModeProperty =
            BindableProperty.Create(nameof(CacheMode), typeof(WebViewCacheMode), typeof(AdvancedWebView), WebViewCacheMode.Default);

        public WebViewCacheMode CacheMode
        {
            get => (WebViewCacheMode)GetValue(CacheModeProperty);
            set => SetValue(CacheModeProperty, value);
        }

        #endregion


        #region Methods

        // Metodo per gestire la cache
        public void ClearCache()
        {
            // Implementazione specifica per piattaforma tramite renderer personalizzati
            MessagingCenter.Send(this, "ClearWebViewCache");
        }

        // Metodo per salvare o caricare cookie
        public void SetCookie(string url, string name, string value)
        {
            if (EnableCookies)
            {
                // Implementazione tramite renderer specifici della piattaforma
                MessagingCenter.Send(this, "SetCookie", new Tuple<string, string, string>(url, name, value));
            }
        }

        // Verifica se la navigazione dovrebbe essere bloccata
        private bool ShouldBlockNavigation(string url)
        {
            // Implementare qui la logica di filtraggio URLs
            return false; // Di default, non bloccare nulla
        }

        #endregion
    }

    public enum WebViewCacheMode
    {
        Default,
        NoCache,
        CacheOnly,
        CacheElseNetwork
    }

}
