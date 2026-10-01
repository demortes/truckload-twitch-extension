using System.Net.Http.Json;
using System.Text.Json;

namespace Truckload.Bridge.TruckTel;

/// <summary>
/// Polls a TruckTel plugin (https://github.com/jvanstraten/TruckTel) running inside ETS2/ATS.
/// Reads only the key prefixes the bridge needs from its REST API, in the flat format.
/// </summary>
public sealed class TruckTelSource : ITelemetrySource
{
    private static readonly string[] Prefixes = ["game", "frame", "rest", "truck", "job", "trailer"];

    private readonly HttpClient _http;
    private readonly string _baseUrl;

    public TruckTelSource(HttpClient http, string url)
    {
        _http = http;
        _baseUrl = NormalizeUrl(url);
    }

    /// <summary>Accepts "http://host:port", or a full ".../api/rest/flat" URL.</summary>
    public static string NormalizeUrl(string url)
    {
        var trimmed = url.TrimEnd('/');
        return trimmed.Contains("/api/rest", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "/api/rest/flat";
    }

    public async Task<TelemetryFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        // TruckTel closes idle keep-alive connections, so a pooled connection can be dead by the
        // time it is reused ("connection forcibly closed"). One retry gets a fresh connection.
        var first = await TryFetchAsync(cancellationToken);
        if (first.Available || cancellationToken.IsCancellationRequested)
            return first;

        return await TryFetchAsync(cancellationToken);
    }

    private async Task<TelemetryFetchResult> TryFetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(3));

            var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
            foreach (var prefix in Prefixes)
            {
                var part = await _http.GetFromJsonAsync<Dictionary<string, JsonElement>>($"{_baseUrl}/{prefix}", cts.Token);
                if (part is null)
                    continue;
                foreach (var (key, value) in part)
                    merged[key] = value;
            }

            return merged.Count == 0
                ? TelemetryFetchResult.Unavailable("TruckTel returned no data. Is ETS2/ATS running and past the main menu?")
                : TelemetryFetchResult.Ok(TruckTelMapper.ToFunbit(merged));
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
        {
            return TelemetryFetchResult.Unavailable(
                $"Could not read telemetry from TruckTel at {_baseUrl} ({ex.GetType().Name}: {ex.Message}). Is ETS2/ATS running with the TruckTel plugin installed (and is the port correct)?");
        }
    }
}
