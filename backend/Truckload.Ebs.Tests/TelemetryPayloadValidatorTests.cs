using Truckload.Contracts;
using Xunit;

namespace Truckload.Ebs.Tests;

public class TelemetryPayloadValidatorTests
{
    private static readonly TelemetryPayload Valid = new(
        V: 1, Ts: 0, Connected: true, Paused: false, Game: "ats", Units: "imperial",
        Job: new JobInfo(true, "Electronics", "Los Angeles", "Phoenix", 372, 245),
        Truck: new TruckInfo("Peterbilt", "579", "TRK-4521", 67, 3, 124532));

    [Fact]
    public void Validate_AcceptsAValidPayload() =>
        Assert.Empty(TelemetryPayloadValidator.Validate(Valid));

    [Fact]
    public void Validate_RejectsNull() =>
        Assert.NotEmpty(TelemetryPayloadValidator.Validate(null));

    [Fact]
    public void Validate_RejectsWrongSchemaVersion() =>
        Assert.NotEmpty(TelemetryPayloadValidator.Validate(Valid with { V = 2 }));

    [Theory]
    [InlineData("furlongs")]
    [InlineData("")]
    public void Validate_RejectsUnknownUnits(string units) =>
        Assert.NotEmpty(TelemetryPayloadValidator.Validate(Valid with { Units = units }));

    [Fact]
    public void Validate_RejectsFuelPercentOutOfRange() =>
        Assert.NotEmpty(TelemetryPayloadValidator.Validate(Valid with { Truck = Valid.Truck! with { FuelPercent = 101 } }));

    [Fact]
    public void Validate_RejectsNegativeDistance() =>
        Assert.NotEmpty(TelemetryPayloadValidator.Validate(Valid with { Job = Valid.Job! with { Distance = -1 } }));

    [Fact]
    public void Validate_AllowsNullJobAndTruckWhenDisconnected() =>
        Assert.Empty(TelemetryPayloadValidator.Validate(Valid with { Connected = false, Job = null, Truck = null, Game = null }));
}
