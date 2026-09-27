using System.Text.Json;

namespace Truckload.Contracts;

public static class TelemetryPayloadValidator
{
    /// <summary>Maximum accepted size, in bytes, of the serialized JSON payload.</summary>
    public const int MaxBodyBytes = 4096;

    private const int MaxStringLength = 64;
    private const int SupportedSchemaVersion = 1;

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

    private static void CheckString(string field, string? value, List<string> errors)
    {
        if (value is { Length: > MaxStringLength })
            errors.Add($"'{field}' exceeds maximum length.");
    }
}
