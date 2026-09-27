using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Truckload.Contracts;

namespace Truckload.Bridge.Sending;

public enum SendOutcome { Sent, Throttled, RateLimited, Failed, InvalidKey, NetworkError }

public sealed record SendResult(SendOutcome Outcome, TimeSpan? RetryAfter = null, string? Detail = null);

public sealed class IngestSender
{
    private readonly HttpClient _http;
    private readonly string _ingestUrl;
    private readonly string _ingestKey;

    public IngestSender(HttpClient http, string ingestUrl, string ingestKey)
    {
        _http = http;
        _ingestUrl = ingestUrl;
        _ingestKey = ingestKey;
    }

    public async Task<SendResult> SendAsync(TelemetryPayload payload, CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, TelemetryJsonContext.Default.TelemetryPayload);

        using var request = new HttpRequestMessage(HttpMethod.Post, _ingestUrl)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        request.Headers.Add("X-Api-Key", _ingestKey);
        request.Headers.UserAgent.ParseAdd("Truckload-Bridge/1.0");

        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

            using var response = await _http.SendAsync(request, cts.Token);

            if (response.StatusCode == HttpStatusCode.Unauthorized)
                return new SendResult(SendOutcome.InvalidKey, Detail: "The backend rejected the ingest key.");

            if (!response.IsSuccessStatusCode)
                return new SendResult(SendOutcome.Failed, Detail: $"Backend returned {(int)response.StatusCode}.");

            var body = await response.Content.ReadFromJsonAsync<JsonElement>(cts.Token);
            var broadcast = body.TryGetProperty("broadcast", out var broadcastProp) ? broadcastProp.GetString() : null;
            var retryAfterMs = body.TryGetProperty("retryAfterMs", out var retryProp) && retryProp.ValueKind == JsonValueKind.Number
                ? retryProp.GetDouble()
                : (double?)null;

            return broadcast switch
            {
                "rate_limited" => new SendResult(SendOutcome.RateLimited, TimeSpan.FromMilliseconds(retryAfterMs ?? 5000)),
                "throttled" => new SendResult(SendOutcome.Throttled),
                _ => new SendResult(SendOutcome.Sent),
            };
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return new SendResult(SendOutcome.NetworkError, Detail: ex.Message);
        }
    }
}
