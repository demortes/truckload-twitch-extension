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
    TruckInfo? Truck
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
