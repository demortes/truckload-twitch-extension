using Truckload.Bridge.Mapping;
using Truckload.Contracts;
using Xunit;

namespace Truckload.Bridge.Tests;

public class TelemetryEventDetectorTests
{
    private static TruckInfo Truck(int damagePercent) =>
        new("Peterbilt", "579", "TRK-4521", FuelPercent: 50, DamagePercent: damagePercent, Odometer: 1000);

    [Fact]
    public void Detect_NoPreviousTick_ReturnsNull() =>
        Assert.Null(TelemetryEventDetector.Detect(previousTruck: null, currentTruck: Truck(5)));

    [Fact]
    public void Detect_NoCurrentTruck_ReturnsNull() =>
        Assert.Null(TelemetryEventDetector.Detect(previousTruck: Truck(5), currentTruck: null));

    [Fact]
    public void Detect_SmallDamageIncrease_IsIgnored() =>
        Assert.Null(TelemetryEventDetector.Detect(previousTruck: Truck(5), currentTruck: Truck(10)));

    [Fact]
    public void Detect_DamageDecrease_IsIgnored() =>
        // e.g. the truck was repaired at a service station between ticks.
        Assert.Null(TelemetryEventDetector.Detect(previousTruck: Truck(40), currentTruck: Truck(5)));

    [Fact]
    public void Detect_LargeDamageJump_ProducesACrashEvent()
    {
        var events = TelemetryEventDetector.Detect(previousTruck: Truck(5), currentTruck: Truck(25));

        Assert.NotNull(events);
        var evt = Assert.Single(events);
        Assert.Equal("crash", evt.Type);
        Assert.Equal("warning", evt.Severity);
        Assert.Contains("20%", evt.Message);
    }

    [Fact]
    public void Detect_JumpExactlyAtThreshold_ProducesACrashEvent() =>
        Assert.NotNull(TelemetryEventDetector.Detect(
            previousTruck: Truck(0),
            currentTruck: Truck(TelemetryEventDetector.CrashDamageJumpThreshold)));
}
