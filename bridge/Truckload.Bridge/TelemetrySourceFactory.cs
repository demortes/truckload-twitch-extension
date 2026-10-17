using Truckload.Bridge.Demo;
using Truckload.Bridge.Funbit;
using Truckload.Bridge.TruckTel;

namespace Truckload.Bridge;

/// <summary>Builds the telemetry source an options object asks for; shared by the CLI and the tray app.</summary>
public static class TelemetrySourceFactory
{
    public static ITelemetrySource Create(BridgeOptions options, HttpClient telemetryHttpClient, TimeProvider time) =>
        options.Demo
            ? new DemoSource(time, options.DemoGame)
            : options.ResolvedSource switch
            {
                BridgeOptions.SourceFunbit => new FunbitSource(telemetryHttpClient, options.ResolvedTelemetryUrl),
                BridgeOptions.SourceTrucktel => new TruckTelSource(telemetryHttpClient, options.ResolvedTelemetryUrl),
                var other => throw new ArgumentException($"Unknown --source '{other}'. Use 'trucktel' or 'funbit'."),
            };

    /// <summary>
    /// The local telemetry servers (TruckTel in particular) close idle keep-alive connections, which makes a
    /// pooled client fail on every few requests. Loopback connections are cheap, so never reuse them.
    /// </summary>
    public static HttpClient CreateTelemetryHttpClient() =>
        new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.Zero });
}
