using Truckload.Ebs.Entities;

namespace Truckload.Ebs.Services;

public interface ITelemetryService
{
    Task UpsertAsync(string channelId, string payloadJson);
    Task<string?> GetLatestAsync(string channelId);

    /// <summary>Returns up to <paramref name="limit"/> of a channel's most recently completed jobs, newest first.</summary>
    Task<IReadOnlyList<JobHistoryEntry>> GetHistoryAsync(string channelId, int limit);
}
