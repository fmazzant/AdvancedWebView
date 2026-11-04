namespace Mafesoft.Maui.Controls
{
    /// <summary>
    /// Throw from a bridge handler to reject the page's Promise with a specific <c>error.code</c>.
    /// Other exceptions reject it with code <c>HandlerError</c>.
    /// </summary>
    /// <example>
    /// <code>throw new BridgeException("NotFound", $"User {id} does not exist.");</code>
    /// </example>
    public class BridgeException : Exception
    {
        /// <summary>Creates the exception.</summary>
        /// <param name="code">Value of <c>error.code</c> in JavaScript.</param>
        /// <param name="message">Value of <c>error.message</c> in JavaScript.</param>
        public BridgeException(string code, string message) : base(message)
        {
            ArgumentException.ThrowIfNullOrEmpty(code);
            Code = code;
        }

        /// <summary>Creates the exception with an inner exception.</summary>
        public BridgeException(string code, string message, Exception? innerException) : base(message, innerException)
        {
            ArgumentException.ThrowIfNullOrEmpty(code);
            Code = code;
        }

        /// <summary>Value of <c>error.code</c> in JavaScript.</summary>
        public string Code { get; }
    }

    /// <summary>Error codes the bridge itself uses to reject Promises.</summary>
    public static class BridgeErrorCodes
    {
        /// <summary>No handler is registered with the requested name.</summary>
        public const string MethodNotFound = nameof(MethodNotFound);

        /// <summary>The arguments could not be converted to the handler's request type.</summary>
        public const string InvalidArguments = nameof(InvalidArguments);

        /// <summary>The handler took longer than <see cref="BridgeOptions.InvokeTimeout"/>.</summary>
        public const string Timeout = nameof(Timeout);

        /// <summary>The handler threw an exception.</summary>
        public const string HandlerError = nameof(HandlerError);
    }
}
