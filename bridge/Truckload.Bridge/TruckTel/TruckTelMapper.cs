using System.Text.Json;
using Truckload.Bridge.Funbit;

namespace Truckload.Bridge.TruckTel;

/// <summary>
/// Converts TruckTel's "flat" REST representation (a JSON object keyed by the SCS telemetry
/// SDK's period-separated channel/config names, e.g. <c>truck.wear.engine</c>) into the
/// <see cref="FunbitTelemetry"/> model the rest of the bridge already maps to the contract.
/// Units are the SCS SDK's: kilometers, liters, meters, wear as a 0-1 fraction, times in
/// game-minutes / seconds. Missing or null keys are simply left at their defaults.
/// </summary>
public static class TruckTelMapper
{
    public static FunbitTelemetry ToFunbit(IReadOnlyDictionary<string, JsonElement> flat)
    {
        var gameId = GetString(flat, "game.id");

        var game = new FunbitGame
        {
            // The plugin lives inside the game, so a response with game data means we are connected.
            Connected = gameId is not null,
            Paused = GetBool(flat, "frame.paused"),
            GameName = gameId,
        };

        var truck = new FunbitTruck
        {
            Make = GetString(flat, "truck.brand"),
            Model = GetString(flat, "truck.name"),
            LicensePlate = GetString(flat, "truck.license.plate"),
            Fuel = GetDouble(flat, "truck.fuel.amount"),
            FuelCapacity = GetDouble(flat, "truck.fuel.capacity"),
            Odometer = GetDouble(flat, "truck.odometer"),
            WearEngine = GetDouble(flat, "truck.wear.engine"),
            WearTransmission = GetDouble(flat, "truck.wear.transmission"),
            WearCabin = GetDouble(flat, "truck.wear.cabin"),
            WearChassis = GetDouble(flat, "truck.wear.chassis"),
            WearWheels = GetDouble(flat, "truck.wear.wheels"),
        };

        var trailer = new FunbitTrailer
        {
            // The contract's "cargo" is what is being hauled; prefer the job's cargo name.
            Name = GetString(flat, "job.cargo") ?? GetString(flat, "trailer.name") ?? GetString(flat, "trailer.0.name"),
            Attached = GetBool(flat, "trailer.connected"),
        };

        // delivery.time is an absolute game-minute deadline; game.time is the current game-minute.
        DateTime? remaining = null;
        if (TryGetDouble(flat, "job.delivery.time", out var deadline) && TryGetDouble(flat, "game.time", out var now))
            remaining = MinutesToFunbitTimespan(Math.Max(0, deadline - now));

        var job = new FunbitJob
        {
            Income = GetDouble(flat, "job.income"),
            SourceCity = GetString(flat, "job.source.city"),
            DestinationCity = GetString(flat, "job.destination.city"),
            RemainingTime = remaining,
        };

        DateTime? etaSpan = TryGetDouble(flat, "truck.navigation.time", out var etaSeconds)
            ? MinutesToFunbitTimespan(Math.Max(0, etaSeconds) / 60.0)
            : null;

        var navigation = new FunbitNavigation
        {
            EstimatedDistance = GetDouble(flat, "truck.navigation.distance"), // meters
            EstimatedTime = etaSpan,
        };

        return new FunbitTelemetry { Game = game, Truck = truck, Trailer = trailer, Job = job, Navigation = navigation };
    }

    /// <summary>Funbit encodes timespans as a DateTime offset from 0001-01-01, which the mapper decodes.</summary>
    private static DateTime MinutesToFunbitTimespan(double minutes) => DateTime.MinValue + TimeSpan.FromMinutes(minutes);

    // Keys with both a value and children appear as "key._" in the struct form only; check both
    // spellings so a future flat-format change can't silently blank a field.
    private static bool TryGetElement(IReadOnlyDictionary<string, JsonElement> flat, string key, out JsonElement value)
    {
        if (flat.TryGetValue(key, out value) && IsPresent(value))
            return true;
        return flat.TryGetValue(key + "._", out value) && IsPresent(value);
    }

    private static bool IsPresent(JsonElement v) => v.ValueKind is not (JsonValueKind.Null or JsonValueKind.Undefined);

    private static string? GetString(IReadOnlyDictionary<string, JsonElement> flat, string key)
    {
        if (!TryGetElement(flat, key, out var v))
            return null;
        var s = v.ValueKind == JsonValueKind.String ? v.GetString() : v.ToString();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static bool GetBool(IReadOnlyDictionary<string, JsonElement> flat, string key) =>
        TryGetElement(flat, key, out var v) && v.ValueKind == JsonValueKind.True;

    private static double GetDouble(IReadOnlyDictionary<string, JsonElement> flat, string key) =>
        TryGetDouble(flat, key, out var d) ? d : 0;

    private static bool TryGetDouble(IReadOnlyDictionary<string, JsonElement> flat, string key, out double value)
    {
        value = 0;
        return TryGetElement(flat, key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out value);
    }
}
