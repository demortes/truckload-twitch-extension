using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Truckload.Contracts;
using Truckload.Ebs.Models;
using Xunit;

namespace Truckload.Ebs.Tests;

/// <summary>
/// Exercises the end-to-end throttling behavior via the ingest endpoint, using the
/// factory's FakeTimeProvider to control elapsed time deterministically.
/// </summary>
public class BroadcastThrottleTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public BroadcastThrottleTests(TestWebAppFactory factory) => _factory = factory;

    private static readonly TelemetryPayload SamplePayload = new(
        V: 1, Ts: 1727400000, Connected: true, Paused: false, Game: "ats", Units: "imperial",
        Job: new JobInfo(true, "Electronics", "Los Angeles", "Phoenix", 372, 245),
        Truck: new TruckInfo("Peterbilt", "579", "TRK-4521", 67, 3, 124532));

    private static StringContent Json(TelemetryPayload payload) =>
        // Must match the camelCase contract the real endpoint expects; a plain
        // JsonSerializer.Serialize(payload) call here emits PascalCase property names and
        // every ingest in this test would be rejected as malformed before it ever reaches
        // the throttle logic under test.
        new(JsonSerializer.Serialize(payload, TelemetryJsonContext.Default.TelemetryPayload), Encoding.UTF8, "application/json");

    [Fact]
    public async Task SecondIngestWithinInterval_IsThrottled_ThenSendsAfterTimeAdvances()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var client = _factory.CreateClient();
        var token = TestJwt.CreateBroadcasterToken("throttle-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var key = (await (await client.PostAsync("/api/channels/keys", content: null))
            .Content.ReadFromJsonAsync<IngestKeyResponse>())!.IngestKey;
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("X-Api-Key", key);

        var first = await client.PostAsync("/api/ingest", Json(SamplePayload));
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", firstBody.GetProperty("broadcast").GetString());

        var second = await client.PostAsync("/api/ingest", Json(SamplePayload));
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("throttled", secondBody.GetProperty("broadcast").GetString());

        _factory.Time.Advance(TimeSpan.FromSeconds(1.1));

        var third = await client.PostAsync("/api/ingest", Json(SamplePayload));
        var thirdBody = await third.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", thirdBody.GetProperty("broadcast").GetString());

        // Only the two "sent" ingests should have reached the fake Twitch PubSub call.
        Assert.Equal(2, _factory.PubSub.Calls.Count(c => c.ChannelId == "throttle-1"));
    }

    [Fact]
    public async Task RateLimitedBroadcast_SuppressesFurtherSendsUntilRetryAfterElapses()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var client = _factory.CreateClient();
        var token = TestJwt.CreateBroadcasterToken("throttle-2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var key = (await (await client.PostAsync("/api/channels/keys", content: null))
            .Content.ReadFromJsonAsync<IngestKeyResponse>())!.IngestKey;
        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("X-Api-Key", key);

        _factory.PubSub.Enqueue(new BroadcastResult(BroadcastOutcome.RateLimited, 429, TimeSpan.FromSeconds(5)));

        var first = await client.PostAsync("/api/ingest", Json(SamplePayload));
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("rate_limited", firstBody.GetProperty("broadcast").GetString());

        _factory.Time.Advance(TimeSpan.FromSeconds(2));
        var second = await client.PostAsync("/api/ingest", Json(SamplePayload));
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();
        // Still within the 5s suppression window from the 429.
        Assert.Equal("throttled", secondBody.GetProperty("broadcast").GetString());

        _factory.Time.Advance(TimeSpan.FromSeconds(4));
        var third = await client.PostAsync("/api/ingest", Json(SamplePayload));
        var thirdBody = await third.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("sent", thirdBody.GetProperty("broadcast").GetString());
    }
}
