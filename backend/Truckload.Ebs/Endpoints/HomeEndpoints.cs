using System.Net;
using Microsoft.Extensions.Options;
using Truckload.Ebs.Configuration;

namespace Truckload.Ebs.Endpoints;

public static class HomeEndpoints
{
    public static void MapHomeEndpoints(this WebApplication app)
    {
        app.MapGet("/", (HttpContext context, IOptions<SiteSettings> site) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src 'none'; style-src 'unsafe-inline'; base-uri 'none'; form-action 'none'";
            return Results.Content(RenderPage(context.Request, site.Value), "text/html; charset=utf-8");
        }).ExcludeFromDescription();
    }

    private static string RenderPage(HttpRequest request, SiteSettings site)
    {
        // Behind a TLS-terminating proxy (Caddy) the request scheme is http; trust the forwarded proto for display only.
        var scheme = request.Headers["X-Forwarded-Proto"].FirstOrDefault() is { Length: > 0 } proto && proto is "http" or "https"
            ? proto
            : request.Scheme;
        var ingestUrl = $"{scheme}://{request.Host}/api/ingest";

        static string E(string s) => WebUtility.HtmlEncode(s);

        return $$"""
<!doctype html>
<html lang="en">
<head>
<meta charset="utf-8">
<meta name="viewport" content="width=device-width, initial-scale=1">
<title>Truckload</title>
<style>
  :root { color-scheme: light dark; --bg:#fff; --fg:#1b1b1f; --muted:#5f6368; --card:#f4f4f7; --accent:#9146ff; }
  @media (prefers-color-scheme: dark) { :root { --bg:#0e0e10; --fg:#efeff1; --muted:#adadb8; --card:#18181b; } }
  body { margin:0; background:var(--bg); color:var(--fg); font:16px/1.6 system-ui, sans-serif; }
  main { max-width:44rem; margin:0 auto; padding:2.5rem 1rem 4rem; }
  h1 { margin:0 0 .25rem; } p.lead { color:var(--muted); margin-top:0; }
  ol { padding-left:1.25rem; } li { margin:1rem 0; }
  code { background:var(--card); padding:.15rem .4rem; border-radius:4px; word-break:break-all; }
  pre { background:var(--card); padding:.75rem 1rem; border-radius:8px; overflow-x:auto; }
  a { color:var(--accent); } .btn { display:inline-block; background:var(--accent); color:#fff; padding:.5rem 1rem; border-radius:6px; text-decoration:none; font-weight:600; }
  footer { margin-top:3rem; color:var(--muted); font-size:.875rem; }
</style>
</head>
<body>
<main>
  <h1>Truckload</h1>
  <p class="lead">Live job and truck stats from American Truck Simulator and Euro Truck Simulator 2, shown to your viewers right on your Twitch stream.</p>
  <p>This is the Truckload backend service. Streamers set up in three steps:</p>
  <ol>
    <li><strong>Install the extension.</strong> Add it to your channel and activate the Panel and/or Video Overlay.<br>
        <a class="btn" href="{{E(site.ExtensionUrl)}}">Get the Twitch extension</a></li>
    <li><strong>Generate your ingest key.</strong> Open the extension's <em>Config</em> page from your Creator Dashboard and click <em>Generate API Key</em>. Keep the key secret.</li>
    <li><strong>Run the Bridge on your PC.</strong> <a href="{{E(site.BridgeDownloadUrl)}}">Download <code>Truckload.Bridge.exe</code></a> (Windows 10/11), then run:
        <pre>Truckload.Bridge.exe --ingest-url {{E(ingestUrl)}} --key &lt;your-key&gt; --save</pre></li>
  </ol>
  <p>Start ATS or ETS2 with an active job; the Config page should show "Receiving data" within a few seconds. Full instructions and troubleshooting: <a href="{{E(site.DocsUrl)}}">streamer setup guide</a>.</p>
  <footer>API status: <a href="/api/health/live">/api/health/live</a></footer>
</main>
</body>
</html>
""";
    }
}
