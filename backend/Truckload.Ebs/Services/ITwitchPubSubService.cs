namespace Truckload.Ebs.Services;

public interface ITwitchPubSubService
{
    Task<bool> BroadcastAsync(string channelId, string messageJson);
}
