namespace Truckload.Ebs.Services;

public interface ITelemetryService
{
    Task UpsertAsync(string channelId, string payloadJson);
    Task<string?> GetLatestAsync(string channelId);
}
