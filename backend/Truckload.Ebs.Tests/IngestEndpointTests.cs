using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Truckload.Contracts;
using Truckload.Ebs.Models;
using Xunit;

namespace Truckload.Ebs.Tests;

public class IngestEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public IngestEndpointTests(TestWebAppFactory factory) => _factory = factory;

    private static readonly TelemetryPayload SamplePayload = new(
        V: 1,
        Ts: 1727400000,
        Connected: true,
        Paused: false,
        Game: "ats",
        Units: "imperial",
        Job: new JobInfo(true, "Electronics", "Los Angeles", "Phoenix", 372, 245),
        Truck: new TruckInfo("Peterbilt", "579", "TRK-4521", 67, 3, 124532)
    );

    private async Task<(HttpClient Client, string ChannelId, string IngestKey)> AuthorizedChannelAsync(string channelId)
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var client = _factory.CreateClient();
        var token = TestJwt.CreateBroadcasterToken(channelId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var keyResponse = await (await client.PostAsync("/api/channels/keys", content: null))
            .Content.ReadFromJsonAsync<IngestKeyResponse>();

        client.DefaultRequestHeaders.Authorization = null;
        return (client, channelId, keyResponse!.IngestKey);
    }

    private static StringContent Json(object payload) =>
        new(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task Ingest_WithoutApiKey_Returns401()
    {
        var (client, _, _) = await AuthorizedChannelAsync("ing-1");
        var response = await client.PostAsync("/api/ingest", Json(SamplePayload));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithWrongApiKey_Returns401()
    {
        var (client, _, _) = await AuthorizedChannelAsync("ing-2");
        client.DefaultRequestHeaders.Add("X-Api-Key", "not-a-real-key");
        var response = await client.PostAsync("/api/ingest", Json(SamplePayload));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithMalformedJson_Returns400()
    {
        var (client, _, key) = await AuthorizedChannelAsync("ing-3");
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        var response = await client.PostAsync("/api/ingest", new StringContent("{not json", Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithContractViolation_Returns400()
    {
        var (client, _, key) = await AuthorizedChannelAsync("ing-4");
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        var invalid = SamplePayload with { Truck = SamplePayload.Truck! with { FuelPercent = 140 } };
        var response = await client.PostAsync("/api/ingest", Json(invalid));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithOversizedBody_Returns413()
    {
        var (client, _, key) = await AuthorizedChannelAsync("ing-5");
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        var oversized = SamplePayload with { Job = SamplePayload.Job! with { Cargo = new string('x', TelemetryPayloadValidator.MaxBodyBytes) } };
        var response = await client.PostAsync("/api/ingest", Json(oversized));
        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_WithValidPayload_StoresAndBroadcasts_AndIsReadableBack()
    {
        var (client, channelId, key) = await AuthorizedChannelAsync("ing-6");
        client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var ingestResponse = await client.PostAsync("/api/ingest", Json(SamplePayload));
        Assert.Equal(HttpStatusCode.OK, ingestResponse.StatusCode);
        var ingestBody = await ingestResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", ingestBody.GetProperty("broadcast").GetString());
        Assert.Single(_factory.PubSub.Calls);
        Assert.Equal(channelId, _factory.PubSub.Calls[0].ChannelId);

        var telemetryResponse = await client.GetAsync($"/api/telemetry/{channelId}");
        Assert.Equal(HttpStatusCode.OK, telemetryResponse.StatusCode);
        var stored = await telemetryResponse.Content.ReadFromJsonAsync<TelemetryPayload>();
        Assert.Equal(SamplePayload.Job!.Cargo, stored!.Job!.Cargo);
        Assert.Equal(SamplePayload.Truck!.FuelPercent, stored.Truck!.FuelPercent);
    }
}
