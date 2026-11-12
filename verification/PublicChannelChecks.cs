using System.Net.Http;
using System.IO;
using System.Security.Cryptography;
using Clipsa;

// Explicit network check for a published release. Downloads are hashed in memory,
// never saved to an executable file or installed. Normal CI remains offline here.
internal static class PublicChannelChecks
{
    public static async Task RunAsync()
    {
        using var client = new HttpClient { Timeout = TimeSpan.FromMinutes(10) };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Clipsa-PublicChannelChecks/1");
        client.DefaultRequestHeaders.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        var manifest = await client.GetStringAsync(UpdateService.DefaultFeed);
        var installed = UpdateService.ParseManifest(manifest, false);
        var portable = UpdateService.ParseManifest(manifest, true);
        if (!installed.SignatureVerified || !portable.SignatureVerified || installed.Version != portable.Version)
            throw new Exception("Published channel must have trusted signatures and matching versions.");
        if (!installed.DownloadUrl.EndsWith("-setup.exe") || !portable.DownloadUrl.EndsWith("-portable.zip"))
            throw new Exception("The channel selected the wrong package mode.");
        foreach (var release in new[] { installed, portable })
        {
            if (!release.DownloadUrl.StartsWith("https://github.com/peesejota/clipsa/releases/download/", StringComparison.Ordinal))
                throw new Exception("Unexpected official release destination.");
            using var response = await client.GetAsync(release.DownloadUrl, HttpCompletionOption.ResponseHeadersRead);
            response.EnsureSuccessStatusCode();
            if (response.RequestMessage?.RequestUri?.Scheme != "https") throw new Exception("Unsafe download redirect.");
            await using var input = await response.Content.ReadAsStreamAsync();
            using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[81920]; long total = 0;
            for (var read = await input.ReadAsync(buffer); read > 0; read = await input.ReadAsync(buffer))
            {
                total += read;
                if (total > 500_000_000) throw new Exception("Package exceeds the application limit.");
                hash.AppendData(buffer, 0, read);
            }
            if (!Convert.ToHexString(hash.GetHashAndReset()).Equals(release.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new Exception("The public download hash differs from the signed channel.");
            Console.WriteLine($"Published {release.Version}: {Path.GetFileName(new Uri(release.DownloadUrl).AbsolutePath)}, {total} bytes, trusted signature and SHA256 OK.");
        }
        Console.WriteLine("Public channel verified. Downloaded code was neither saved nor executed.");
    }
}
