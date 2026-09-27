using Microsoft.Extensions.Options;

namespace Truckload.Ebs.Configuration;

/// <summary>
/// Fails application startup with a clear error rather than letting the first
/// ingest request throw from inside <c>Convert.FromBase64String</c>.
/// </summary>
public class TwitchSettingsValidator : IValidateOptions<TwitchSettings>
{
    public ValidateOptionsResult Validate(string? name, TwitchSettings options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.ClientId))
            failures.Add("Twitch:ClientId is required.");

        if (string.IsNullOrWhiteSpace(options.ExtensionSecret))
        {
            failures.Add("Twitch:ExtensionSecret is required.");
        }
        else
        {
            try
            {
                Convert.FromBase64String(options.ExtensionSecret);
            }
            catch (FormatException)
            {
                failures.Add("Twitch:ExtensionSecret must be valid base64 (as shown in the Twitch developer console).");
            }
        }

        if (options.MinBroadcastIntervalMs < 0)
            failures.Add("Twitch:MinBroadcastIntervalMs must not be negative.");

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
