using System.Text.Json.Serialization;

namespace Truckload.Bridge.Funbit;

// Subset of the JSON exposed by Funbit's ETS2/ATS Telemetry Web Server
// (http://localhost:25555/api/ets2/telemetry). Unknown/extra fields are ignored.

public sealed class FunbitTelemetry
{
    [JsonPropertyName("game")] public FunbitGame? Game { get; set; }
    [JsonPropertyName("truck")] public FunbitTruck? Truck { get; set; }
    [JsonPropertyName("trailer")] public FunbitTrailer? Trailer { get; set; }
    [JsonPropertyName("job")] public FunbitJob? Job { get; set; }
    [JsonPropertyName("navigation")] public FunbitNavigation? Navigation { get; set; }
}

public sealed class FunbitGame
{
    [JsonPropertyName("connected")] public bool Connected { get; set; }
    [JsonPropertyName("paused")] public bool Paused { get; set; }
    /// <summary>e.g. "ATS" or "ETS2"; the mapper lower-cases it.</summary>
    [JsonPropertyName("gameName")] public string? GameName { get; set; }
}

public sealed class FunbitTruck
{
    [JsonPropertyName("make")] public string? Make { get; set; }
    [JsonPropertyName("model")] public string? Model { get; set; }
    [JsonPropertyName("licensePlate")] public string? LicensePlate { get; set; }
    [JsonPropertyName("fuel")] public double Fuel { get; set; }
    [JsonPropertyName("fuelCapacity")] public double FuelCapacity { get; set; }
    [JsonPropertyName("odometer")] public double Odometer { get; set; } // kilometers
    [JsonPropertyName("wearEngine")] public double WearEngine { get; set; }
    [JsonPropertyName("wearTransmission")] public double WearTransmission { get; set; }
    [JsonPropertyName("wearCabin")] public double WearCabin { get; set; }
    [JsonPropertyName("wearChassis")] public double WearChassis { get; set; }
    [JsonPropertyName("wearWheels")] public double WearWheels { get; set; }

    // Instrument-cluster fields (feed the contract's "dashboard"). The TruckTel mapper fills these
    // too, converting its SI values, so everything here is in Funbit's units.
    [JsonPropertyName("speed")] public double Speed { get; set; } // km/h, negative when reversing
    [JsonPropertyName("blinkerLeftActive")] public bool BlinkerLeftActive { get; set; } // stalk position, not the flashing lamp
    [JsonPropertyName("blinkerRightActive")] public bool BlinkerRightActive { get; set; }
    [JsonPropertyName("hazardWarning")] public bool HazardWarning { get; set; }
    [JsonPropertyName("lightsParkingOn")] public bool LightsParkingOn { get; set; }
    [JsonPropertyName("lightsBeamLowOn")] public bool LightsBeamLowOn { get; set; }
    [JsonPropertyName("lightsBeamHighOn")] public bool LightsBeamHighOn { get; set; }
    [JsonPropertyName("wipersOn")] public bool WipersOn { get; set; }
    [JsonPropertyName("fuelWarningOn")] public bool FuelWarningOn { get; set; }
    [JsonPropertyName("oilPressureWarningOn")] public bool OilPressureWarningOn { get; set; }
    [JsonPropertyName("waterTemperatureWarningOn")] public bool WaterTemperatureWarningOn { get; set; }
    [JsonPropertyName("batteryVoltageWarningOn")] public bool BatteryVoltageWarningOn { get; set; }
    [JsonPropertyName("adblueWarningOn")] public bool AdblueWarningOn { get; set; }
    [JsonPropertyName("airPressureWarningOn")] public bool AirPressureWarningOn { get; set; }
    [JsonPropertyName("airPressureEmergencyOn")] public bool AirPressureEmergencyOn { get; set; }
    [JsonPropertyName("parkBrakeOn")] public bool ParkBrakeOn { get; set; }
}

public sealed class FunbitTrailer
{
    [JsonPropertyName("name")] public string? Name { get; set; }
    [JsonPropertyName("attached")] public bool Attached { get; set; }
}

public sealed class FunbitJob
{
    [JsonPropertyName("income")] public double Income { get; set; }
    [JsonPropertyName("sourceCity")] public string? SourceCity { get; set; }
    [JsonPropertyName("destinationCity")] public string? DestinationCity { get; set; }
    /// <summary>An ISO datetime encoding a timespan relative to 0001-01-01, not a plain duration.</summary>
    [JsonPropertyName("remainingTime")] public DateTime? RemainingTime { get; set; }
}

public sealed class FunbitNavigation
{
    [JsonPropertyName("estimatedDistance")] public double EstimatedDistance { get; set; } // meters
    /// <summary>An ISO datetime encoding a timespan relative to 0001-01-01, not a plain duration.</summary>
    [JsonPropertyName("estimatedTime")] public DateTime? EstimatedTime { get; set; }
}
