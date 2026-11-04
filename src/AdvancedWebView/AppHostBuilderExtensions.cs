using Mafesoft.Maui.Controls.Handlers;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Registration of <see cref="AdvancedWebView"/> in a MAUI app.</summary>
    public static class AppHostBuilderExtensions
    {
        /// <summary>
        /// Registers the <see cref="AdvancedWebView"/> handler. Call it in <c>MauiProgram.CreateMauiApp</c>.
        /// </summary>
        public static MauiAppBuilder UseAdvancedWebView(this MauiAppBuilder builder)
        {
            builder.ConfigureMauiHandlers(handlers =>
                handlers.AddHandler<AdvancedWebView, AdvancedWebViewHandler>());
            return builder;
        }
    }
}
