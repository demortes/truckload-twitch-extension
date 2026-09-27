namespace Truckload.Contracts;

public static class TelemetryUnits
{
    public const string Imperial = "imperial";
    public const string Metric = "metric";

    /// <summary>
    /// v1 rule: American Truck Simulator reports in imperial, Euro Truck Simulator 2 in metric.
    /// </summary>
    public static string ForGame(string? game) =>
        string.Equals(game, "ets2", StringComparison.OrdinalIgnoreCase) ? Metric : Imperial;

    private const double MetersPerMile = 1609.34;
    private const double KmPerMile = 1.60934;

    /// <summary>Converts a distance in meters to whole miles or kilometers depending on <paramref name="units"/>.</summary>
    public static int MetersToDistance(double meters, string units) =>
        units == Metric
            ? (int)Math.Round(meters / 1000.0)
            : (int)Math.Round(meters / MetersPerMile);

    /// <summary>Converts a distance already in kilometers to whole miles or kilometers depending on <paramref name="units"/>.</summary>
    public static int KmToDistance(double km, string units) =>
        units == Metric
            ? (int)Math.Round(km)
            : (int)Math.Round(km / KmPerMile);
}
