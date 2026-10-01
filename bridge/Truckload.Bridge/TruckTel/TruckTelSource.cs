using System.Net.Http.Json;
using System.Text.Json;

namespace Truckload.Bridge.TruckTel;

/// <summary>
/// Polls a TruckTel plugin (https://github.com/jvanstraten/TruckTel) running inside ETS2/ATS.
/// Reads only the key prefixes the bridge needs from its REST API, in the flat format.
/// Given several candidate URLs (the Truckload app's port, then TruckTel's default), it uses the
/// first that answers and sticks with it until it stops responding.
/// </summary>
public sealed class TruckTelSource : ITelemetrySource
{
    private static readonly string[] Prefixes = ["game", "frame", "truck", "job", "trailer"];

    private readonly HttpClient _http;
    private readonly string[] _baseUrls;
    private int _preferred;

    public TruckTelSource(HttpClient http, string url)
        : this(http, [url])
    {
    }

    public TruckTelSource(HttpClient http, IEnumerable<string> urls)
    {
        _http = http;
        _baseUrls = urls.Select(NormalizeUrl).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (_baseUrls.Length == 0)
            throw new ArgumentException("At least one TruckTel URL is required.", nameof(urls));
    }

    /// <summary>Accepts "http://host:port", or a full ".../api/rest/flat" URL.</summary>
    public static string NormalizeUrl(string url)
    {
        var trimmed = url.TrimEnd('/');
        return trimmed.Contains("/api/rest", StringComparison.OrdinalIgnoreCase) ? trimmed : trimmed + "/api/rest/flat";
    }

    public async Task<TelemetryFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        var errors = new List<string>();

        for (var attempt = 0; attempt < _baseUrls.Length; attempt++)
        {
            var index = (_preferred + attempt) % _baseUrls.Length;
            var baseUrl = _baseUrls[index];

            try
            {
                var merged = await FetchFlatAsync(baseUrl, cancellationToken);
                _preferred = index;

                return merged.Count == 0
                    ? TelemetryFetchResult.Unavailable("TruckTel returned no data. Is ETS2/ATS running and past the main menu?")
                    : TelemetryFetchResult.Ok(TruckTelMapper.ToFunbit(merged));
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException or JsonException)
            {
                if (cancellationToken.IsCancellationRequested)
                    throw;
                errors.Add($"{baseUrl} ({ex.GetType().Name}: {ex.Message})");
            }
        }

        return TelemetryFetchResult.Unavailable(
            $"Could not read telemetry from TruckTel at {string.Join(" or ", errors)}. " +
            "Is ETS2/ATS running with the TruckTel plugin installed (and is the port correct)?");
    }

    private async Task<Dictionary<string, JsonElement>> FetchFlatAsync(string baseUrl, CancellationToken cancellationToken)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(3));

        var merged = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var prefix in Prefixes)
        {
            var part = await _http.GetFromJsonAsync<Dictionary<string, JsonElement>>($"{baseUrl}/{prefix}", cts.Token);
            if (part is null)
                continue;
            foreach (var (key, value) in part)
                merged[key] = value;
        }

        return merged;
    }
}
