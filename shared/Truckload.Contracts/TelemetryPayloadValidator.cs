using System.Text.Json;

namespace Truckload.Contracts;

public static class TelemetryPayloadValidator
{
    /// <summary>Maximum accepted size, in bytes, of the serialized JSON payload.</summary>
    public const int MaxBodyBytes = 4096;

    private const int MaxStringLength = 64;
    private const int SupportedSchemaVersion = 1;

    /// <summary>Maximum number of events allowed in a single payload's <c>events</c> list.</summary>
    private const int MaxEvents = 5;

    private static readonly string[] AllowedEventSeverities = { "info", "warning", "critical" };

    /// <summary>Validates a deserialized payload against the v1 contract's rules. Returns an empty list when valid.</summary>
    public static IReadOnlyList<string> Validate(TelemetryPayload? payload)
    {
        var errors = new List<string>();

        if (payload is null)
        {
            errors.Add("Payload is missing or could not be parsed.");
            return errors;
        }

        if (payload.V != SupportedSchemaVersion)
            errors.Add($"Unsupported schema version '{payload.V}'. Expected {SupportedSchemaVersion}.");

        if (payload.Units != TelemetryUnits.Imperial && payload.Units != TelemetryUnits.Metric)
            errors.Add($"'units' must be '{TelemetryUnits.Imperial}' or '{TelemetryUnits.Metric}'.");

        if (payload.Game is { Length: > 0 } game && game.Length > MaxStringLength)
            errors.Add("'game' exceeds maximum length.");

        if (payload.Connected)
        {
            if (payload.Job is { } job)
                ValidateJob(job, errors);

            if (payload.Truck is { } truck)
                ValidateTruck(truck, errors);
        }

        if (payload.Events is { } events)
            ValidateEvents(events, errors);

        if (payload.Dashboard is { } dashboard)
            ValidateDashboard(dashboard, errors);

        return errors;
    }

    /// <summary>Validates that the serialized form of the payload does not exceed <see cref="MaxBodyBytes"/>.</summary>
    public static bool WithinSizeLimit(ReadOnlySpan<byte> utf8Json) => utf8Json.Length <= MaxBodyBytes;

    private static void ValidateJob(JobInfo job, List<string> errors)
    {
        CheckString(nameof(job.Cargo), job.Cargo, errors);
        CheckString(nameof(job.Source), job.Source, errors);
        CheckString(nameof(job.Destination), job.Destination, errors);

        if (job.Distance < 0)
            errors.Add("'job.distance' must not be negative.");

        if (job.EtaMinutes < 0)
            errors.Add("'job.etaMinutes' must not be negative.");
    }

    private static void ValidateTruck(TruckInfo truck, List<string> errors)
    {
        CheckString(nameof(truck.Make), truck.Make, errors);
        CheckString(nameof(truck.Model), truck.Model, errors);

        if (truck.LicensePlate is { Length: > MaxStringLength })
            errors.Add("'truck.licensePlate' exceeds maximum length.");

        if (truck.FuelPercent is < 0 or > 100)
            errors.Add("'truck.fuelPercent' must be between 0 and 100.");

        if (truck.DamagePercent is < 0 or > 100)
            errors.Add("'truck.damagePercent' must be between 0 and 100.");

        if (truck.Odometer < 0)
            errors.Add("'truck.odometer' must not be negative.");
    }

    private const int MaxSpeed = 500;
    private const int MaxRestMinutes = 7 * 24 * 60;

    private static void ValidateDashboard(DashboardInfo dashboard, List<string> errors)
    {
        if (dashboard.Speed is < 0 or > MaxSpeed)
            errors.Add($"'dashboard.speed' must be between 0 and {MaxSpeed}.");

        if (dashboard.RestMinutes is < 0 or > MaxRestMinutes)
            errors.Add($"'dashboard.restMinutes' must be between 0 and {MaxRestMinutes}.");

        if (!DashboardValues.Signals.Contains(dashboard.Signal))
            errors.Add($"'dashboard.signal' must be one of: {string.Join(", ", DashboardValues.Signals)}.");

        if (!DashboardValues.Lights.Contains(dashboard.Lights))
            errors.Add($"'dashboard.lights' must be one of: {string.Join(", ", DashboardValues.Lights)}.");

        if (dashboard.Warnings is null)
        {
            errors.Add("'dashboard.warnings' must be a list (empty when there are none).");
            return;
        }

        if (dashboard.Warnings.Count > DashboardValues.Warnings.Length)
            errors.Add($"'dashboard.warnings' must not contain more than {DashboardValues.Warnings.Length} entries.");

        foreach (var warning in dashboard.Warnings)
        {
            if (!DashboardValues.Warnings.Contains(warning))
                errors.Add($"'dashboard.warnings[]' entries must be one of: {string.Join(", ", DashboardValues.Warnings)}.");
        }
    }

    private static void ValidateEvents(IReadOnlyList<TelemetryEvent> events, List<string> errors)
    {
        if (events.Count > MaxEvents)
        {
            errors.Add($"'events' must not contain more than {MaxEvents} entries.");
            return;
        }

        foreach (var evt in events)
        {
            if (string.IsNullOrWhiteSpace(evt.Type))
                errors.Add("'events[].type' must not be empty.");
            CheckString("events[].type", evt.Type, errors);

            if (!AllowedEventSeverities.Contains(evt.Severity))
                errors.Add($"'events[].severity' must be one of: {string.Join(", ", AllowedEventSeverities)}.");

            if (string.IsNullOrWhiteSpace(evt.Message))
                errors.Add("'events[].message' must not be empty.");
            if (evt.Message is { Length: > 200 })
                errors.Add("'events[].message' exceeds maximum length.");
        }
    }

    private static void CheckString(string field, string? value, List<string> errors)
    {
        if (value is { Length: > MaxStringLength })
            errors.Add($"'{field}' exceeds maximum length.");
    }
}
