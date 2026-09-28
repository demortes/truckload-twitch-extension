namespace Truckload.Ebs.Entities;

/// <summary>
/// A completed job archived for a channel, so viewers who join mid-stream can see recent
/// deliveries. Written by <see cref="Services.TelemetryService"/> when it detects an
/// active-to-completed job transition while upserting the latest snapshot; retention is
/// capped per channel (see <see cref="Services.TelemetryService.MaxJobHistoryRowsPerChannel"/>),
/// so this is session-scoped history, not a full archive.
/// </summary>
public class JobHistoryEntry
{
    public Guid Id { get; set; }
    public string ChannelId { get; set; } = string.Empty;
    public string Cargo { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public int Distance { get; set; }
    public DateTime CompletedAt { get; set; } = DateTime.UtcNow;

    public Channel Channel { get; set; } = null!;
}
