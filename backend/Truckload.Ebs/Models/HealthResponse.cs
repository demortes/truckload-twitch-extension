namespace Truckload.Ebs.Models;

public class HealthResponse
{
    public string Status { get; set; } = "ok";
    public string Version { get; set; } = "1.0.0";
    public string Db { get; set; } = "ok";
}
