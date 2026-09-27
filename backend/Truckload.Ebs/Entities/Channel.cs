namespace Truckload.Ebs.Entities;

public class Channel
{
    public string ChannelId { get; set; } = string.Empty;
    public string IngestKey { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TelemetrySnapshot? LatestTelemetry { get; set; }
    public List<JobHistoryEntry> JobHistory { get; set; } = new();
}
