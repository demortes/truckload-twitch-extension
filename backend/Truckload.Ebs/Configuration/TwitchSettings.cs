namespace Truckload.Ebs.Configuration;

public class TwitchSettings
{
    public string ClientId { get; set; } = string.Empty;
    public string ExtensionSecret { get; set; } = string.Empty; // Base64-encoded

    /// <summary>When false, telemetry is stored but never relayed over Twitch PubSub. Useful for local/dev.</summary>
    public bool BroadcastEnabled { get; set; } = true;

    /// <summary>Minimum time between PubSub broadcasts to the same channel.</summary>
    public int MinBroadcastIntervalMs { get; set; } = 1000;

    public string PubSubUrl { get; set; } = "https://api.twitch.tv/helix/extensions/pubsub";
}
