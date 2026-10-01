using System.Text.Json.Serialization;

namespace Truckload.Contracts;

/// <summary>
/// Source-generated serialization context for <see cref="TelemetryPayload"/>.
/// Using source generation (rather than reflection-based System.Text.Json)
/// keeps the bridge app safe to publish as a trimmed, self-contained single file.
/// </summary>
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
    WriteIndented = false)]
[JsonSerializable(typeof(TelemetryPayload))]
[JsonSerializable(typeof(JobInfo))]
[JsonSerializable(typeof(TruckInfo))]
[JsonSerializable(typeof(DashboardInfo))]
[JsonSerializable(typeof(TelemetryEvent))]
[JsonSerializable(typeof(IReadOnlyList<TelemetryEvent>))]
public partial class TelemetryJsonContext : JsonSerializerContext
{
}
