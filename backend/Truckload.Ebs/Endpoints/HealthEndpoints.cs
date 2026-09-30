using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Truckload.Ebs.Data;
using Truckload.Ebs.Models;

namespace Truckload.Ebs.Endpoints;

public static class HealthEndpoints
{
    private static readonly string Version =
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    public static void MapHealthEndpoints(this WebApplication app)
    {
        // Liveness: process is up and serving. Deliberately does not touch the database, so a DB
        // outage doesn't get the container restarted (Docker HEALTHCHECK / k8s liveness use this).
        app.MapGet("/api/health/live", () => Results.Ok(new HealthResponse
        {
            Status = "ok",
            Version = Version,
            Db = "unchecked",
        }));

        // Readiness (also served at /api/health for backwards compatibility): includes a DB check.
        app.MapGet("/api/health/ready", CheckReadinessAsync);
        app.MapGet("/api/health", CheckReadinessAsync);
    }

    private static async Task<IResult> CheckReadinessAsync(AppDbContext db)
    {
        var dbOk = await CanConnectAsync(db);

        var response = new HealthResponse
        {
            Status = dbOk ? "ok" : "degraded",
            Version = Version,
            Db = dbOk ? "ok" : "error",
        };

        return dbOk ? Results.Ok(response) : Results.Json(response, statusCode: 503);
    }

    private static async Task<bool> CanConnectAsync(AppDbContext db)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            return await db.Database.CanConnectAsync(cts.Token);
        }
        catch
        {
            return false;
        }
    }
}
