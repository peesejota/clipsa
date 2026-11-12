using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Text.Json;

namespace Clipsa;

public sealed record ReleaseInfo(string Version, string DownloadUrl, string Sha256, string? ReleaseNotesUrl);
public static class UpdateService
{
    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 4, 0);
    // A repository URL resolves to an asset on the latest stable GitHub Release.
    // No access tokens are stored or embedded in the application.
    public static Uri ResolveFeed(string feed)
    {
        if (!Uri.TryCreate(feed.Trim(), UriKind.Absolute, out var url) || url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0)
            throw new ArgumentException("Introduce una URL HTTPS sin credenciales: un manifiesto o un repositorio público de GitHub.");
        if (url.Host.Equals("github.com", StringComparison.OrdinalIgnoreCase))
        {
            var segments = url.AbsolutePath.Trim('/').Split('/');
            if (segments.Length is 2 or 3 or 4 && segments.Take(2).All(s => s.Length > 0 && s.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.')) &&
                (segments.Length == 2 || segments[2] == "releases" && (segments.Length == 3 || segments[3] == "latest")) && url.Query.Length == 0 && url.Fragment.Length == 0)
                return new Uri($"https://github.com/{segments[0]}/{segments[1]}/releases/latest/download/update-manifest.json");
        }
        return url;
    }
    public static async Task<ReleaseInfo?> CheckAsync(string feed, CancellationToken cancellation = default)
    {
        var url = ResolveFeed(feed);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        cancellation = deadline.Token;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(15) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Clipsa/" + CurrentVersion);
        using var response = await GetHttpsAsync(client, url, cancellation);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 128_000) throw new InvalidDataException("El manifiesto de actualización es demasiado grande.");
        await using var input = await response.Content.ReadAsStreamAsync(cancellation);
        using var limited = new MemoryStream();
        var buffer = new byte[4096];
        for (var read = await input.ReadAsync(buffer, cancellation); read > 0; read = await input.ReadAsync(buffer, cancellation))
        {
            if (limited.Length + read > 128_000) throw new InvalidDataException("El manifiesto de actualización es demasiado grande.");
            limited.Write(buffer, 0, read);
        }
        var release = JsonSerializer.Deserialize<ReleaseInfo>(limited.ToArray(), new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Manifiesto de actualización vacío.");
        if (!Version.TryParse(release.Version, out var version) || !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var download) || download.Scheme != Uri.UriSchemeHttps || download.UserInfo.Length != 0 ||
            release.Sha256 is null || release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("El manifiesto de actualización no es válido.");
        return version > CurrentVersion ? release : null;
    }

    private static async Task<HttpResponseMessage> GetHttpsAsync(HttpClient client, Uri url, CancellationToken cancellation)
    {
        for (var redirect = 0; redirect <= 5; redirect++)
        {
            var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellation);
            if ((int)response.StatusCode is not (301 or 302 or 303 or 307 or 308)) return response;
            var location = response.Headers.Location;
            response.Dispose();
            if (location is null) throw new InvalidDataException("Redirección de actualización sin destino.");
            url = location.IsAbsoluteUri ? location : new Uri(url, location);
            if (url.Scheme != Uri.UriSchemeHttps || url.UserInfo.Length != 0) throw new InvalidDataException("Redirección de actualización no segura.");
        }
        throw new InvalidDataException("Demasiadas redirecciones en el canal de actualizaciones.");
    }
}
