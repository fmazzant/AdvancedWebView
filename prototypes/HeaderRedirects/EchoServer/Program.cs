// Echo server for the header/redirect prototype (issue #60).
// Listens on two ports, i.e. two origins: A (5100) and B (5101). Records every request with the two test headers.
//   /page/{id}         HTML page with a script, an image and a link (sub-resources and link navigation)
//   /plain/{id}        HTML page without sub-resources
//   /r{302|307}same/{id}   redirect to /page/{id} on the same origin
//   /r{302|307}cross/{id}  redirect to /page/{id} on the other origin (other port, same host)
//   /res/{id}/...      sub-resources
//   /link/{id}         target of the page link
//   POST /log          free-text line from the app (events, environment)
//   POST /done         the app finished all scenarios
//   GET /results       everything recorded, as JSON (for the CI job)
//   GET /status        "done" or "running"
using System.Collections.Concurrent;
using System.Text;

const int PortA = 5100, PortB = 5101;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{PortA}", $"http://0.0.0.0:{PortB}");
builder.Logging.SetMinimumLevel(LogLevel.Warning);
var app = builder.Build();

var records = new ConcurrentQueue<Record>();
var done = false;
long sequence = 0;

app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? "";
    if (path is not ("/log" or "/done" or "/results" or "/status" or "/favicon.ico"))
    {
        var headers = context.Request.Headers;
        records.Enqueue(new Record(
            Interlocked.Increment(ref sequence),
            "request",
            context.Connection.LocalPort == PortA ? "A" : "B",
            context.Request.Method,
            path,
            headers.Authorization.ToString() is { Length: > 0 } auth ? auth : null,
            headers["X-Proto-Header"].ToString() is { Length: > 0 } custom ? custom : null,
            headers.UserAgent.ToString()));
    }
    context.Response.Headers.CacheControl = "no-store";
    await next();
});

string OtherOrigin(HttpContext context) =>
    $"http://{context.Request.Host.Host}:{(context.Connection.LocalPort == PortA ? PortB : PortA)}";

IResult Html(string body) => Results.Content($"<!doctype html><html><head><meta charset=utf-8><title>proto</title></head><body>{body}</body></html>", "text/html");

app.MapGet("/page/{id}", (string id) => Html(
    $"<h1>{id}</h1><script src=\"/res/{id}/script.js\"></script><img src=\"/res/{id}/img.svg\" width=10 height=10>" +
    $"<a id=\"next\" href=\"/link/{id}\">next</a>"));
app.MapGet("/plain/{id}", (string id) => Html($"<h1>{id}</h1>"));
app.MapGet("/link/{id}", (string id) => Html($"<h1>link {id}</h1>"));
app.MapGet("/res/{id}/script.js", () => Results.Content("window.protoScript = true;", "text/javascript"));
app.MapGet("/res/{id}/img.svg", () => Results.Content("<svg xmlns='http://www.w3.org/2000/svg' width='10' height='10'/>", "image/svg+xml"));

app.MapGet("/r{code:int}same/{id}", (int code, string id) => Redirect(code, $"/page/{id}"));
app.MapGet("/r{code:int}cross/{id}", (HttpContext context, int code, string id) => Redirect(code, $"{OtherOrigin(context)}/page/{id}"));

static IResult Redirect(int code, string location) => code switch
{
    302 => Results.Redirect(location, permanent: false, preserveMethod: false),
    307 => Results.Redirect(location, permanent: false, preserveMethod: true),
    _ => Results.BadRequest(),
};

app.MapPost("/log", async (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body, Encoding.UTF8);
    var line = await reader.ReadToEndAsync();
    records.Enqueue(new Record(Interlocked.Increment(ref sequence), "log", null, null, line, null, null, null));
    Console.WriteLine($"[app] {line}");
    return Results.Ok();
});
app.MapPost("/done", () => { done = true; Console.WriteLine("[app] done"); return Results.Ok(); });
app.MapGet("/status", () => done ? "done" : "running");
app.MapGet("/results", () => Results.Json(records.OrderBy(r => r.Seq)));

Console.WriteLine($"Echo server on ports {PortA} (A) and {PortB} (B)");
app.Run();

record Record(long Seq, string Kind, string? Origin, string? Method, string Path, string? Authorization, string? ProtoHeader, string? UserAgent);
