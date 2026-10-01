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

    private sealed class FailFirstRequestHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            if (Calls == 1)
                return Task.FromException<HttpResponseMessage>(new HttpRequestException("connection forcibly closed"));
            return Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.OK)
            {
                Content = new StringContent("""{"game.id":"ats"}""", System.Text.Encoding.UTF8, "application/json"),
            });
        }
    }

    [Fact]
    public async Task Source_RetriesOnceWhenAReusedConnectionIsDead()
    {
        var handler = new FailFirstRequestHandler();
        var source = new TruckTelSource(new HttpClient(handler), "http://localhost:25852");

        var result = await source.FetchAsync(CancellationToken.None);

        Assert.True(result.Available);
        Assert.Equal("ats", result.Data!.Game!.GameName);
    }

    [Theory]
    [InlineData("http://localhost:8080", "http://localhost:8080/api/rest/flat")]
    [InlineData("http://localhost:8080/", "http://localhost:8080/api/rest/flat")]
    [InlineData("http://localhost:9000/api/rest/flat", "http://localhost:9000/api/rest/flat")]
    public void NormalizeUrl_AddsTheRestPathWhenMissing(string input, string expected) =>
        Assert.Equal(expected, TruckTelSource.NormalizeUrl(input));

    [Fact]
    public void Dashboard_IsMappedFromTheSdkKeys()
    {
        // Values taken from a live ATS session: hazards on, wipers on, battery warning, parking brake.
        var flat = Parse("""
            {
              "game.id": "ats",
              "truck.brand": "Kenworth", "truck.name": "W900",
              "truck.speed": 22.352,
              "truck.lblinker": false, "truck.rblinker": false, "truck.hazard.warning": true,
              "truck.light.lblinker": true, "truck.light.rblinker": true,
              "truck.light.beam.low": true, "truck.light.beam.high": false, "truck.light.parking": true,
              "truck.wipers": true,
              "truck.fuel.warning": false, "truck.battery.voltage.warning": true,
              "truck.brake.parking": true
            }
            """);

        var payload = TelemetryMapper.Map(TruckTelMapper.ToFunbit(flat), ts: 1, unitsMode: "auto");
        var dash = payload.Dashboard!;

        Assert.Equal(50, dash.Speed); // 22.352 m/s = 80.47 km/h = 50 mph
        Assert.Equal("hazard", dash.Signal);
        Assert.Equal("low", dash.Lights);
        Assert.True(dash.Wipers);
        Assert.Equal(new[] { "battery", "parkingBrake" }, dash.Warnings);
    }

    [Fact]
    public void Dashboard_UsesTheStalkNotTheFlashingLamp()
    {
        // The lamp flashes (aliasing against a 1s poll); only the stalk position is steady.
        var flat = Parse("""{"game.id":"ats","truck.lblinker":true,"truck.rblinker":false,"truck.light.lblinker":false,"truck.light.rblinker":false}""");

        Assert.Equal("left", TelemetryMapper.Map(TruckTelMapper.ToFunbit(flat), 1, "auto").Dashboard!.Signal);
    }

    [Theory]
    [InlineData("""{"game.id":"ats","rest.stop":270}""", 270)] // 4h 30m until the next rest
    [InlineData("""{"game.id":"ats","rest.stop":0}""", null)] // ambiguous (due now, or fatigue off): not shown
    [InlineData("""{"game.id":"ats","rest.stop":-15}""", null)]
    [InlineData("""{"game.id":"ats"}""", null)] // channel absent (fatigue simulation off)
    public void Dashboard_ReportsMinutesUntilTheNextRest(string json, int? expected)
    {
        var flat = Parse(json);
        flat["truck.brand"] = JsonDocument.Parse("\"Kenworth\"").RootElement; // a truck must exist for a dashboard

        var payload = TelemetryMapper.Map(TruckTelMapper.ToFunbit(flat), ts: 1, unitsMode: "auto");

        Assert.Equal(expected, payload.Dashboard!.RestMinutes);
    }
}
