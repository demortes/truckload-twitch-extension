using Truckload.Bridge;
using Truckload.Bridge.Demo;
using Truckload.Bridge.Funbit;
using Truckload.Bridge.Sending;
using Truckload.Bridge.TruckTel;
using Truckload.Bridge.Updates;

var baseDirectory = AppContext.BaseDirectory;

BridgeOptions options;
try
{
    options = BridgeOptions.Resolve(args, baseDirectory);
}
catch (ArgumentException ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}

if (options.Help)
{
    Console.WriteLine(BridgeOptions.HelpText);
    return 0;
}

if (Environment.ProcessPath is { } processPath)
    SelfUpdater.CleanUpOldExe(processPath);

if (options.Update)
    return await RunUpdateAsync();

if (!options.DryRun && (string.IsNullOrWhiteSpace(options.IngestUrl) || string.IsNullOrWhiteSpace(options.IngestKey)))
{
    Console.Error.WriteLine("Missing --ingest-url and/or --key (or TRUCKLOAD_INGEST_URL / TRUCKLOAD_INGEST_KEY). Use --help for usage, or --dry-run to test without a backend.");
    return 1;
}

if (options.Save)
{
    options.SaveTo(baseDirectory);
    Console.WriteLine($"Saved configuration to {Path.Combine(baseDirectory, BridgeOptions.ConfigFileName)}");
}

Console.WriteLine($"Truckload Bridge v{BridgeVersion.Current}");
Console.WriteLine($"  Telemetry source : {(options.Demo ? $"demo ({options.DemoGame})" : $"{options.ResolvedSource} at {options.ResolvedTelemetryUrl}")}");
Console.WriteLine($"  Ingest URL       : {(options.DryRun ? "(dry run, not sending)" : options.IngestUrl)}");
Console.WriteLine($"  Ingest key       : {(options.DryRun ? "(none)" : Mask(options.IngestKey))}");
Console.WriteLine($"  Poll interval    : {options.PollMs} ms   Heartbeat: {options.HeartbeatSeconds}s   Units: {options.Units}");
Console.WriteLine("Press Ctrl+C to stop.");

using var cts = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) =>
{
    e.Cancel = true;
    cts.Cancel();
};

var time = TimeProvider.System;
using var httpClient = new HttpClient();

// The local telemetry servers (TruckTel in particular) close idle keep-alive connections, which makes a
// pooled client fail on every few requests. Loopback connections are cheap, so never reuse them.
using var telemetryHttpClient = new HttpClient(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.Zero });

ITelemetrySource source = options.Demo
    ? new DemoSource(time, options.DemoGame)
    : options.ResolvedSource switch
    {
        BridgeOptions.SourceFunbit => new FunbitSource(telemetryHttpClient, options.ResolvedTelemetryUrl),
        BridgeOptions.SourceTrucktel => new TruckTelSource(telemetryHttpClient, options.ResolvedTelemetryUrl),
        var other => throw new ArgumentException($"Unknown --source '{other}'. Use 'trucktel' or 'funbit'."),
    };

IngestSender? sender = options.DryRun
    ? null
    : new IngestSender(httpClient, options.IngestUrl!, options.IngestKey!);

// Offline or dry-run setups skip the check, and a failed check never delays or stops startup.
if (!options.DryRun && !options.Demo && !options.NoUpdateCheck)
{
    var available = await new UpdateChecker(httpClient).CheckAsync(BridgeVersion.Current, UpdateChecker.CurrentRuntimeId(), cts.Token);
    if (available is not null)
        Console.WriteLine($"A newer version is available: v{available.Version} (you have v{BridgeVersion.Current}). Run with --update to install it, or see {available.ReleaseUrl}");
}

var runner = new BridgeRunner(source, sender, options, time, Console.WriteLine);

var state = await runner.RunAsync(cts.Token);

if (state.ExitRequested == BridgeExitReason.InvalidKey)
    return 2;

Console.WriteLine("Stopped.");
return 0;

static string Mask(string? key)
{
    if (string.IsNullOrEmpty(key))
        return "(none)";
    return key.Length <= 8 ? "****" : $"{key[..4]}...{key[^4..]}";
}

async Task<int> RunUpdateAsync()
{
    using var http = new HttpClient();
    var runtimeId = UpdateChecker.CurrentRuntimeId();
    Console.WriteLine($"Truckload Bridge v{BridgeVersion.Current}: checking for updates...");

    var update = await new UpdateChecker(http).CheckAsync(BridgeVersion.Current, runtimeId, CancellationToken.None);
    if (update is null)
    {
        Console.WriteLine("You are up to date (or the update check could not reach GitHub).");
        return 0;
    }

    var result = await new SelfUpdater(http, Console.WriteLine).ApplyAsync(update, Environment.ProcessPath, CancellationToken.None);
    switch (result)
    {
        case SelfUpdateResult.Updated:
            Console.WriteLine($"Updated to v{update.Version}. Start the bridge again to use it.");
            return 0;
        case SelfUpdateResult.Unsupported:
            Console.WriteLine($"v{update.Version} is available but can't be installed automatically here. Download it from {update.ReleaseUrl}");
            return 0;
        default:
            Console.WriteLine($"Update to v{update.Version} failed. You can download it manually from {update.ReleaseUrl}");
            return 3;
    }
}
