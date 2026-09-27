using Xunit;

namespace Truckload.Bridge.Tests;

public class BridgeOptionsTests : IDisposable
{
    private readonly string _tempDir;

    public BridgeOptionsTests()
    {
        _tempDir = Directory.CreateTempSubdirectory("truckload-bridge-tests-").FullName;
    }

    public void Dispose() => Directory.Delete(_tempDir, recursive: true);

    [Fact]
    public void Resolve_UsesDefaults_WhenNothingElseIsProvided()
    {
        var options = BridgeOptions.Resolve(Array.Empty<string>(), _tempDir);
        Assert.Equal(1000, options.PollMs);
        Assert.Equal(30, options.HeartbeatSeconds);
        Assert.Equal("auto", options.Units);
    }

    [Fact]
    public void Resolve_CliArgsOverrideConfigFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, BridgeOptions.ConfigFileName), """{"IngestUrl":"https://from-file/api/ingest","IngestKey":"file-key"}""");

        var options = BridgeOptions.Resolve(new[] { "--key", "cli-key" }, _tempDir);

        Assert.Equal("https://from-file/api/ingest", options.IngestUrl); // kept from file
        Assert.Equal("cli-key", options.IngestKey); // overridden by CLI
    }

    [Fact]
    public void Resolve_PollMsHasAOneSecondFloor()
    {
        var options = BridgeOptions.Resolve(new[] { "--poll-ms", "100" }, _tempDir);
        Assert.Equal(1000, options.PollMs);
    }

    [Fact]
    public void Resolve_DemoFlagAndDemoGame()
    {
        var options = BridgeOptions.Resolve(new[] { "--demo", "--demo-game", "ets2" }, _tempDir);
        Assert.True(options.Demo);
        Assert.Equal("ets2", options.DemoGame);
    }

    [Fact]
    public void Resolve_HelpFlag()
    {
        var options = BridgeOptions.Resolve(new[] { "--help" }, _tempDir);
        Assert.True(options.Help);
    }
}
