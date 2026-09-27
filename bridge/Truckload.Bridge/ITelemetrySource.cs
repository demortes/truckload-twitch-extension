using Truckload.Bridge.Funbit;

namespace Truckload.Bridge;

public interface ITelemetrySource
{
    Task<TelemetryFetchResult> FetchAsync(CancellationToken cancellationToken);
}

public sealed record TelemetryFetchResult(bool Available, FunbitTelemetry? Data, string? Error = null)
{
    public static TelemetryFetchResult Unavailable(string error) => new(false, null, error);
    public static TelemetryFetchResult Ok(FunbitTelemetry data) => new(true, data);
}
