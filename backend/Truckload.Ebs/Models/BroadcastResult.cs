namespace Truckload.Ebs.Models;

public enum BroadcastOutcome
{
    Sent,
    Throttled,
    RateLimited,
    Failed,
    Disabled,
}

public record BroadcastResult(BroadcastOutcome Outcome, int? StatusCode = null, TimeSpan? RetryAfter = null)
{
    public string OutcomeText => Outcome switch
    {
        BroadcastOutcome.Sent => "sent",
        BroadcastOutcome.Throttled => "throttled",
        BroadcastOutcome.RateLimited => "rate_limited",
        BroadcastOutcome.Failed => "failed",
        BroadcastOutcome.Disabled => "disabled",
        _ => "unknown",
    };
}
