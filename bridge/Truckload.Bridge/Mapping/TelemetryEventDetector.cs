using Truckload.Contracts;

namespace Truckload.Bridge.Mapping;

/// <summary>
/// Detects one-off, noteworthy events by comparing the truck info of the current
/// mapped tick to the previous one. Separate from <see cref="TelemetryMapper"/> (which
/// is a pure, stateless raw-to-contract mapping) because event detection is inherently
/// stateful: it needs to remember what the previous tick looked like.
/// </summary>
public static class TelemetryEventDetector
{
    /// <summary>
    /// A same-tick increase in <see cref="TruckInfo.DamagePercent"/> at or above this many
    /// percentage points is treated as a crash/major-collision heuristic. Funbit's wear
    /// fields creep up gradually from normal wear-and-tear (well under a point per tick at
    /// the bridge's poll rate), so a jump this large in a single tick is a reasonable signal
    /// that something sudden (a crash, a rollover) happened rather than routine wear.
    /// </summary>
    public const int CrashDamageJumpThreshold = 15;

    /// <summary>
    /// Compares <paramref name="previousTruck"/> (the truck info from the last tick, whether
    /// or not that tick was actually sent) to <paramref name="currentTruck"/> and returns any
    /// events detected, or null when nothing noteworthy happened.
    /// </summary>
    public static IReadOnlyList<TelemetryEvent>? Detect(TruckInfo? previousTruck, TruckInfo? currentTruck)
    {
        if (previousTruck is null || currentTruck is null)
            return null;

        var damageJump = currentTruck.DamagePercent - previousTruck.DamagePercent;
        if (damageJump < CrashDamageJumpThreshold)
            return null;

        return new[]
        {
            new TelemetryEvent(
                Type: "crash",
                Severity: "warning",
                Message: $"Damage jumped {damageJump}% - possible crash or major collision."),
        };
    }
}
