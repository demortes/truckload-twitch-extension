using Truckload.Bridge;
using Truckload.Bridge.Demo;
using Truckload.Bridge.Funbit;
using Truckload.Bridge.Sending;

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

if (!options.DryRun && (string.IsNullOrWhiteSpace(options.IngestUrl) || string.IsNullOrWhiteSpace(options.IngestKey)))
{
    Console.Error.WriteLine("Missing --ingest-url and/or --key (or TRUCKLOAD_INGEST_URL / TRUCKLOAD_INGEST_KEY). Use --help for usage, or --dry-run to test without a backend.");
    return 1;
}

if (options.Save)
{
    options.Save(baseDirectory);
    Console.WriteLine($"Saved configuration to {Path.Combine(baseDirectory, BridgeOptions.ConfigFileName)}");
}

Console.WriteLine("Truckload Bridge v1.0.0");
Console.WriteLine($"  Telemetry source : {(options.Demo ? $"demo ({options.DemoGame})" : options.TelemetryUrl)}");
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

ITelemetrySource source = options.Demo
    ? new DemoSource(time, options.DemoGame)
    : new FunbitSource(httpClient, options.TelemetryUrl);

IngestSender? sender = options.DryRun
    ? null
    : new IngestSender(httpClient, options.IngestUrl!, options.IngestKey!);

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
