using System.Net;
using System.Text;

namespace Mafesoft.Maui.Controls
{
    /// <summary>Platform-independent parts of downloads: data: URLs, file names and the HTTP transfer.</summary>
    internal static class Downloads
    {
        static readonly HttpClient Http = new(new SocketsHttpHandler { UseCookies = false, AutomaticDecompression = DecompressionMethods.All });

        /// <summary>Decodes a <c>data:[mime][;base64],payload</c> URL. Returns null when it is not one.</summary>
        public static (byte[] Data, string MimeType)? ParseDataUrl(string url)
        {
            if (!url.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
                return null;

            var comma = url.IndexOf(',');
            if (comma < 0)
                return null;

            var header = url[5..comma];
            var payload = url[(comma + 1)..];
            var parameters = header.Split(';');
            var mime = string.IsNullOrEmpty(parameters[0]) ? "text/plain" : parameters[0].ToLowerInvariant();
            var isBase64 = parameters.Skip(1).Any(p => p.Equals("base64", StringComparison.OrdinalIgnoreCase));

            try
            {
                var data = isBase64
                    ? Convert.FromBase64String(Uri.UnescapeDataString(payload))
                    : Encoding.UTF8.GetBytes(Uri.UnescapeDataString(payload));
                return (data, mime);
            }
            catch (FormatException)
            {
                return null;
            }
        }

        /// <summary>
        /// A safe file name: the suggested one, else the last segment of an http(s) URL, else "download";
        /// with an extension from the MIME type when it has none.
        /// </summary>
        public static string FileName(string? suggested, string url, string? mimeType)
        {
            var name = suggested;
            if (string.IsNullOrWhiteSpace(name) && Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme is "http" or "https")
                name = Uri.UnescapeDataString(uri.Segments.LastOrDefault() ?? string.Empty);

            // Last path segment only (no Path.GetFileName: on Windows it would treat "a:" as a drive).
            name = (name ?? string.Empty).Replace('\\', '/').Split('/').Last().Trim().Trim('.');
            foreach (var c in Path.GetInvalidFileNameChars().Concat(['<', '>', ':', '"', '|', '?', '*']))
                name = name.Replace(c, '_');

            if (string.IsNullOrEmpty(name))
                name = "download";

            if (!Path.HasExtension(name) && ExtensionFor(mimeType) is { } extension)
                name += extension;

            return name;
        }

        static string? ExtensionFor(string? mimeType) => mimeType?.ToLowerInvariant() switch
        {
            "application/pdf" => ".pdf",
            "application/zip" or "application/x-zip-compressed" => ".zip",
            "application/json" => ".json",
            "text/plain" => ".txt",
            "text/csv" => ".csv",
            "text/html" => ".html",
            "image/png" => ".png",
            "image/jpeg" => ".jpg",
            "image/gif" => ".gif",
            "image/svg+xml" => ".svg",
            "image/webp" => ".webp",
            "audio/mpeg" => ".mp3",
            "video/mp4" => ".mp4",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document" => ".docx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet" => ".xlsx",
            _ => null,
        };

        /// <summary>
        /// Downloads <paramref name="url"/> to <paramref name="path"/> with the web view's cookies and user agent.
        /// Progress goes from 0 to 1 when the size is known; 1 is always reported at the end.
        /// </summary>
        public static async Task DownloadAsync(string url, string? cookieHeader, string? userAgent, string? referrer,
            string path, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (!string.IsNullOrEmpty(cookieHeader))
                request.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
            if (!string.IsNullOrEmpty(userAgent))
                request.Headers.TryAddWithoutValidation("User-Agent", userAgent);
            if (Uri.TryCreate(referrer, UriKind.Absolute, out var referrerUri) && referrerUri.Scheme is "http" or "https")
                request.Headers.Referrer = referrerUri;

            using var response = await Http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            var total = response.Content.Headers.ContentLength;
            await using var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await CopyToFileAsync(source, path, total, progress, cancellationToken).ConfigureAwait(false);
        }

        /// <summary>Writes <paramref name="source"/> to <paramref name="path"/>, replacing it; a partial file is deleted on failure.</summary>
        public static async Task CopyToFileAsync(Stream source, string path, long? total, IProgress<double>? progress, CancellationToken cancellationToken)
        {
            if (Path.GetDirectoryName(path) is { Length: > 0 } directory)
                Directory.CreateDirectory(directory);

            try
            {
                await using (var target = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
                {
                    var buffer = new byte[81920];
                    long written = 0;
                    int read;
                    progress?.Report(0);
                    while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                    {
                        await target.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                        written += read;
                        if (total is > 0)
                            progress?.Report(Math.Min(1d, (double)written / total.Value));
                    }
                }
                progress?.Report(1);
            }
            catch
            {
                TryDelete(path);
                throw;
            }
        }

        static void TryDelete(string path)
        {
            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
