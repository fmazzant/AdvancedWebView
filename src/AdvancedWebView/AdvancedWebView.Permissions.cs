namespace Mafesoft.Maui.Controls
{
    /// <summary>Device features a page can ask for.</summary>
    [Flags]
    public enum WebPermissions
    {
        /// <summary>None.</summary>
        None = 0,

        /// <summary>The camera (<c>getUserMedia({ video: true })</c>).</summary>
        Camera = 1,

        /// <summary>The microphone (<c>getUserMedia({ audio: true })</c>).</summary>
        Microphone = 2,

        /// <summary>The location (<c>navigator.geolocation</c>).</summary>
        Geolocation = 4,
    }

    /// <summary>What to answer when a page asks for the camera, the microphone or the location.</summary>
    public enum PermissionDecision
    {
        /// <summary>
        /// Let the user decide. iOS: WebKit asks for each site (camera and microphone).
        /// Android has no such dialog: the page gets the permission when the app has it, asking the OS permission
        /// the first time. Handle <see cref="AdvancedWebView.PermissionRequested"/> to show your own dialog.
        /// </summary>
        AskUser,

        /// <summary>Give the permission to the page. The OS permission dialog still appears the first time.</summary>
        Grant,

        /// <summary>Refuse the permission. The page's request fails.</summary>
        Deny,
    }

    /// <summary>
    /// A page asked for the camera, the microphone or the location. Set <see cref="Decision"/>. For an asynchronous
    /// decision (for example a dialog), call <see cref="GetDeferral"/> before the first <c>await</c> and dispose it after.
    /// </summary>
    public sealed class PermissionRequestedEventArgs : EventArgs
    {
        readonly TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int pending = 1; // the event itself; deferrals add to it

        internal PermissionRequestedEventArgs(Uri? origin, WebPermissions permissions, PermissionDecision decision)
        {
            Origin = origin;
            Permissions = permissions;
            Decision = decision;
        }

        /// <summary>Origin of the page that asks (for example <c>https://meet.example.com</c>); null for local content.</summary>
        public Uri? Origin { get; }

        /// <summary>What the page asks for. Camera and microphone can come together.</summary>
        public WebPermissions Permissions { get; }

        /// <summary>The answer. Starts as <see cref="AdvancedWebView.PermissionRequestBehavior"/>.</summary>
        public PermissionDecision Decision { get; set; }

        /// <summary>
        /// Delays the answer until the returned object is disposed, so <see cref="Decision"/> can be set after an <c>await</c>.
        /// </summary>
        /// <example>
        /// <code>
        /// using var deferral = e.GetDeferral();
        /// e.Decision = await page.DisplayAlertAsync("Camera", "Allow?", "Allow", "Deny") ? PermissionDecision.Grant : PermissionDecision.Deny;
        /// </code>
        /// </example>
        public IDisposable GetDeferral()
        {
            Interlocked.Increment(ref pending);
            return new Deferral(this);
        }

        /// <summary>Completes once the event handlers have returned and every deferral is disposed.</summary>
        internal Task WhenDecidedAsync()
        {
            Release();
            return done.Task;
        }

        void Release()
        {
            if (Interlocked.Decrement(ref pending) == 0)
                done.TrySetResult();
        }

        sealed class Deferral(PermissionRequestedEventArgs owner) : IDisposable
        {
            int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    owner.Release();
            }
        }
    }

    // Camera, microphone and location requested by the page.
    public partial class AdvancedWebView
    {
        /// <summary>Bindable property for <see cref="PermissionRequestBehavior"/>.</summary>
        public static readonly BindableProperty PermissionRequestBehaviorProperty =
            BindableProperty.Create(nameof(PermissionRequestBehavior), typeof(PermissionDecision), typeof(AdvancedWebView), PermissionDecision.AskUser);

        /// <summary>
        /// The answer to a page asking for the camera, the microphone or the location, unless a
        /// <see cref="PermissionRequested"/> handler changes it. Defaults to <see cref="PermissionDecision.AskUser"/>.
        /// </summary>
        public PermissionDecision PermissionRequestBehavior
        {
            get => (PermissionDecision)GetValue(PermissionRequestBehaviorProperty);
            set => SetValue(PermissionRequestBehaviorProperty, value);
        }

        /// <summary>
        /// Raised on the main thread when a page asks for the camera, the microphone or (Android only) the location.
        /// On iOS, WebKit handles the location itself and asks the user for each site.
        /// </summary>
        public event EventHandler<PermissionRequestedEventArgs>? PermissionRequested;

        /// <summary>Raises <see cref="PermissionRequested"/> and waits for its deferrals.</summary>
        internal async Task<PermissionDecision> RequestPermissionAsync(Uri? origin, WebPermissions permissions)
        {
            var e = new PermissionRequestedEventArgs(origin, permissions, PermissionRequestBehavior);
            try
            {
                PermissionRequested?.Invoke(this, e);
            }
            catch (Exception ex)
            {
                LogWarning($"PermissionRequested handler failed, the permission is denied: {ex.Message}");
                return PermissionDecision.Deny;
            }

            await e.WhenDecidedAsync();
            return e.Decision;
        }

        /// <summary>
        /// Asks the OS permissions the app needs for <paramref name="wanted"/> (only the first time) and returns those it has.
        /// </summary>
        internal async Task<WebPermissions> RequestOsPermissionsAsync(WebPermissions wanted)
        {
            var granted = WebPermissions.None;
            if (wanted.HasFlag(WebPermissions.Camera) && await EnsureAsync<Permissions.Camera>())
                granted |= WebPermissions.Camera;
            if (wanted.HasFlag(WebPermissions.Microphone) && await EnsureAsync<Permissions.Microphone>())
                granted |= WebPermissions.Microphone;
            if (wanted.HasFlag(WebPermissions.Geolocation) && await EnsureAsync<Permissions.LocationWhenInUse>())
                granted |= WebPermissions.Geolocation;
            return granted;
        }

        async Task<bool> EnsureAsync<T>() where T : Permissions.BasePermission, new()
        {
            try
            {
                if (await Permissions.CheckStatusAsync<T>() == PermissionStatus.Granted)
                    return true;
                return await Permissions.RequestAsync<T>() == PermissionStatus.Granted;
            }
            catch (Exception ex)
            {
                // Typically PermissionException: the permission is missing from the app manifest.
                LogWarning($"{typeof(T).Name} permission unavailable: {ex.Message}");
                return false;
            }
        }
    }
}
