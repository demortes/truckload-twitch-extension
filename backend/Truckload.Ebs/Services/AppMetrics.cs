using System.Diagnostics.Metrics;

namespace Truckload.Ebs.Services;

/// <summary>
/// Application-level OpenTelemetry metrics. Tags are deliberately low-cardinality
/// (no channel IDs) so they are safe to ship to Datadog as custom-metric dimensions.
/// </summary>
public static class AppMetrics
{
    public const string MeterName = "Truckload.Ebs";

    private static readonly Meter Meter = new(MeterName);

    private static readonly Counter<long> IngestRequests =
        Meter.CreateCounter<long>("truckload.ingest.requests", description: "Telemetry ingest requests by outcome.");

    private static readonly Counter<long> Broadcasts =
        Meter.CreateCounter<long>("truckload.broadcast.attempts", description: "Twitch PubSub broadcast decisions by outcome.");

    private static readonly Histogram<long> PayloadBytes =
        Meter.CreateHistogram<long>("truckload.ingest.payload_size", unit: "By", description: "Size of accepted ingest payloads.");

    public static void RecordIngest(string outcome) =>
        IngestRequests.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public static void RecordBroadcast(string outcome) =>
        Broadcasts.Add(1, new KeyValuePair<string, object?>("outcome", outcome));

    public static void RecordPayloadSize(long bytes) => PayloadBytes.Record(bytes);
}
