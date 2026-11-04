namespace Mafesoft.Maui.Controls
{
    /// <summary>The JavaScript side of the bridge, injected into every page.</summary>
    internal static class BridgeScript
    {
        /// <summary>Name of the object injected by Android's WebMessageListener.</summary>
        public const string AndroidObjectName = "__advancedWebViewNative";

        /// <summary>Name of the WKScriptMessageHandler (window.webkit.messageHandlers.&lt;name&gt;).</summary>
        public const string IosHandlerName = "advancedWebView";

        /// <summary>
        /// Defines <c>window.AdvancedWebView</c>. Idempotent: running it twice on the same page keeps the first instance.
        /// Every page load gets a random <c>pageId</c>, so C# can tell pages apart and drop work for pages that are gone.
        /// </summary>
        public const string Source = """
            (function () {
              if (window.AdvancedWebView && window.AdvancedWebView.__bridge) { return; }

              var pageId = Math.random().toString(36).slice(2) + Date.now().toString(36);
              var nextId = 1;
              var pending = {};
              var listeners = [];

              function send(envelope) {
                envelope.pageId = pageId;
                var text = JSON.stringify(envelope);
                if (window.webkit && window.webkit.messageHandlers && window.webkit.messageHandlers.advancedWebView) {
                  window.webkit.messageHandlers.advancedWebView.postMessage(text);
                } else if (window.__advancedWebViewNative) {
                  window.__advancedWebViewNative.postMessage(text);
                } else {
                  throw new Error("AdvancedWebView bridge is not available in this frame.");
                }
              }

              function toJson(value) { return value === undefined ? null : value; }

              var api = {
                __bridge: true,

                postMessage: function (data) {
                  send({ type: "message", data: toJson(data) });
                },

                invoke: function (method, args) {
                  return new Promise(function (resolve, reject) {
                    var id = String(nextId++);
                    pending[id] = { resolve: resolve, reject: reject };
                    try {
                      send({ type: "invoke", id: id, method: String(method), args: toJson(args) });
                    } catch (e) {
                      delete pending[id];
                      reject(e);
                    }
                  });
                },

                addEventListener: function (type, listener) {
                  if (type === "message" && typeof listener === "function" && listeners.indexOf(listener) < 0) {
                    listeners.push(listener);
                  }
                },

                removeEventListener: function (type, listener) {
                  var i = listeners.indexOf(listener);
                  if (type === "message" && i >= 0) { listeners.splice(i, 1); }
                },

                // Error page (ErrorPageHtml): loads the URL that failed. Elsewhere it reloads the page.
                // A normal navigation, so Navigating and the external-link rules still apply.
                retry: function () {
                  var url = window.__advancedWebViewFailedUrl;
                  if (typeof url === "string" && url) { window.location.href = url; } else { window.location.reload(); }
                },

                __receive: function (envelope) {
                  if (envelope.type === "message") {
                    var event = { data: envelope.data };
                    listeners.slice().forEach(function (l) {
                      try { l(event); } catch (e) { setTimeout(function () { throw e; }); }
                    });
                    return;
                  }
                  if (envelope.pageId !== pageId) { return; }
                  var call = pending[envelope.id];
                  if (!call) { return; }
                  delete pending[envelope.id];
                  if (envelope.type === "result") {
                    call.resolve(envelope.result);
                  } else {
                    var info = envelope.error || {};
                    var error = new Error(info.message || "AdvancedWebView handler failed.");
                    error.code = info.code || "HandlerError";
                    if (info.details) { error.details = info.details; }
                    call.reject(error);
                  }
                },

                // C# InvokeJavaScriptAsync: calls window.<name> with the arguments and posts back the (awaited) result.
                __call: function (id, name, args) {
                  function errorInfo(e) {
                    return { message: (e && e.message) ? String(e.message) : String(e), stack: (e && e.stack) ? String(e.stack) : null };
                  }
                  function reply(envelope) {
                    envelope.id = id;
                    try {
                      send(envelope);
                    } catch (e) {
                      // Typically a result JSON.stringify cannot handle (circular object).
                      try { send({ type: "callError", id: id, error: errorInfo(e) }); } catch (ignored) { }
                    }
                  }
                  try {
                    var parts = String(name).split(".");
                    var owner = window;
                    var target = window;
                    for (var i = 0; i < parts.length; i++) {
                      owner = target;
                      target = (target === null || target === undefined) ? undefined : target[parts[i]];
                    }
                    if (typeof target !== "function") { throw new TypeError("'" + name + "' is not a function."); }
                    Promise.resolve(target.apply(owner, args || [])).then(
                      function (result) { reply({ type: "callResult", result: toJson(result) }); },
                      function (e) { reply({ type: "callError", error: errorInfo(e) }); });
                  } catch (e) {
                    reply({ type: "callError", error: errorInfo(e) });
                  }
                }
              };

              api.ready = Promise.resolve(api);
              Object.freeze(api);
              Object.defineProperty(window, "AdvancedWebView", { value: api, writable: false, configurable: false, enumerable: true });

              // Android: replies to this frame arrive on the object injected by WebMessageListener.
              if (window.__advancedWebViewNative) {
                window.__advancedWebViewNative.onmessage = function (event) {
                  try { api.__receive(JSON.parse(event.data)); } catch (e) { }
                };
              }

              try { window.dispatchEvent(new Event("advancedwebviewready")); } catch (e) { }
            })();
            """;
    }
}
