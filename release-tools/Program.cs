using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text;
using System.IO.Compression;

const string KeyName = "Clipsa.UpdateSigning.2026.v1";
const string Repository = "peesejota/clipsa";
var jsonOptions = new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
if (args.Length == 0) throw new ArgumentException("Commands: init-key | sign <artifacts> <version> <tag> | publish <artifacts> <version> <tag> | backup-key <encrypted-file>");
if (args[0] == "fetch")
{
    var destination = Path.GetFullPath(args[2]);
    if (Directory.Exists(destination) && Directory.EnumerateFileSystemEntries(destination).Any()) throw new IOException("Destination must be empty.");
    using var client = await GitHubClient();
    using var listing = JsonDocument.Parse(await client.GetStringAsync($"https://api.github.com/repos/{Repository}/actions/runs/{long.Parse(args[1])}/artifacts"));
    var artifact = listing.RootElement.GetProperty("artifacts").EnumerateArray().Single(a => a.GetProperty("name").GetString()!.StartsWith("Clipsa-") && !a.GetProperty("expired").GetBoolean());
    var id = artifact.GetProperty("id").GetInt64();
    var temp = Path.Combine(Path.GetTempPath(), "Clipsa-artifact-" + Guid.NewGuid().ToString("N") + ".zip");
    try
    {
        using var response = await client.GetAsync($"https://api.github.com/repos/{Repository}/actions/artifacts/{id}/zip", HttpCompletionOption.ResponseHeadersRead); response.EnsureSuccessStatusCode();
        await using (var output = File.Create(temp)) await response.Content.CopyToAsync(output);
        if (artifact.TryGetProperty("digest", out var digest) && digest.GetString() is { } expected && expected.StartsWith("sha256:"))
        { using var input = File.OpenRead(temp); if (!Convert.ToHexString(SHA256.HashData(input)).Equals(expected[7..], StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Artifact digest mismatch."); }
        ZipFile.ExtractToDirectory(temp, destination, overwriteFiles: false);
        Console.WriteLine("Verified CI artifact downloaded and extracted.");
    }
    finally { if (File.Exists(temp)) File.Delete(temp); }
    return;
}
if (args[0] == "init-key")
{
    if (!CngKey.Exists(KeyName))
    {
        var options = new CngKeyCreationParameters { ExportPolicy = CngExportPolicies.AllowExport, KeyUsage = CngKeyUsages.Signing, Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider };
        options.Parameters.Add(new CngProperty("Length", BitConverter.GetBytes(3072), CngPropertyOptions.None));
        using var created = CngKey.Create(CngAlgorithm.Rsa, KeyName, options);
    }
    using var key = new RSACng(CngKey.Open(KeyName));
    Console.WriteLine(Convert.ToBase64String(key.ExportSubjectPublicKeyInfo())); return;
}
if (args[0] == "backup-key")
{
    Console.Write("Password for encrypted backup (not echoed): ");
    var password = new StringBuilder();
    for (var k = Console.ReadKey(true); k.Key != ConsoleKey.Enter; k = Console.ReadKey(true))
        if (k.Key == ConsoleKey.Backspace) { if (password.Length > 0) password.Length--; } else if (!char.IsControl(k.KeyChar)) password.Append(k.KeyChar);
    if (password.Length < 16) throw new ArgumentException("Use at least 16 characters.");
    using var key = new RSACng(CngKey.Open(KeyName));
    if (File.Exists(args[1])) throw new IOException("Backup already exists.");
    File.WriteAllBytes(args[1], key.ExportEncryptedPkcs8PrivateKey(password.ToString(), new PbeParameters(PbeEncryptionAlgorithm.Aes256Cbc, HashAlgorithmName.SHA256, 200_000)));
    Console.WriteLine("Encrypted backup written. Keep it outside the repository."); return;
}
if (args[0] == "sign-json")
{
    var bytes = File.ReadAllBytes(args[1]);
    using var key = new RSACng(CngKey.Open(KeyName));
    File.WriteAllBytes(args[2], JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(bytes), signature = Convert.ToBase64String(key.SignData(bytes, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) }, jsonOptions));
    Console.WriteLine("JSON signed; no private key exported."); return;
}
if (args.Length != 4 || args[0] is not ("sign" or "publish")) throw new ArgumentException("Expected sign/publish <artifacts> <version> <tag>.");
var folder = Path.GetFullPath(args[1]); var version = Version.Parse(args[2]); var tag = args[3];
if (!System.Text.RegularExpressions.Regex.IsMatch(tag, "^v" + System.Text.RegularExpressions.Regex.Escape(args[2]) + "(-beta\\.\\d+)?$")) throw new ArgumentException("Tag/version mismatch.");
var installerName = $"Clipsa-{args[2]}-win-x64-setup.exe";
var portableName = $"Clipsa-{args[2]}-win-x64-portable.zip";
using var buildInfo = JsonDocument.Parse(File.ReadAllText(Path.Combine(folder, "release.json")));
if (buildInfo.RootElement.GetProperty("version").GetString() != args[2]) throw new InvalidDataException("Artifact/project version mismatch.");
var sourceCommit = buildInfo.RootElement.GetProperty("commit").GetString()!;
if (!System.Text.RegularExpressions.Regex.IsMatch(sourceCommit, "^[a-fA-F0-9]{40}$")) throw new InvalidDataException("Missing source commit in CI artifacts.");
var allowedFiles = new HashSet<string>(new[] { installerName, portableName, "update-manifest.json", "update-manifest-portable.json", "release.json", "SHA256SUMS.txt", "LEEME.md" }, StringComparer.OrdinalIgnoreCase);
if (Directory.GetFiles(folder).Any(path => !allowedFiles.Contains(Path.GetFileName(path)))) throw new InvalidDataException("Unexpected files in artifacts folder; refusing to sign or upload unrelated data.");
string Hash(string file) { using var stream = File.OpenRead(Path.Combine(folder, file)); return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant(); }
var payload = JsonSerializer.SerializeToUtf8Bytes(new { version = args[2], downloadUrl = $"https://github.com/{Repository}/releases/download/{tag}/{installerName}", sha256 = Hash(installerName), portableDownloadUrl = $"https://github.com/{Repository}/releases/download/{tag}/{portableName}", portableSha256 = Hash(portableName), releaseNotesUrl = $"https://github.com/{Repository}/releases/tag/{tag}", channel = tag.Contains("-beta.") ? "beta" : "stable" }, jsonOptions);
using var signingKey = new RSACng(CngKey.Open(KeyName));
if (!File.ReadAllText("Clipsa/UpdateTrust.cs").Contains(Convert.ToBase64String(signingKey.ExportSubjectPublicKeyInfo()), StringComparison.Ordinal))
    throw new InvalidOperationException("The signing key does not match the public key in the application source.");
var signed = JsonSerializer.SerializeToUtf8Bytes(new { payload = Convert.ToBase64String(payload), signature = Convert.ToBase64String(signingKey.SignData(payload, HashAlgorithmName.SHA256, RSASignaturePadding.Pss)) }, jsonOptions);
File.WriteAllBytes(Path.Combine(folder, "update-manifest.json"), signed);
// Same signed payload includes distinct portable/installer assets; clients select their own mode.
File.WriteAllBytes(Path.Combine(folder, "update-manifest-portable.json"), signed);
var releaseMetadata = Path.Combine(folder, "release.json");
if (File.Exists(releaseMetadata))
{
    var node = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllText(releaseMetadata))!;
    node["manifestSigned"] = true;
    node["authenticodeSigned"] = false;
    node["updatePublicKeySha256"] = Convert.ToHexString(SHA256.HashData(signingKey.ExportSubjectPublicKeyInfo())).ToLowerInvariant();
    File.WriteAllText(releaseMetadata, node.ToJsonString(jsonOptions));
}
var checksums = Directory.GetFiles(folder).Where(p => Path.GetFileName(p) != "SHA256SUMS.txt" && Path.GetExtension(p) is ".exe" or ".zip" or ".json" or ".md").OrderBy(Path.GetFileName).Select(p => Hash(Path.GetFileName(p)) + "  " + Path.GetFileName(p));
File.WriteAllLines(Path.Combine(folder, "SHA256SUMS.txt"), checksums);
Console.WriteLine("Release manifests signed; SHA256 inventory regenerated. Private key remains in Windows CNG.");
if (args[0] == "sign") return;
using var http = await GitHubClient();
var api = $"https://api.github.com/repos/{Repository}";
using (var history = JsonDocument.Parse(await http.GetStringAsync(api + "/releases?per_page=100")))
    if (history.RootElement.EnumerateArray().Any(r => !r.GetProperty("draft").GetBoolean() && System.Text.RegularExpressions.Regex.Match(r.GetProperty("tag_name").GetString() ?? "", @"^v(\d+\.\d+\.\d+)").Groups[1].Value == args[2]))
        throw new InvalidOperationException("This application version already has a release. Increment the project version so installed clients can detect the update.");
using var existing = await http.GetAsync($"{api}/releases/tags/{tag}");
long releaseId;
if (existing.IsSuccessStatusCode)
{
    using var draft = JsonDocument.Parse(await existing.Content.ReadAsStringAsync());
    if (!draft.RootElement.GetProperty("draft").GetBoolean()) throw new InvalidOperationException("Refusing to overwrite a public release.");
    releaseId = draft.RootElement.GetProperty("id").GetInt64();
    foreach (var asset in draft.RootElement.GetProperty("assets").EnumerateArray())
        if (!allowedFiles.Contains(asset.GetProperty("name").GetString()!)) throw new InvalidDataException("Unexpected assets in the draft; review before publishing.");
    foreach (var asset in draft.RootElement.GetProperty("assets").EnumerateArray())
    { using var removed = await http.DeleteAsync($"{api}/releases/assets/{asset.GetProperty("id").GetInt64()}"); removed.EnsureSuccessStatusCode(); }
    using var retarget = await http.PatchAsJsonAsync($"{api}/releases/{releaseId}", new { target_commitish = sourceCommit }); retarget.EnsureSuccessStatusCode();
}
else
{
    if (existing.StatusCode != System.Net.HttpStatusCode.NotFound) existing.EnsureSuccessStatusCode();
    using var created = await http.PostAsJsonAsync(api + "/releases", new { tag_name = tag, target_commitish = sourceCommit, name = "Clipsa " + tag, draft = true, prerelease = tag.Contains("-beta."), body = "Windows x64 · Instalador y portable autocontenidos. Manifiesto firmado y SHA256 verificados por la app. Beta sin certificado Authenticode de editor; no desactives tu antivirus. Guarda las capturas antes de actualizar." });
    created.EnsureSuccessStatusCode(); using var data = JsonDocument.Parse(await created.Content.ReadAsStringAsync()); releaseId = data.RootElement.GetProperty("id").GetInt64();
}
foreach (var file in Directory.GetFiles(folder).Where(p => Path.GetExtension(p) is ".exe" or ".zip" or ".json" or ".txt" or ".md"))
{
    await using var stream = File.OpenRead(file); using var content = new StreamContent(stream); content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
    using var upload = await http.PostAsync($"https://uploads.github.com/repos/{Repository}/releases/{releaseId}/assets?name={Uri.EscapeDataString(Path.GetFileName(file))}", content);
    upload.EnsureSuccessStatusCode(); Console.WriteLine("Uploaded: " + Path.GetFileName(file));
}
using var published = await http.PatchAsJsonAsync($"{api}/releases/{releaseId}", new { draft = false, prerelease = tag.Contains("-beta."), make_latest = tag.Contains("-beta.") ? "false" : "true" });
published.EnsureSuccessStatusCode(); Console.WriteLine($"Published: https://github.com/{Repository}/releases/tag/{tag}");

static async Task<HttpClient> GitHubClient()
{
    // GCM credentials stay in memory; never print tokens or put them in process arguments.
    var start = new ProcessStartInfo("git", "credential fill") { RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true };
    using var process = Process.Start(start)!;
    await process.StandardInput.WriteAsync("protocol=https\nhost=github.com\n\n"); process.StandardInput.Close();
    var text = await process.StandardOutput.ReadToEndAsync(); await process.WaitForExitAsync();
    var token = text.Split('\n').FirstOrDefault(line => line.StartsWith("password="))?[9..].Trim();
    if (process.ExitCode != 0 || string.IsNullOrEmpty(token)) throw new InvalidOperationException("GitHub credentials unavailable.");
    var client = new HttpClient { Timeout = TimeSpan.FromMinutes(20) };
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
    client.DefaultRequestHeaders.UserAgent.ParseAdd("Clipsa-ReleaseTools/1"); client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    return client;
}
