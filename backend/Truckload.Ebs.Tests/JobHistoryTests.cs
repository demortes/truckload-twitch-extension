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
/// Exercises job-completion detection in the ingest upsert path — a stored active job
/// followed by a non-active/absent one on the next ingest should be archived to job
/// history — plus the history endpoint built on top of it and its retention cap.
/// </summary>
public class JobHistoryTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public JobHistoryTests(TestWebAppFactory factory) => _factory = factory;

    private static TelemetryPayload ActiveJobPayload(string cargo) => new(
        V: 1,
        Ts: 1727400000,
        Connected: true,
        Paused: false,
        Game: "ats",
        Units: "imperial",
        Job: new JobInfo(true, cargo, "Los Angeles", "Phoenix", 372, 245),
        Truck: null);

    private static readonly TelemetryPayload NoJobPayload = new(
        V: 1,
        Ts: 1727400100,
        Connected: true,
        Paused: false,
        Game: "ats",
        Units: "imperial",
        Job: null,
        Truck: null);

    private static StringContent Json(TelemetryPayload payload) =>
        // Must match the camelCase contract the real endpoint expects; see IngestEndpointTests
        // for why a plain JsonSerializer.Serialize(payload) call would break this.
        new(JsonSerializer.Serialize(payload, TelemetryJsonContext.Default.TelemetryPayload), Encoding.UTF8, "application/json");

    private async Task<HttpClient> AuthorizedChannelAsync(string channelId)
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var client = _factory.CreateClient();
        var token = TestJwt.CreateBroadcasterToken(channelId);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var key = (await (await client.PostAsync("/api/channels/keys", content: null))
            .Content.ReadFromJsonAsync<IngestKeyResponse>())!.IngestKey;

        client.DefaultRequestHeaders.Authorization = null;
        client.DefaultRequestHeaders.Add("X-Api-Key", key);
        return client;
    }

    [Fact]
    public async Task CompletedJob_IsArchivedToHistory_AndReturnedByHistoryEndpoint()
    {
        var client = await AuthorizedChannelAsync("hist-1");

        await client.PostAsync("/api/ingest", Json(ActiveJobPayload("Electronics")));
        _factory.Time.Advance(TimeSpan.FromMinutes(5));
        await client.PostAsync("/api/ingest", Json(NoJobPayload));

        var response = await client.GetAsync("/api/telemetry/hist-1/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<JobHistoryEntryResponse[]>();
        var entry = Assert.Single(history!);
        Assert.Equal("Electronics", entry.Cargo);
        Assert.Equal("Los Angeles", entry.Source);
        Assert.Equal("Phoenix", entry.Destination);
        Assert.Equal(372, entry.Distance);
        Assert.NotEqual(Guid.Empty, entry.Id);
    }

    [Fact]
    public async Task StillActiveJob_DoesNotProduceHistoryRow()
    {
        var client = await AuthorizedChannelAsync("hist-2");

        var job = ActiveJobPayload("Furniture");
        await client.PostAsync("/api/ingest", Json(job));
        _factory.Time.Advance(TimeSpan.FromMinutes(1));

        // Same job, still active on the next tick (e.g. ETA ticking down) — not a completion.
        var updatedJob = job with { Job = job.Job! with { Distance = 350, EtaMinutes = 200 } };
        await client.PostAsync("/api/ingest", Json(updatedJob));

        var response = await client.GetAsync("/api/telemetry/hist-2/history");
        var history = await response.Content.ReadFromJsonAsync<JobHistoryEntryResponse[]>();
        Assert.Empty(history!);
    }

    [Fact]
    public async Task FirstEverIngest_NeverProducesHistoryRow()
    {
        // No previously stored snapshot to compare against — nothing should be archived.
        var client = await AuthorizedChannelAsync("hist-3");

        await client.PostAsync("/api/ingest", Json(NoJobPayload));

        var response = await client.GetAsync("/api/telemetry/hist-3/history");
        var history = await response.Content.ReadFromJsonAsync<JobHistoryEntryResponse[]>();
        Assert.Empty(history!);
    }

    [Fact]
    public async Task HistoryEndpoint_ReturnsNewestFirst_AndCapsAtRetentionLimit()
    {
        var client = await AuthorizedChannelAsync("hist-4");

        // 25 completed jobs; only the most recent 20 should survive pruning.
        for (var i = 1; i <= 25; i++)
        {
            await client.PostAsync("/api/ingest", Json(ActiveJobPayload($"Cargo-{i}")));
            _factory.Time.Advance(TimeSpan.FromMinutes(1));
            await client.PostAsync("/api/ingest", Json(NoJobPayload));
            _factory.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var response = await client.GetAsync("/api/telemetry/hist-4/history?limit=50");
        var history = await response.Content.ReadFromJsonAsync<JobHistoryEntryResponse[]>();

        Assert.Equal(20, history!.Length);
        // Newest first: the most recently completed job (Cargo-25) comes first, and only
        // the 20 most recent survive pruning (Cargo-6 through Cargo-25).
        Assert.Equal("Cargo-25", history[0].Cargo);
        Assert.Equal("Cargo-6", history[^1].Cargo);
    }

    [Fact]
    public async Task HistoryEndpoint_DefaultsToTwentyRows_WhenNoLimitGiven()
    {
        var client = await AuthorizedChannelAsync("hist-5");

        for (var i = 1; i <= 22; i++)
        {
            await client.PostAsync("/api/ingest", Json(ActiveJobPayload($"Cargo-{i}")));
            _factory.Time.Advance(TimeSpan.FromMinutes(1));
            await client.PostAsync("/api/ingest", Json(NoJobPayload));
            _factory.Time.Advance(TimeSpan.FromMinutes(1));
        }

        var response = await client.GetAsync("/api/telemetry/hist-5/history");
        var history = await response.Content.ReadFromJsonAsync<JobHistoryEntryResponse[]>();
        Assert.Equal(20, history!.Length);
    }

    [Fact]
    public async Task HistoryEndpoint_ForUnknownChannel_ReturnsEmptyList()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/telemetry/never-seen-channel/history");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var history = await response.Content.ReadFromJsonAsync<JobHistoryEntryResponse[]>();
        Assert.Empty(history!);
    }
}
