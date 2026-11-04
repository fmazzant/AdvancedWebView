using System.Net;
using System.Net.Sockets;
using System.Text;

namespace Mafesoft.Maui.Controls.Tests;

public class DownloadsTests
{
    [Fact]
    public void Data_url_base64()
    {
        var parsed = Downloads.ParseDataUrl("data:application/pdf;base64," + Convert.ToBase64String([1, 2, 3]));

        Assert.NotNull(parsed);
        Assert.Equal([1, 2, 3], parsed.Value.Data);
        Assert.Equal("application/pdf", parsed.Value.MimeType);
    }

    [Fact]
    public void Data_url_text_is_percent_decoded()
    {
        var parsed = Downloads.ParseDataUrl("data:text/csv;charset=utf-8,a%2Cb%0A1%2C%C3%A8");

        Assert.Equal("a,b\n1,è", Encoding.UTF8.GetString(parsed!.Value.Data));
        Assert.Equal("text/csv", parsed.Value.MimeType);
    }

    [Fact]
    public void Data_url_without_type_is_text_plain() =>
        Assert.Equal("text/plain", Downloads.ParseDataUrl("data:,hello")!.Value.MimeType);

    [Theory]
    [InlineData("https://example.com/a.pdf")]
    [InlineData("data:text/plain")] // no comma
    [InlineData("data:;base64,@@@")] // invalid base64
    public void Not_a_valid_data_url(string url) =>
        Assert.Null(Downloads.ParseDataUrl(url));

    [Theory]
    [InlineData("report.pdf", "https://example.com/x", null, "report.pdf")]
    [InlineData(null, "https://example.com/files/Invoice%202026.pdf?id=1", null, "Invoice 2026.pdf")]
    [InlineData(null, "https://example.com/download", "application/zip", "download.zip")]
    [InlineData(null, "blob:https://example.com/1234-5678", "application/pdf", "download.pdf")]
    [InlineData(null, "data:text/csv,a", "text/csv", "download.csv")]
    [InlineData("../../etc/passwd", "https://example.com/", null, "passwd")]
    [InlineData("a:b|c?.txt", "https://example.com/", null, "a_b_c_.txt")]
    [InlineData("   ", "https://example.com/", null, "download")]
    [InlineData(null, "https://example.com/", "application/x-unknown", "download")]
    public void File_names(string? suggested, string url, string? mime, string expected) =>
        Assert.Equal(expected, Downloads.FileName(suggested, url, mime));

    [Fact]
    public async Task Copy_reports_progress_and_writes_the_file()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "out.bin");
        var data = new byte[200_000];
        Random.Shared.NextBytes(data);
        var reported = new List<double>();

        await Downloads.CopyToFileAsync(new MemoryStream(data), path, data.Length, new SyncProgress(reported.Add), TestContext.Current.CancellationToken);

        Assert.Equal(data, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(0, reported[0]);
        Assert.Equal(1, reported[^1]);
        Assert.Equal(reported.OrderBy(p => p), reported);
        Directory.Delete(Path.GetDirectoryName(path)!, true);
    }

    [Fact]
    public async Task Cancelled_copy_deletes_the_partial_file()
    {
        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".bin");
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            Downloads.CopyToFileAsync(new MemoryStream(new byte[1000]), path, 1000, null, cts.Token));

        Assert.False(File.Exists(path));
    }

    [Fact]
    public async Task Http_download_sends_cookies_user_agent_and_referrer()
    {
        var port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();

        string? cookie = null, agent = null, referrer = null;
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            cookie = context.Request.Headers["Cookie"];
            agent = context.Request.UserAgent;
            referrer = context.Request.UrlReferrer?.ToString();
            var body = Encoding.UTF8.GetBytes("file content");
            context.Response.ContentLength64 = body.Length;
            await context.Response.OutputStream.WriteAsync(body, TestContext.Current.CancellationToken);
            context.Response.Close();
        }, TestContext.Current.CancellationToken);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        await Downloads.DownloadAsync($"http://localhost:{port}/file.txt", "session=abc", "TestAgent/1.0", "https://example.com/page",
            path, null, TestContext.Current.CancellationToken);
        await server;

        Assert.Equal("file content", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal("session=abc", cookie);
        Assert.Equal("TestAgent/1.0", agent);
        Assert.Equal("https://example.com/page", referrer);
        File.Delete(path);
    }

    [Fact]
    public async Task Http_error_throws_and_leaves_no_file()
    {
        var port = FreePort();
        using var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{port}/");
        listener.Start();
        var server = Task.Run(async () =>
        {
            var context = await listener.GetContextAsync();
            context.Response.StatusCode = 404;
            context.Response.Close();
        }, TestContext.Current.CancellationToken);

        var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N") + ".txt");
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Downloads.DownloadAsync($"http://localhost:{port}/missing", null, null, null, path, null, TestContext.Current.CancellationToken));
        await server;

        Assert.False(File.Exists(path));
    }

    static int FreePort()
    {
        using var socket = new TcpListener(IPAddress.Loopback, 0);
        socket.Start();
        return ((IPEndPoint)socket.LocalEndpoint).Port;
    }

    // Progress<T> posts to the thread pool; this one reports synchronously, in order.
    sealed class SyncProgress(Action<double> report) : IProgress<double>
    {
        public void Report(double value) => report(value);
    }
}
