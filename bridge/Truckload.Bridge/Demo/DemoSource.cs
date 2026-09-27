using Truckload.Bridge.Funbit;

namespace Truckload.Bridge.Demo;

/// <summary>
/// A scripted, deterministic telemetry feed with no dependency on the game being installed.
/// Used for local development and for Twitch extension reviewers who need to see live data
/// without running ATS/ETS2 themselves.
/// </summary>
public sealed class DemoSource : ITelemetrySource
{
    private readonly TimeProvider _time;
    private readonly bool _ets2;
    private readonly DateTimeOffset _start;

    private static readonly (string Source, string Destination, string Cargo)[] AtsJobs =
    {
        ("Los Angeles", "Phoenix", "Electronics"),
        ("Phoenix", "Dallas", "Machine Parts"),
        ("Dallas", "Denver", "Farm Supplies"),
    };

    private static readonly (string Source, string Destination, string Cargo)[] Ets2Jobs =
    {
        ("Rotterdam", "Berlin", "Electronics"),
        ("Berlin", "Warsaw", "Machine Parts"),
        ("Warsaw", "Vienna", "Steel Coils"),
    };

    private const int TotalDistanceMiles = 372;
    private const int TotalEtaMinutes = 245;
    private const int JobDurationSeconds = 300; // one simulated leg completes every 5 minutes
    private const int RestDurationSeconds = 20; // pause between jobs with job:null

    public DemoSource(TimeProvider time, string game)
    {
        _time = time;
        _ets2 = string.Equals(game, "ets2", StringComparison.OrdinalIgnoreCase);
        _start = time.GetUtcNow();
    }

    public Task<TelemetryFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var elapsed = (int)(_time.GetUtcNow() - _start).TotalSeconds;
        var cycle = JobDurationSeconds + RestDurationSeconds;
        var phase = elapsed % cycle;
        var legIndex = (elapsed / cycle) % (_ets2 ? Ets2Jobs.Length : AtsJobs.Length);
        var (source, destination, cargo) = (_ets2 ? Ets2Jobs : AtsJobs)[legIndex];

        var game = new FunbitGame { Connected = true, Paused = false, GameName = _ets2 ? "ETS2" : "ATS" };

        // fuel drains from 100% to ~35% and damage creeps up slowly over the whole demo run.
        var fuelPercent = Math.Max(35, 100 - elapsed / 20);
        var damage = Math.Min(15, elapsed / 600);

        var truck = new FunbitTruck
        {
            Make = "Peterbilt",
            Model = "579",
            LicensePlate = "TRK-4521",
            Fuel = fuelPercent,
            FuelCapacity = 100,
            Odometer = 124532 + elapsed / 10.0,
            WearChassis = damage / 100.0,
        };

        if (phase >= JobDurationSeconds)
        {
            // Resting between jobs: connected, but no active job.
            var data = new FunbitTelemetry { Game = game, Truck = truck, Job = null, Trailer = null, Navigation = null };
            return Task.FromResult(TelemetryFetchResult.Ok(data));
        }

        var progress = phase / (double)JobDurationSeconds;
        var remainingDistance = TotalDistanceMiles * 1609.34 * (1 - progress);
        var remainingMinutes = (int)Math.Round(TotalEtaMinutes * (1 - progress));

        var jobData = new FunbitJob
        {
            Income = 1000,
            SourceCity = source,
            DestinationCity = destination,
            RemainingTime = DateTime.MinValue.AddMinutes(remainingMinutes),
        };
        var navigation = new FunbitNavigation
        {
            EstimatedDistance = remainingDistance,
            EstimatedTime = DateTime.MinValue.AddMinutes(remainingMinutes),
        };
        var trailer = new FunbitTrailer { Name = cargo, Attached = true };

        var result = new FunbitTelemetry { Game = game, Truck = truck, Job = jobData, Navigation = navigation, Trailer = trailer };
        return Task.FromResult(TelemetryFetchResult.Ok(result));
    }
}
