namespace Truckload.Ebs.Models;

public class JobHistoryEntryResponse
{
    public Guid Id { get; set; }
    public string Cargo { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public int Distance { get; set; }
    public DateTime CompletedAt { get; set; }
}
