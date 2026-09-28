using Truckload.Ebs.Models;
using Truckload.Ebs.Services;

namespace Truckload.Ebs.Endpoints;

public static class JobHistoryEndpoints
{
    private const int DefaultLimit = 20;
    private const int MaxLimit = 100;

    public static void MapJobHistoryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/telemetry/{channelId}/history", async (
            string channelId,
            int? limit,
            ITelemetryService telemetryService) =>
        {
            var take = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);

            var entries = await telemetryService.GetHistoryAsync(channelId, take);

            var response = entries.Select(e => new JobHistoryEntryResponse
            {
                Id = e.Id,
                Cargo = e.Cargo,
                Source = e.Source,
                Destination = e.Destination,
                Distance = e.Distance,
                CompletedAt = e.CompletedAt,
            });

            return Results.Ok(response);
        });
    }
}
