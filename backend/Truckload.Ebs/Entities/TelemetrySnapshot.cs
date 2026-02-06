namespace Truckload.Ebs.Entities;

public class TelemetrySnapshot
{
    public string ChannelId { get; set; } = string.Empty;
    public string PayloadJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Channel Channel { get; set; } = null!;
}
