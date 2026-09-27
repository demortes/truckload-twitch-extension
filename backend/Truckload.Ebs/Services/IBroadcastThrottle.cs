namespace Truckload.Ebs.Services;

/// <summary>
/// Per-channel rate limiting for outbound Twitch PubSub broadcasts, so a fast-polling
/// bridge cannot exceed Twitch's per-channel Extension PubSub rate limit.
/// </summary>
public interface IBroadcastThrottle
{
    /// <summary>Returns true if a broadcast for this channel may proceed now, and records that it did.</summary>
    bool TryAcquire(string channelId);

    /// <summary>Suppresses further broadcasts for this channel until the given duration elapses (e.g. after a 429).</summary>
    void Suppress(string channelId, TimeSpan duration);
}
