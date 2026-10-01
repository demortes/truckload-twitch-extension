namespace Truckload.Contracts;

/// <summary>
/// The canonical telemetry contract (schema version 1) sent by the bridge app,
/// stored by the backend, and consumed identically by every frontend view.
/// </summary>
public sealed record TelemetryPayload(
    int V,
    long Ts,
    bool Connected,
    bool Paused,
    string? Game,
    string Units,
    JobInfo? Job,
    TruckInfo? Truck,
    IReadOnlyList<TelemetryEvent>? Events = null,
    DashboardInfo? Dashboard = null
);

public sealed record JobInfo(
    bool Active,
    string Cargo,
    string Source,
    string Destination,
    int Distance,
    int EtaMinutes
);

public sealed record TruckInfo(
    string Make,
    string Model,
    string? LicensePlate,
    int FuelPercent,
    int DamagePercent,
    int Odometer
);

/// <summary>
/// Live instrument-cluster state, shown on the panel/overlay "dashboard". Unlike
/// <see cref="TruckInfo"/> (slow-moving stats), this changes constantly (speed) or flickers
/// (signals), so it is optional and omitted when the source can't provide it.
/// </summary>
public sealed record DashboardInfo(
    /// <summary>Current speed (absolute value), in the payload's units: mph for imperial, km/h for metric.</summary>
    int Speed,
    /// <summary>Turn signals: one of <see cref="DashboardValues.Signals"/>.</summary>
    string Signal,
    /// <summary>Headlights: one of <see cref="DashboardValues.Lights"/>.</summary>
    string Lights,
    bool Wipers,
    /// <summary>Active warning lamps, each one of <see cref="DashboardValues.Warnings"/>. Empty when none.</summary>
    IReadOnlyList<string> Warnings
);

/// <summary>The fixed vocabularies <see cref="DashboardInfo"/> may use.</summary>
public static class DashboardValues
{
    public static readonly string[] Signals = ["off", "left", "right", "hazard"];
    public static readonly string[] Lights = ["off", "parking", "low", "high"];

    /// <summary>fuel, oil pressure, coolant temperature, battery voltage, AdBlue, air pressure, parking brake.</summary>
    public static readonly string[] Warnings = ["fuel", "oil", "coolant", "battery", "adblue", "air", "parkingBrake"];
}

/// <summary>
/// A one-off, transient in-game event (e.g. a crash) detected by the bridge since its
/// last tick. Unlike the rest of the payload, which is a full re-sent snapshot of
/// steady-state, <see cref="TelemetryPayload.Events"/> is only populated on the tick
/// where something noteworthy happened and is null/omitted otherwise — consumers should
/// treat it as "something happened just now", not as ongoing state to keep displaying.
/// </summary>
public sealed record TelemetryEvent(
    string Type,
    string Severity,
    string Message
);
