using System.Runtime.InteropServices;
using System.Text.Json;

namespace Truckload.Bridge.Updates;

/// <summary>A newer release than the running bridge, and the zip asset for this platform if the release has one.</summary>
public sealed record UpdateInfo(string Version, string ReleaseUrl, string? AssetName, string? AssetUrl);

/// <summary>
/// Asks the GitHub releases API for the latest release. Every failure (offline, rate limited,
/// unexpected JSON) yields <c>null</c>: an update check must never stop the bridge from starting.
/// </summary>
public sealed class UpdateChecker
{
    public const string LatestReleaseUrl = "https://api.github.com/repos/demortes/truckload-twitch-extension/releases/latest";

    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(5);

    private readonly HttpClient _http;
    private readonly string _url;

    public UpdateChecker(HttpClient http, string url = LatestReleaseUrl)
    {
        _http = http;
        _url = url;
    }

    /// <summary>The release asset platform id the CI publishes (win-x64, linux-x64, osx-x64), or null if unsupported.</summary>
    public static string? CurrentRuntimeId()
    {
        if (RuntimeInformation.ProcessArchitecture != Architecture.X64)
            return null;
        if (OperatingSystem.IsWindows()) return "win-x64";
        if (OperatingSystem.IsLinux()) return "linux-x64";
        if (OperatingSystem.IsMacOS()) return "osx-x64";
        return null;
    }

    public async Task<UpdateInfo?> CheckAsync(string currentVersion, string? runtimeId, CancellationToken cancellationToken)
    {
        // Dev builds ("dev", or anything that is not a release version) have nothing to compare against.
        if (!TryParseVersion(currentVersion, out _))
            return null;

        try
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(Timeout);

            using var request = new HttpRequestMessage(HttpMethod.Get, _url);
            request.Headers.UserAgent.ParseAdd($"Truckload-Bridge/{currentVersion}");
            request.Headers.Accept.ParseAdd("application/vnd.github+json");

            using var response = await _http.SendAsync(request, timeout.Token);
            if (!response.IsSuccessStatusCode)
                return null;

            using var doc = await JsonDocument.ParseAsync(await response.Content.ReadAsStreamAsync(timeout.Token), cancellationToken: timeout.Token);
            var root = doc.RootElement;

            // Pre-releases and drafts are never offered to streamers.
            if (root.TryGetProperty("prerelease", out var pre) && pre.ValueKind == JsonValueKind.True)
                return null;
            if (root.TryGetProperty("draft", out var draft) && draft.ValueKind == JsonValueKind.True)
                return null;

            var tag = root.GetProperty("tag_name").GetString();
            if (tag is null)
                return null;
            var version = tag.TrimStart('v', 'V');
            if (!IsNewer(version, currentVersion))
                return null;

            var releaseUrl = root.TryGetProperty("html_url", out var html) ? html.GetString() ?? "" : "";
            var expectedAsset = runtimeId is null ? null : $"Truckload.Bridge-{runtimeId}-v{version}.zip";

            string? assetName = null, assetUrl = null;
            if (expectedAsset is not null && root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
            {
                foreach (var asset in assets.EnumerateArray())
                {
                    if (asset.TryGetProperty("name", out var n) && n.GetString() == expectedAsset
                        && asset.TryGetProperty("browser_download_url", out var u))
                    {
                        assetName = expectedAsset;
                        assetUrl = u.GetString();
                        break;
                    }
                }
            }

            return new UpdateInfo(version, releaseUrl, assetName, assetUrl);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or KeyNotFoundException or InvalidOperationException)
        {
            return null;
        }
    }

    /// <summary>True when <paramref name="latest"/> is a strictly higher release version than <paramref name="current"/>.</summary>
    public static bool IsNewer(string latest, string current) =>
        TryParseVersion(latest, out var l) && TryParseVersion(current, out var c) && l > c;

    private static bool TryParseVersion(string value, out Version version)
    {
        // "1.6.3", "v1.6.3", or "1.6.3-rc.1" (the suffix is ignored; pre-release tags are filtered earlier).
        var core = value.TrimStart('v', 'V');
        var dash = core.IndexOf('-');
        if (dash > 0)
            core = core[..dash];
        return Version.TryParse(core, out version!);
    }
}
