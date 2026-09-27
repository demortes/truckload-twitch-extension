namespace Truckload.Ebs.Configuration;

public class DatabaseSettings
{
    /// <summary>When true, pending EF Core migrations are applied automatically on startup, in every environment.</summary>
    public bool AutoMigrate { get; set; } = true;
}
