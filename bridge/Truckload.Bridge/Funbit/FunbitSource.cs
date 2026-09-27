using System.Net.Http.Json;

namespace Truckload.Bridge.Funbit;

/// <summary>Polls a local Funbit-compatible ETS2/ATS Telemetry Web Server over HTTP.</summary>
public sealed class FunbitSource : ITelemetrySource
{
    private readonly HttpClient _http;
    private readonly string _url;

    public FunbitSource(HttpClient http, string url)
    {
        _http = http;
        _url = url;
    }

    public async Task<TelemetryFetchResult> FetchAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(2));

            var data = await _http.GetFromJsonAsync<FunbitTelemetry>(_url, cts.Token);
            return data is null
                ? TelemetryFetchResult.Unavailable("Telemetry server returned an empty response.")
                : TelemetryFetchResult.Ok(data);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or OperationCanceledException)
        {
            return TelemetryFetchResult.Unavailable(
                $"Could not reach the telemetry server at {_url}. Is ETS2/ATS running with the telemetry SDK plugin installed?");
        }
    }
}
