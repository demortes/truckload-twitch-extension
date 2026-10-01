using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Truckload.Contracts;
using Truckload.Ebs.Models;
using Truckload.Ebs.Services;
using Xunit;

namespace Truckload.Ebs.Tests;

public class RateLimitingTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public RateLimitingTests(TestWebAppFactory factory) => _factory = factory;

    private static readonly TelemetryPayload Sample = new(
        V: 1, Ts: 1727400000, Connected: true, Paused: false, Game: "ats", Units: "imperial",
        Job: null, Truck: new TruckInfo("Peterbilt", "579", "TRK-4521", 67, 3, 124532));

    private static StringContent Json(TelemetryPayload payload) =>
        new(JsonSerializer.Serialize(payload, TelemetryJsonContext.Default.TelemetryPayload), Encoding.UTF8, "application/json");

    private async Task<string> NewKeyAsync(HttpClient client, string channelId)
    {
        await _factory.EnsureDatabaseCreatedAsync();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", TestJwt.CreateBroadcasterToken(channelId));
        var key = (await (await client.PostAsync("/api/channels/keys", content: null)).Content.ReadFromJsonAsync<IngestKeyResponse>())!.IngestKey;
        client.DefaultRequestHeaders.Authorization = null;
        return key;
    }

    [Fact]
    public async Task Ingest_NoLongerAcceptsTheKeyInTheQueryString()
    {
        var client = _factory.CreateClient();
        var key = await NewKeyAsync(client, "rl-query");

        var response = await client.PostAsync($"/api/ingest?key={key}", Json(Sample));

        // A key in the URL would be logged by proxies and CDNs, so only the X-Api-Key header is honoured.
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Ingest_IsLimitedPerKey_AndOtherKeysAreUnaffected()
    {
        // One request per second sustained, burst of two.
        using var limited = _factory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?> { ["RateLimiting:IngestPerKeyPerSecond"] = "1" })));
        var client = limited.CreateClient();
        var noisyKey = await NewKeyAsync(client, "rl-noisy");
        var quietKey = await NewKeyAsync(client, "rl-quiet");

        var statuses = new List<HttpStatusCode>();
        HttpResponseMessage? rejected = null;
        for (var i = 0; i < 6; i++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "/api/ingest") { Content = Json(Sample) };
            request.Headers.Add("X-Api-Key", noisyKey);
            var response = await client.SendAsync(request);
            statuses.Add(response.StatusCode);
            if (response.StatusCode == HttpStatusCode.TooManyRequests)
                rejected ??= response;
        }

        Assert.Equal(HttpStatusCode.OK, statuses[0]);
        Assert.Contains(HttpStatusCode.TooManyRequests, statuses);
        Assert.NotNull(rejected!.Headers.RetryAfter);

        using var other = new HttpRequestMessage(HttpMethod.Post, "/api/ingest") { Content = Json(Sample) };
        other.Headers.Add("X-Api-Key", quietKey);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(other)).StatusCode);
    }

    [Fact]
    public void PartitionKey_OnlyWellFormedKeysGetTheirOwnBucket()
    {
        var real = new string('a', 64);

        Assert.Equal(real, RateLimiting.PartitionKeyFor(real));
        Assert.Equal("malformed", RateLimiting.PartitionKeyFor(null));
        Assert.Equal("malformed", RateLimiting.PartitionKeyFor("short"));
        Assert.Equal("malformed", RateLimiting.PartitionKeyFor(new string('z', 64))); // right length, not hex
    }
}
