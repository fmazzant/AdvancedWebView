document.getElementById("script").textContent = "The script ran.";
document.getElementById("origin").textContent = "Origin: " + location.origin + " (" + location.href + ")";

// Pages of a custom scheme are app content: the bridge works without Bridge.AllowedOrigins.
document.getElementById("btnBridge").addEventListener("click", function () {
  if (window.AdvancedWebView)
    AdvancedWebView.postMessage({ from: location.href });
  else
    alert("The bridge is not available.");
});
