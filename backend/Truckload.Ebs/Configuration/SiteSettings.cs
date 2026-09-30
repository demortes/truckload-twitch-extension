namespace Truckload.Ebs.Configuration;

/// <summary>Links shown on the API's public landing page (GET /).</summary>
public class SiteSettings
{
    public string ExtensionUrl { get; set; } =
        "https://dashboard.twitch.tv/extensions/4xg4ruobtcst87wrh63xur9wkcq8ah-0.0.1";

    public string BridgeDownloadUrl { get; set; } =
        "https://github.com/demortes/truckload-twitch-extension/releases/latest";

    public string DocsUrl { get; set; } =
        "https://github.com/demortes/truckload-twitch-extension/blob/main/docs/streamer-setup.md";
}
