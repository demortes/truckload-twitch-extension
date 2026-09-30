using System.Text.Json;
using Truckload.Bridge.Mapping;
using Truckload.Bridge.TruckTel;
using Truckload.Contracts;
using Xunit;

namespace Truckload.Bridge.Tests;

public class TruckTelTests
{
    // Key names are the SCS telemetry SDK's, as exposed by TruckTel's "flat" REST format.
    private const string SampleFlat = """
        {
          "game.id": "ats",
          "game.time": 10000,
          "frame.paused": false,
          "truck.brand": "Kenworth",
          "truck.name": "W900",
          "truck.license.plate": "TRK 123",
          "truck.fuel.amount": 150.0,
          "truck.fuel.capacity": 600.0,
          "truck.odometer": 1000.0,
          "truck.wear.engine": 0.10,
          "truck.wear.transmission": 0.05,
          "truck.wear.cabin": 0.02,
          "truck.wear.chassis": 0.30,
          "truck.wear.wheels": 0.20,
          "truck.navigation.distance": 160934.0,
          "truck.navigation.time": 5400,
          "trailer.connected": true,
          "job.cargo": "Machinery",
          "job.income": 4200,
          "job.source.city": "Phoenix",
          "job.destination.city": "Las Vegas",
          "job.delivery.time": 10600
        }
        """;

    private static Dictionary<string, JsonElement> Parse(string json) =>
        JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!;

    [Fact]
    public void ToFunbit_ThenMapper_ProducesTheContractPayload()
    {
        var raw = TruckTelMapper.ToFunbit(Parse(SampleFlat));
        var payload = TelemetryMapper.Map(raw, ts: 1, unitsMode: "auto");

        Assert.True(payload.Connected);
        Assert.False(payload.Paused);
        Assert.Equal("ats", payload.Game);
        Assert.Equal(TelemetryUnits.Imperial, payload.Units);

        Assert.NotNull(payload.Job);
        Assert.Equal("Machinery", payload.Job!.Cargo);
        Assert.Equal("Phoenix", payload.Job.Source);
        Assert.Equal("Las Vegas", payload.Job.Destination);
        Assert.Equal(90, payload.Job.EtaMinutes); // navigation.time 5400 s
        Assert.Equal(100.0, (double)payload.Job.Distance, precision: 0); // 160.934 km -> ~100 mi

        Assert.NotNull(payload.Truck);
        Assert.Equal("Kenworth", payload.Truck!.Make);
        Assert.Equal("W900", payload.Truck.Model);
        Assert.Equal("TRK 123", payload.Truck.LicensePlate);
        Assert.Equal(25, payload.Truck.FuelPercent); // 150 / 600
        Assert.Equal(30, payload.Truck.DamagePercent); // worst wear = chassis
    }

    [Fact]
    public void RemainingTime_IsDeadlineMinusGameTime_WhenNavigationTimeIsMissing()
    {
        var flat = Parse(SampleFlat);
        flat.Remove("truck.navigation.time");

        var raw = TruckTelMapper.ToFunbit(flat);
        var payload = TelemetryMapper.Map(raw, 1, "auto");

        Assert.Equal(600, payload.Job!.EtaMinutes); // 10600 - 10000 game minutes
    }

    [Fact]
    public void NoGameId_MeansNotConnected()
    {
        var raw = TruckTelMapper.ToFunbit(Parse("{}"));
        Assert.False(raw.Game!.Connected);
    }

    [Fact]
    public void NoActiveJob_MapsToNoJob()
    {
        var flat = Parse(SampleFlat);
        foreach (var key in flat.Keys.Where(k => k.StartsWith("job.")).ToList())
            flat.Remove(key);

        var payload = TelemetryMapper.Map(TruckTelMapper.ToFunbit(flat), 1, "auto");

        Assert.Null(payload.Job);
        Assert.NotNull(payload.Truck);
    }

    [Fact]
    public void NullValuesAreTreatedAsMissing()
    {
        var flat = Parse("""{"game.id":"ets2","truck.brand":null,"job.destination.city":null}""");
        var raw = TruckTelMapper.ToFunbit(flat);

        Assert.Null(raw.Truck!.Make);
        Assert.Null(raw.Job!.DestinationCity);
    }

    [Theory]
    [InlineData("http://localhost:8080", "http://localhost:8080/api/rest/flat")]
    [InlineData("http://localhost:8080/", "http://localhost:8080/api/rest/flat")]
    [InlineData("http://localhost:9000/api/rest/flat", "http://localhost:9000/api/rest/flat")]
    public void NormalizeUrl_AddsTheRestPathWhenMissing(string input, string expected) =>
        Assert.Equal(expected, TruckTelSource.NormalizeUrl(input));
}
