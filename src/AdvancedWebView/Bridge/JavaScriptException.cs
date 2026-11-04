namespace Mafesoft.Maui.Controls
{
    /// <summary>A JavaScript function called with <c>InvokeJavaScriptAsync</c> threw an error or rejected its Promise.</summary>
    public class JavaScriptException : Exception
    {
        /// <summary>Creates the exception.</summary>
        public JavaScriptException(string message, string? javaScriptStack = null) : base(message)
        {
            JavaScriptStack = javaScriptStack;
        }

        /// <summary>The JavaScript stack trace (<c>error.stack</c>), when the page provided one.</summary>
        public string? JavaScriptStack { get; }
    }
}
