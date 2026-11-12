using System.Net;
using System.Net.Http;
using System.Reflection;
using Clipsa;

internal static class UpdateChecks
{
    public static void Run()
    {
        foreach (var suffix in new[] { "", "/", "/releases", "/releases/latest" })
            Require(UpdateService.ResolveFeed("https://github.com/peesejota/clipsa" + suffix).AbsoluteUri ==
                UpdateService.DefaultFeed, "GitHub repository resolution");
        Require(UpdateService.ResolveFeed("https://example.test/feed.json?channel=stable").AbsoluteUri == "https://example.test/feed.json?channel=stable", "custom HTTPS feed preserved");
        foreach (var invalid in new[] { "http://example.test/feed.json", "https://user:secret@example.test/feed.json", "file:///C:/feed.json", "not a URL" })
        {
            try { UpdateService.ResolveFeed(invalid); throw new Exception("Unsafe feed accepted."); }
            catch (ArgumentException) { }
        }
        using var safe = new HttpClient(new RedirectHandler("https://downloads.example.test/feed.json"));
        using var response = Request(safe).GetAwaiter().GetResult();
        Require(response.StatusCode == HttpStatusCode.OK, "HTTPS redirect accepted");
        foreach (var unsafeUrl in new[] { "http://downloads.example.test/feed.json", "https://user:secret@example.test/feed.json" })
        {
            using var client = new HttpClient(new RedirectHandler(unsafeUrl));
            try { Request(client).GetAwaiter().GetResult(); throw new Exception("Unsafe redirect accepted."); }
            catch (System.IO.InvalidDataException) { }
        }
        Console.WriteLine("GitHub URL resolution, HTTPS feeds, credential rejection and safe redirects: OK (no network or UI).");
        UpdaterChecks.Run();
    }
    private static Task<HttpResponseMessage> Request(HttpClient client) =>
        (Task<HttpResponseMessage>)typeof(UpdateService).GetMethod("GetHttpsAsync", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { client, new Uri("https://example.test/feed.json"), CancellationToken.None })!;
    private static void Require(bool condition, string name) { if (!condition) throw new Exception(name); }
    private sealed class RedirectHandler(string destination) : HttpMessageHandler
    {
        private int calls;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellation)
        {
            if (++calls > 1) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
            var response = new HttpResponseMessage(HttpStatusCode.Redirect);
            response.Headers.Location = new Uri(destination);
            return Task.FromResult(response);
        }
    }
}
