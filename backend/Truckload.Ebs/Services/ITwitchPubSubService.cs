using Truckload.Ebs.Models;

namespace Truckload.Ebs.Services;

public interface ITwitchPubSubService
{
    Task<BroadcastResult> BroadcastAsync(string channelId, string messageJson, CancellationToken cancellationToken = default);
}
