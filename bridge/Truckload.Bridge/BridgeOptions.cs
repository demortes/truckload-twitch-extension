using System.Text.Json;

namespace Truckload.Bridge;

public sealed class BridgeOptions
{
    public string? IngestUrl { get; set; }
    public string? IngestKey { get; set; }
    public string TelemetryUrl { get; set; } = "http://localhost:25555/api/ets2/telemetry";
    public int PollMs { get; set; } = 1000;
    public int HeartbeatSeconds { get; set; } = 30;

    /// <summary>"auto" (derive from the reported game), "imperial", or "metric".</summary>
    public string Units { get; set; } = "auto";

    public bool Demo { get; set; }
    public string DemoGame { get; set; } = "ats";
    public bool DryRun { get; set; }
    public bool Save { get; set; }
    public bool Verbose { get; set; }
    public bool Help { get; set; }

    public const string ConfigFileName = "truckload-bridge.json";

    /// <summary>Resolves options from, in increasing priority: defaults, the config file next to the exe, environment variables, then CLI args.</summary>
    public static BridgeOptions Resolve(string[] args, string baseDirectory)
    {
        var options = new BridgeOptions();

        var configPath = Path.Combine(baseDirectory, ConfigFileName);
        if (File.Exists(configPath))
        {
            try
            {
                var json = File.ReadAllText(configPath);
                var fromFile = JsonSerializer.Deserialize<BridgeOptions>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                });
                if (fromFile is not null)
                    options = fromFile;
            }
            catch (Exception ex) when (ex is IOException or JsonException)
            {
                Console.Error.WriteLine($"Warning: failed to read {ConfigFileName}: {ex.Message}");
            }
        }

        ApplyEnvironment(options);
        ApplyArgs(options, args);

        return options;
    }

    private static void ApplyEnvironment(BridgeOptions options)
    {
        options.IngestUrl = Environment.GetEnvironmentVariable("TRUCKLOAD_INGEST_URL") ?? options.IngestUrl;
        options.IngestKey = Environment.GetEnvironmentVariable("TRUCKLOAD_INGEST_KEY") ?? options.IngestKey;
        options.TelemetryUrl = Environment.GetEnvironmentVariable("TRUCKLOAD_TELEMETRY_URL") ?? options.TelemetryUrl;
    }

    private static void ApplyArgs(BridgeOptions options, string[] args)
    {
        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--ingest-url":
                    options.IngestUrl = RequireValue(args, ref i);
                    break;
                case "--key":
                    options.IngestKey = RequireValue(args, ref i);
                    break;
                case "--telemetry-url":
                    options.TelemetryUrl = RequireValue(args, ref i);
                    break;
                case "--poll-ms":
                    options.PollMs = int.Parse(RequireValue(args, ref i));
                    break;
                case "--heartbeat-s":
                    options.HeartbeatSeconds = int.Parse(RequireValue(args, ref i));
                    break;
                case "--units":
                    options.Units = RequireValue(args, ref i);
                    break;
                case "--demo":
                    options.Demo = true;
                    break;
                case "--demo-game":
                    options.DemoGame = RequireValue(args, ref i);
                    break;
                case "--dry-run":
                    options.DryRun = true;
                    break;
                case "--save":
                    options.Save = true;
                    break;
                case "--verbose":
                    options.Verbose = true;
                    break;
                case "--help":
                case "-h":
                case "-?":
                    options.Help = true;
                    break;
            }
        }

        // Never let the bridge hammer the telemetry server or the backend faster than once a second.
        options.PollMs = Math.Max(options.PollMs, 1000);
    }

    private static string RequireValue(string[] args, ref int i)
    {
        if (i + 1 >= args.Length)
            throw new ArgumentException($"Missing value for '{args[i]}'.");
        return args[++i];
    }

    public void Save(string baseDirectory)
    {
        var path = Path.Combine(baseDirectory, ConfigFileName);
        var json = JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(path, json);
    }

    public static string HelpText => """
        Truckload Bridge — sends ATS/ETS2 telemetry to the Truckload Twitch extension backend.

        Usage:
          Truckload.Bridge --ingest-url <url> --key <key> [options]
          Truckload.Bridge --demo --ingest-url <url> --key <key>
          Truckload.Bridge --dry-run [options]

        Options:
          --ingest-url <url>     Backend ingest endpoint, e.g. https://your-domain/api/ingest
          --key <key>            Ingest API key from the extension's Config page
          --telemetry-url <url>  Local telemetry server URL (default: http://localhost:25555/api/ets2/telemetry)
          --poll-ms <ms>         Poll interval, minimum and default 1000
          --heartbeat-s <s>      Send a keep-alive even when nothing changed (default 30)
          --units auto|imperial|metric   Distance units (default: auto, derived from the game)
          --demo                 Run a scripted sample job instead of reading the game
          --demo-game ats|ets2   Game to simulate in demo mode (default: ats)
          --dry-run              Print payloads instead of sending them; no ingest URL/key required
          --save                 Write the resolved options to truckload-bridge.json next to the exe
          --verbose              Log every payload considered, not just ones sent
          --help                 Show this text
        """;
}
