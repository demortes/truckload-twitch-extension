using Xunit;
using System.IO.Compression;
using System.Net;
using Truckload.Bridge.Updates;

namespace Truckload.Bridge.Tests;

public class UpdateCheckerTests
{
    private const string Rid = "win-x64";

    private static string Release(string tag, bool prerelease = false, string? assetName = null) => $$"""
        {
          "tag_name": "{{tag}}",
          "html_url": "https://github.com/demortes/truckload-twitch-extension/releases/tag/{{tag}}",
          "prerelease": {{(prerelease ? "true" : "false")}},
          "draft": false,
          "assets": [
            { "name": "{{assetName ?? $"Truckload.Bridge-{Rid}-{tag}.zip"}}", "browser_download_url": "https://github.com/x/y/releases/download/{{tag}}/asset.zip" },
            { "name": "Truckload.Bridge-linux-x64-{{tag}}.zip", "browser_download_url": "https://github.com/x/y/releases/download/{{tag}}/linux.zip" }
          ]
        }
        """;

    private static UpdateChecker Checker(FakeHttpMessageHandler handler) =>
        new(new HttpClient(handler), "https://example.test/latest");

    [Theory]
    [InlineData("1.6.3", "1.6.2", true)]
    [InlineData("1.7.0", "1.6.9", true)]
    [InlineData("v2.0.0", "1.9.9", true)]
    [InlineData("1.6.2", "1.6.2", false)]
    [InlineData("1.6.1", "1.6.2", false)]
    [InlineData("1.6.3", "dev", false)]
    [InlineData("not-a-version", "1.6.2", false)]
    public void IsNewer_ComparesReleaseVersions(string latest, string current, bool expected) =>
        Assert.Equal(expected, UpdateChecker.IsNewer(latest, current));

    [Fact]
    public async Task Check_NewerRelease_ReturnsAssetForThisPlatform()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Release("v1.6.3"));

        var update = await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None);

        Assert.NotNull(update);
        Assert.Equal("1.6.3", update.Version);
        Assert.Equal("Truckload.Bridge-win-x64-v1.6.3.zip", update.AssetName);
        Assert.EndsWith("/asset.zip", update.AssetUrl);
    }

    [Fact]
    public async Task Check_SameVersion_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Release("v1.6.2"));

        Assert.Null(await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None));
    }

    [Fact]
    public async Task Check_DevBuild_DoesNotEvenCallGitHub()
    {
        var handler = new FakeHttpMessageHandler();

        Assert.Null(await Checker(handler).CheckAsync("dev", Rid, CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Check_Prerelease_IsNotOffered()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Release("v9.0.0", prerelease: true));

        Assert.Null(await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None));
    }

    [Fact]
    public async Task Check_NewerReleaseWithoutMatchingAsset_ReturnsInfoWithoutDownload()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, Release("v1.6.3", assetName: "something-else.zip"));

        var update = await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None);

        Assert.NotNull(update);
        Assert.Null(update.AssetUrl);
    }

    [Theory]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Check_HttpFailure_ReturnsNull(HttpStatusCode status)
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(status, "{}");

        Assert.Null(await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None));
    }

    [Fact]
    public async Task Check_MalformedJson_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.EnqueueJson(HttpStatusCode.OK, "not json");

        Assert.Null(await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None));
    }

    [Fact]
    public async Task Check_NetworkError_ReturnsNull()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(() => throw new HttpRequestException("offline"));

        Assert.Null(await Checker(handler).CheckAsync("1.6.2", Rid, CancellationToken.None));
    }
}

public sealed class SelfUpdaterTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("truckload-update-").FullName;
    private readonly List<string> _log = new();

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    private string ExePath => Path.Combine(_dir, "Truckload.Bridge.test.exe");

    private static byte[] Zip(string entryName, string content)
    {
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, leaveOpen: true))
        {
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write(content);
        }
        return ms.ToArray();
    }

    private static UpdateInfo Update(string url = "https://github.com/x/y/releases/download/v1.6.3/a.zip") =>
        new("1.6.3", "https://github.com/x/y/releases/tag/v1.6.3", "a.zip", url);

    private SelfUpdater Updater(FakeHttpMessageHandler handler) => new(new HttpClient(handler), _log.Add);

    private static void EnqueueBytes(FakeHttpMessageHandler handler, byte[] bytes) =>
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });

    [Theory]
    [InlineData("https://github.com/x/y/a.zip", true)]
    [InlineData("https://objects.githubusercontent.com/a.zip", true)]
    [InlineData("http://github.com/x/y/a.zip", false)]
    [InlineData("https://evil.example/a.zip", false)]
    [InlineData("https://github.com.evil.example/a.zip", false)]
    [InlineData("not a url", false)]
    public void IsAllowedDownloadUrl_OnlyHttpsGitHub(string url, bool expected) =>
        Assert.Equal(expected, SelfUpdater.IsAllowedDownloadUrl(url));

    [Fact]
    public async Task Apply_ReplacesExeAndKeepsOldOneAside()
    {
        File.WriteAllText(ExePath, "old build");
        var handler = new FakeHttpMessageHandler();
        EnqueueBytes(handler, Zip(Path.GetFileName(ExePath), "new build"));

        var result = await Updater(handler).ApplyAsync(Update(), ExePath, CancellationToken.None);

        Assert.Equal(SelfUpdateResult.Updated, result);
        Assert.Equal("new build", File.ReadAllText(ExePath));
        Assert.Equal("old build", File.ReadAllText(ExePath + SelfUpdater.OldSuffix));
        Assert.False(File.Exists(ExePath + ".new"));
    }

    [Fact]
    public async Task Apply_ZipWithoutTheExe_LeavesInstalledExeUntouched()
    {
        File.WriteAllText(ExePath, "old build");
        var handler = new FakeHttpMessageHandler();
        EnqueueBytes(handler, Zip("README.md", "hello"));

        var result = await Updater(handler).ApplyAsync(Update(), ExePath, CancellationToken.None);

        Assert.Equal(SelfUpdateResult.DownloadFailed, result);
        Assert.Equal("old build", File.ReadAllText(ExePath));
        Assert.False(File.Exists(ExePath + SelfUpdater.OldSuffix));
    }

    [Fact]
    public async Task Apply_DownloadError_LeavesInstalledExeUntouched()
    {
        File.WriteAllText(ExePath, "old build");
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.NotFound));

        var result = await Updater(handler).ApplyAsync(Update(), ExePath, CancellationToken.None);

        Assert.Equal(SelfUpdateResult.DownloadFailed, result);
        Assert.Equal("old build", File.ReadAllText(ExePath));
    }

    [Fact]
    public async Task Apply_UntrustedHost_IsRefusedWithoutAnyRequest()
    {
        File.WriteAllText(ExePath, "old build");
        var handler = new FakeHttpMessageHandler();

        var result = await Updater(handler).ApplyAsync(Update("https://evil.example/a.zip"), ExePath, CancellationToken.None);

        Assert.Equal(SelfUpdateResult.DownloadFailed, result);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Apply_NoAssetOrNoExePath_IsUnsupported()
    {
        var handler = new FakeHttpMessageHandler();

        Assert.Equal(SelfUpdateResult.Unsupported,
            await Updater(handler).ApplyAsync(Update() with { AssetUrl = null }, ExePath, CancellationToken.None));
        Assert.Equal(SelfUpdateResult.Unsupported,
            await Updater(handler).ApplyAsync(Update(), null, CancellationToken.None));
        Assert.Equal(SelfUpdateResult.Unsupported,
            await Updater(handler).ApplyAsync(Update(), Path.Combine(_dir, "dotnet.exe"), CancellationToken.None));
    }

    [Fact]
    public void CleanUpOldExe_RemovesLeftoverFromPreviousUpdate()
    {
        File.WriteAllText(ExePath + SelfUpdater.OldSuffix, "leftover");

        SelfUpdater.CleanUpOldExe(ExePath);

        Assert.False(File.Exists(ExePath + SelfUpdater.OldSuffix));
    }
}
