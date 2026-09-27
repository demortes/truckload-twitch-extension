using Truckload.Bridge.Funbit;

namespace Truckload.Bridge.Tests;

public sealed class FakeTelemetrySource : ITelemetrySource
{
    public TelemetryFetchResult NextResult { get; set; } = TelemetryFetchResult.Ok(new FunbitTelemetry
    {
        Game = new FunbitGame { Connected = true, GameName = "ATS" },
        Truck = new FunbitTruck { Make = "Peterbilt", Model = "579", Fuel = 50, FuelCapacity = 100 },
    });

    public Task<TelemetryFetchResult> FetchAsync(CancellationToken cancellationToken) => Task.FromResult(NextResult);
}
