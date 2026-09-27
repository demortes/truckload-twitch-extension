using Truckload.Bridge.Funbit;
using Truckload.Bridge.Mapping;
using Truckload.Contracts;
using Xunit;

namespace Truckload.Bridge.Tests;

public class TelemetryMapperTests
{
    private static FunbitTelemetry AtsSample() => new()
    {
        Game = new FunbitGame { Connected = true, Paused = false, GameName = "ATS" },
        Truck = new FunbitTruck
        {
            Make = "Peterbilt",
            Model = "579",
            LicensePlate = "TRK-4521",
            Fuel = 67,
            FuelCapacity = 100,
            Odometer = 124532 * 1.60934, // km, chosen to round-trip cleanly back to 124532 miles
            WearChassis = 0.03,
            WearEngine = 0.01,
        },
        Trailer = new FunbitTrailer { Name = "Electronics", Attached = true },
        Job = new FunbitJob { Income = 1000, SourceCity = "Los Angeles", DestinationCity = "Phoenix" },
        Navigation = new FunbitNavigation
        {
            EstimatedDistance = 372 * 1609.34, // meters
            EstimatedTime = DateTime.MinValue.AddMinutes(245),
        },
    };

    [Fact]
    public void Map_AtsWithAutoUnits_ProducesImperialDistances()
    {
        var payload = TelemetryMapper.Map(AtsSample(), ts: 1000, unitsMode: "auto");

        Assert.Equal(TelemetryUnits.Imperial, payload.Units);
        Assert.Equal("ats", payload.Game);
        Assert.True(payload.Connected);
        Assert.NotNull(payload.Job);
        Assert.Equal(372, payload.Job!.Distance);
        Assert.Equal(245, payload.Job.EtaMinutes);
        Assert.Equal("Electronics", payload.Job.Cargo);
        Assert.Equal("Los Angeles", payload.Job.Source);
        Assert.Equal("Phoenix", payload.Job.Destination);
        Assert.NotNull(payload.Truck);
        Assert.Equal(67, payload.Truck!.FuelPercent);
        Assert.Equal(3, payload.Truck.DamagePercent);
        Assert.Equal(124532, payload.Truck.Odometer); // 200460 km -> miles
    }

    [Fact]
    public void Map_Ets2WithAutoUnits_ProducesMetricDistances()
    {
        var raw = AtsSample();
        raw.Game!.GameName = "ETS2";
        raw.Navigation!.EstimatedDistance = 600_000; // 600 km in meters
        raw.Truck!.Odometer = 100_000; // already km

        var payload = TelemetryMapper.Map(raw, ts: 1000, unitsMode: "auto");

        Assert.Equal(TelemetryUnits.Metric, payload.Units);
        Assert.Equal(600, payload.Job!.Distance);
        Assert.Equal(100_000, payload.Truck!.Odometer);
    }

    [Fact]
    public void Map_ExplicitUnitsOverridesGameDefault()
    {
        var payload = TelemetryMapper.Map(AtsSample(), ts: 1000, unitsMode: TelemetryUnits.Metric);
        Assert.Equal(TelemetryUnits.Metric, payload.Units);
    }

    [Fact]
    public void Map_NoDestinationCity_MeansNoActiveJob()
    {
        var raw = AtsSample();
        raw.Job!.DestinationCity = null;

        var payload = TelemetryMapper.Map(raw, ts: 1000, unitsMode: "auto");
        Assert.Null(payload.Job);
    }

    [Fact]
    public void Map_ZeroIncome_MeansNoActiveJob()
    {
        var raw = AtsSample();
        raw.Job!.Income = 0;

        var payload = TelemetryMapper.Map(raw, ts: 1000, unitsMode: "auto");
        Assert.Null(payload.Job);
    }

    [Fact]
    public void Map_DisconnectedGame_ReturnsNullJobAndTruck()
    {
        var raw = AtsSample();
        raw.Game!.Connected = false;

        var payload = TelemetryMapper.Map(raw, ts: 1000, unitsMode: "auto");
        Assert.False(payload.Connected);
        Assert.Null(payload.Job);
        Assert.Null(payload.Truck);
    }

    [Fact]
    public void Map_ZeroFuelCapacity_DoesNotProduceInfinity()
    {
        var raw = AtsSample();
        raw.Truck!.FuelCapacity = 0;

        var payload = TelemetryMapper.Map(raw, ts: 1000, unitsMode: "auto");
        Assert.Equal(0, payload.Truck!.FuelPercent);
    }

    [Fact]
    public void Map_DamagePercentIsTheWorstWearComponent()
    {
        var raw = AtsSample();
        raw.Truck!.WearChassis = 0.02;
        raw.Truck.WearEngine = 0.02;
        raw.Truck.WearWheels = 0.20;

        var payload = TelemetryMapper.Map(raw, ts: 1000, unitsMode: "auto");
        Assert.Equal(20, payload.Truck!.DamagePercent);
    }

    [Fact]
    public void Disconnected_ProducesAConnectedFalsePayloadWithNoJobOrTruck()
    {
        var payload = TelemetryMapper.Disconnected(ts: 500);
        Assert.False(payload.Connected);
        Assert.Null(payload.Job);
        Assert.Null(payload.Truck);
        Assert.Null(payload.Game);
    }
}
