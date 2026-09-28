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
    IReadOnlyList<TelemetryEvent>? Events = null
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
