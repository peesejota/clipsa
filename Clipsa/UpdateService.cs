using System.Net.Http;
using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Security.Cryptography;

namespace Clipsa;

public sealed record ReleaseInfo(string Version, string DownloadUrl, string Sha256, string? ReleaseNotesUrl,
    string? PortableDownloadUrl = null, string? PortableSha256 = null)
{
    [JsonIgnore] public bool SignatureVerified { get; init; }
    [JsonIgnore] public string? SignedManifest { get; init; }
}
public static class UpdateService
{
    public static Version CurrentVersion => Assembly.GetExecutingAssembly().GetName().Version ?? new Version(0, 4, 0);
    public const string DefaultFeed = "https://raw.githubusercontent.com/peesejota/clipsa/main/updates/beta.json";
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
                return segments[0].Equals("peesejota", StringComparison.OrdinalIgnoreCase) && segments[1].Equals("clipsa", StringComparison.OrdinalIgnoreCase)
                    ? new Uri(DefaultFeed) : new Uri($"https://github.com/{segments[0]}/{segments[1]}/releases/latest/download/update-manifest.json");
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
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
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
        var release = ParseManifest(System.Text.Encoding.UTF8.GetString(limited.ToArray()), AppPaths.IsPortable);
        var version = Version.Parse(release.Version);
        return version > CurrentVersion ? release : null;
    }
    public static ReleaseInfo ParseManifest(string manifest, bool portable)
    {
        if (manifest.Length > 128_000) throw new InvalidDataException("Manifiesto demasiado grande.");
        using var root = JsonDocument.Parse(manifest);
        var verified = false; var data = System.Text.Encoding.UTF8.GetBytes(manifest);
        if (root.RootElement.TryGetProperty("payload", out var payload))
        {
            data = Convert.FromBase64String(payload.GetString()!);
            var signature = Convert.FromBase64String(root.RootElement.GetProperty("signature").GetString()!);
            if (!UpdateTrust.Verify(data, signature)) throw new InvalidDataException("La firma de la actualización no es válida. No se descargará ni ejecutará.");
            verified = true;
        }
        var release = JsonSerializer.Deserialize<ReleaseInfo>(data, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? throw new InvalidDataException("Manifiesto de actualización vacío.");
        if (portable && verified) release = release with { DownloadUrl = release.PortableDownloadUrl!, Sha256 = release.PortableSha256! };
        if (!Version.TryParse(release.Version, out var version) || !Uri.TryCreate(release.DownloadUrl, UriKind.Absolute, out var download) || download.Scheme != Uri.UriSchemeHttps || download.UserInfo.Length != 0 ||
            release.Sha256 is null || release.Sha256.Length != 64 || !release.Sha256.All(Uri.IsHexDigit)) throw new InvalidDataException("El manifiesto de actualización no es válido.");
        return release with { SignatureVerified = verified, SignedManifest = verified ? manifest : null };
    }

    public static async Task<string> DownloadAsync(ReleaseInfo release, IProgress<double>? progress, CancellationToken cancellation)
    {
        if (!release.SignatureVerified || release.SignedManifest is null) throw new InvalidDataException("Esta fuente no tiene una firma de confianza. Sólo puede abrirse manualmente en el navegador.");
        var trusted = ParseManifest(release.SignedManifest, AppPaths.IsPortable);
        if (trusted.DownloadUrl != release.DownloadUrl || trusted.Sha256 != release.Sha256) throw new InvalidDataException("Los datos de la actualización han cambiado.");
        var folder = Path.Combine(AppPaths.DataDirectory, "updates", Guid.NewGuid().ToString("N")); Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, AppPaths.IsPortable ? "Clipsa-update.zip" : "Clipsa-update.exe");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation); deadline.CancelAfter(TimeSpan.FromMinutes(10)); cancellation = deadline.Token;
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = Timeout.InfiniteTimeSpan };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Clipsa/" + CurrentVersion);
        try
        {
            using var response = await GetHttpsAsync(client, new Uri(release.DownloadUrl), cancellation); response.EnsureSuccessStatusCode();
            var length = response.Content.Headers.ContentLength;
            if (length > 500_000_000) throw new InvalidDataException("Paquete demasiado grande.");
            await using var source = await response.Content.ReadAsStreamAsync(cancellation);
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            long total = 0; var buffer = new byte[81920];
            await using (var target = new FileStream(path + ".partial", FileMode.CreateNew, FileAccess.Write, FileShare.None, buffer.Length, true))
            {
                for (var count = await source.ReadAsync(buffer, cancellation); count > 0; count = await source.ReadAsync(buffer, cancellation))
                {
                    total += count; if (total > 500_000_000) throw new InvalidDataException("Paquete demasiado grande.");
                    hash.AppendData(buffer, 0, count); await target.WriteAsync(buffer.AsMemory(0, count), cancellation);
                    progress?.Report(length > 0 ? (double)total / length.Value : 0);
                }
            }
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("El SHA256 no coincide. El archivo no se ejecutará.");
            File.Move(path + ".partial", path); progress?.Report(1); return path;
        }
        catch { if (File.Exists(path + ".partial")) File.Delete(path + ".partial"); throw; }
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
