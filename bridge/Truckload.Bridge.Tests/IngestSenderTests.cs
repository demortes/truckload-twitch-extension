using System.Net;
using Truckload.Bridge.Sending;
using Truckload.Contracts;
using Xunit;

namespace Truckload.Bridge.Tests;

public class IngestSenderTests
{
    private static readonly TelemetryPayload Payload = new(
        V: 1, Ts: 1, Connected: false, Paused: false, Game: null, Units: "imperial", Job: null, Truck: null);

    [Fact]
    public async Task TooManyRequests_BacksOffForTheServersRetryAfter()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(() =>
        {
            var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
            response.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(7));
            return response;
        });
        var sender = new IngestSender(new HttpClient(handler), "https://example.test/api/ingest", "key");

        var result = await sender.SendAsync(Payload, CancellationToken.None);

        Assert.Equal(SendOutcome.RateLimited, result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(7), result.RetryAfter);
    }

    [Fact]
    public async Task TooManyRequests_WithoutRetryAfter_UsesAShortDefault()
    {
        var handler = new FakeHttpMessageHandler();
        handler.Enqueue(() => new HttpResponseMessage(HttpStatusCode.TooManyRequests));
        var sender = new IngestSender(new HttpClient(handler), "https://example.test/api/ingest", "key");

        var result = await sender.SendAsync(Payload, CancellationToken.None);

        Assert.Equal(SendOutcome.RateLimited, result.Outcome);
        Assert.Equal(TimeSpan.FromSeconds(5), result.RetryAfter);
    }
}
