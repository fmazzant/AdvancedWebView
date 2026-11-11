using System.Net;

namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// A server (or a proxy) asks for HTTP authentication. Call <see cref="Proceed"/> with the credentials, or
    /// <see cref="Cancel"/>. For an asynchronous answer (for example a login dialog), call <see cref="GetDeferral"/>
    /// before the first <c>await</c> and dispose it after.
    /// </summary>
    public sealed class HttpAuthenticationRequestedEventArgs : EventArgs
    {
        readonly EventDeferral deferral = new();

        internal HttpAuthenticationRequestedEventArgs(string host, string? realm, HttpAuthenticationScheme scheme, bool isProxy, int previousFailureCount)
        {
            Host = host;
            Realm = realm;
            Scheme = scheme;
            IsProxy = isProxy;
            PreviousFailureCount = previousFailureCount;
        }

        /// <summary>The host that asks, for example <c>intranet.example.com</c>.</summary>
        public string Host { get; }

        /// <summary>The realm the server names, if any (for example <c>Intranet</c>).</summary>
        public string? Realm { get; }

        /// <summary>The scheme. Android does not report it: <see cref="HttpAuthenticationScheme.Unknown"/>.</summary>
        public HttpAuthenticationScheme Scheme { get; }

        /// <summary>Whether a proxy asks, not the site. Only iOS and Mac Catalyst report it.</summary>
        public bool IsProxy { get; }

        /// <summary>
        /// How many times credentials were already given for this host and realm and refused. Use it to stop asking
        /// after wrong credentials, for example with <see cref="Cancel"/> when it reaches 3.
        /// </summary>
        public int PreviousFailureCount { get; }

        /// <summary>The credentials given with <see cref="Proceed"/>; null when cancelled or not answered.</summary>
        internal NetworkCredential? Credentials { get; private set; }

        /// <summary>Sends <paramref name="userName"/> and <paramref name="password"/> to the server.</summary>
        public void Proceed(string userName, string password)
        {
            ArgumentNullException.ThrowIfNull(userName);
            ArgumentNullException.ThrowIfNull(password);
            Credentials = new NetworkCredential(userName, password);
        }

        /// <summary>Sends no credentials: the page shows the server's answer to an unauthenticated request (usually 401).</summary>
        public void Cancel() => Credentials = null;

        /// <summary>
        /// Delays the answer until the returned object is disposed, so <see cref="Proceed"/> can be called after an <c>await</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// using var deferral = e.GetDeferral();
        /// var login = await ShowLoginAsync(e.Host, e.Realm);
        /// if (login is null) e.Cancel(); else e.Proceed(login.User, login.Password);
        /// </code>
        /// </example>
        public IDisposable GetDeferral() => deferral.GetDeferral();

        /// <summary>Completes once the event handlers have returned and every deferral is disposed.</summary>
        internal Task WhenAnsweredAsync() => deferral.WhenDoneAsync();
    }

    // HTTP authentication: Basic, Digest, NTLM, Negotiate (#54).
    public partial class AdvancedWebView
    {
        /// <summary>
        /// Raised on the main thread when a page asks for HTTP authentication (Basic, Digest, NTLM, Negotiate).
        /// Without a handler the platform's default applies: Android and iOS send no credentials, WebView2 on Windows
        /// shows its own login dialog.
        /// </summary>
        /// <remarks>
        /// The platform keeps accepted credentials for the session, so the event is usually raised once per site.
        /// After wrong credentials the server asks again: use <see cref="HttpAuthenticationRequestedEventArgs.PreviousFailureCount"/>
        /// to stop, for example with <see cref="HttpAuthenticationRequestedEventArgs.Cancel"/>.
        /// </remarks>
        public event EventHandler<HttpAuthenticationRequestedEventArgs>? HttpAuthenticationRequested;

        /// <summary>Whether a handler is attached: without one, the platform's default handling is kept.</summary>
        internal bool HasHttpAuthenticationHandler => HttpAuthenticationRequested is not null;

        /// <summary>Raises <see cref="HttpAuthenticationRequested"/> and waits for its deferrals; null means cancel.</summary>
        internal async Task<NetworkCredential?> RequestHttpAuthenticationAsync(
            string host, string? realm, HttpAuthenticationScheme scheme, bool isProxy, int previousFailureCount)
        {
            var e = new HttpAuthenticationRequestedEventArgs(host, realm, scheme, isProxy, previousFailureCount);
            try
            {
                HttpAuthenticationRequested?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                LogWarning($"HttpAuthenticationRequested handler failed, the request is cancelled: {ex.Message}");
                return null;
            }

            await e.WhenAnsweredAsync();
            return e.Credentials;
        }
    }
}
