using Truckload.Bridge.Funbit;
using Truckload.Contracts;

namespace Truckload.Bridge.Mapping;

/// <summary>
/// Pure mapping from Funbit's raw telemetry JSON to the canonical v1 contract.
/// This is the one place unit conversions and Funbit's quirky timespan-as-datetime
/// fields are handled.
/// </summary>
public static class TelemetryMapper
{
    /// <summary>A payload for when the telemetry server itself could not be reached.</summary>
    public static TelemetryPayload Disconnected(long ts) =>
        new(1, ts, Connected: false, Paused: false, Game: null, Units: TelemetryUnits.Imperial, Job: null, Truck: null);

    public static TelemetryPayload Map(FunbitTelemetry raw, long ts, string unitsMode)
    {
        var game = raw.Game?.GameName?.ToLowerInvariant();
        var connected = raw.Game?.Connected ?? false;

        if (!connected)
            return new TelemetryPayload(1, ts, false, raw.Game?.Paused ?? false, game, TelemetryUnits.Imperial, null, null);

        var units = unitsMode switch
        {
            TelemetryUnits.Imperial => TelemetryUnits.Imperial,
            TelemetryUnits.Metric => TelemetryUnits.Metric,
            _ => TelemetryUnits.ForGame(game),
        };

        return new TelemetryPayload(
            V: 1,
            Ts: ts,
            Connected: true,
            Paused: raw.Game?.Paused ?? false,
            Game: game,
            Units: units,
            Job: MapJob(raw, units),
            Truck: MapTruck(raw.Truck, units),
            Dashboard: MapDashboard(raw.Truck, units)
        );
    }

    private const double KmhPerMph = 1.60934;

    /// <summary>The instrument-cluster state, or null when there is no truck data.</summary>
    internal static DashboardInfo? MapDashboard(FunbitTruck? truck, string units)
    {
        if (truck is null)
            return null;

        var kmh = Math.Abs(truck.Speed);
        var speed = (int)Math.Round(units == TelemetryUnits.Metric ? kmh : kmh / KmhPerMph);

        // Hazards are both stalks at once (or the dedicated hazard switch); report that as one state.
        var signal = truck.HazardWarning || (truck.BlinkerLeftActive && truck.BlinkerRightActive) ? "hazard"
            : truck.BlinkerLeftActive ? "left"
            : truck.BlinkerRightActive ? "right"
            : "off";

        var lights = truck.LightsBeamHighOn ? "high"
            : truck.LightsBeamLowOn ? "low"
            : truck.LightsParkingOn ? "parking"
            : "off";

        var warnings = new List<string>(DashboardValues.Warnings.Length);
        if (truck.FuelWarningOn) warnings.Add("fuel");
        if (truck.OilPressureWarningOn) warnings.Add("oil");
        if (truck.WaterTemperatureWarningOn) warnings.Add("coolant");
        if (truck.BatteryVoltageWarningOn) warnings.Add("battery");
        if (truck.AdblueWarningOn) warnings.Add("adblue");
        if (truck.AirPressureWarningOn || truck.AirPressureEmergencyOn) warnings.Add("air");
        if (truck.ParkBrakeOn) warnings.Add("parkingBrake");

        return new DashboardInfo(speed, signal, lights, truck.WipersOn, warnings);
    }

    private static JobInfo? MapJob(FunbitTelemetry raw, string units)
    {
        var job = raw.Job;
        var hasDestination = !string.IsNullOrWhiteSpace(job?.DestinationCity);

        if (job is null || !hasDestination || job.Income <= 0)
            return null;

        var distance = TelemetryUnits.MetersToDistance(raw.Navigation?.EstimatedDistance ?? 0, units);
        var etaMinutes = ToMinutes(raw.Navigation?.EstimatedTime) ?? ToMinutes(job.RemainingTime) ?? 0;

        return new JobInfo(
            Active: true,
            Cargo: string.IsNullOrWhiteSpace(raw.Trailer?.Name) ? "Unknown" : raw.Trailer!.Name!,
            Source: job.SourceCity ?? "Unknown",
            Destination: job.DestinationCity ?? "Unknown",
            Distance: Math.Max(0, distance),
            EtaMinutes: Math.Max(0, etaMinutes)
        );
    }

    private static TruckInfo? MapTruck(FunbitTruck? truck, string units)
    {
        if (truck is null)
            return null;

        var fuelPercent = truck.FuelCapacity > 0
            ? (int)Math.Round(Math.Clamp(truck.Fuel / truck.FuelCapacity * 100.0, 0, 100))
            : 0;

        var worstWear = new[]
        {
            truck.WearEngine, truck.WearTransmission, truck.WearCabin, truck.WearChassis, truck.WearWheels,
        }.DefaultIfEmpty(0).Max();
        var damagePercent = (int)Math.Round(Math.Clamp(worstWear * 100.0, 0, 100));

        return new TruckInfo(
            Make: string.IsNullOrWhiteSpace(truck.Make) ? "Unknown" : truck.Make!,
            Model: string.IsNullOrWhiteSpace(truck.Model) ? "Unknown" : truck.Model!,
            LicensePlate: string.IsNullOrWhiteSpace(truck.LicensePlate) ? null : truck.LicensePlate,
            FuelPercent: fuelPercent,
            DamagePercent: damagePercent,
            Odometer: Math.Max(0, TelemetryUnits.KmToDistance(truck.Odometer, units))
        );
    }

    /// <summary>
    /// Funbit represents a timespan as a DateTime offset from 0001-01-01T00:00:00, not as
    /// seconds or a plain duration string. This recovers the actual number of minutes.
    /// </summary>
    private static int? ToMinutes(DateTime? funbitTimespanAsDateTime)
    {
        if (funbitTimespanAsDateTime is not { } value)
            return null;

        var span = value - DateTime.MinValue;
        return span.Ticks <= 0 ? 0 : (int)Math.Round(span.TotalMinutes);
    }
}
