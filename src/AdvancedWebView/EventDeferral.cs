namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// Deferrals of an event whose answer can be given after an <c>await</c>: the answer is ready once the handlers have
    /// returned and every deferral is disposed.
    /// </summary>
    internal sealed class EventDeferral
    {
        readonly TaskCompletionSource done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int pending = 1; // the event itself; deferrals add to it

        public IDisposable GetDeferral()
        {
            Interlocked.Increment(ref pending);
            return new Deferral(this);
        }

        /// <summary>Call once the event handlers have returned: completes when every deferral is disposed.</summary>
        public Task WhenDoneAsync()
        {
            Release();
            return done.Task;
        }

        void Release()
        {
            if (Interlocked.Decrement(ref pending) == 0)
                done.TrySetResult();
        }

        sealed class Deferral(EventDeferral owner) : IDisposable
        {
            int disposed;

            public void Dispose()
            {
                if (Interlocked.Exchange(ref disposed, 1) == 0)
                    owner.Release();
            }
        }
    }
}
