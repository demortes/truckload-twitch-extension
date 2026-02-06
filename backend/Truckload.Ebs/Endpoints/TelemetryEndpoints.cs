using Truckload.Ebs.Models;
using Truckload.Ebs.Services;

namespace Truckload.Ebs.Endpoints;

public static class TelemetryEndpoints
{
    public static void MapTelemetryEndpoints(this WebApplication app)
    {
        app.MapGet("/api/telemetry/{channelId}", async (
            string channelId,
            ITelemetryService telemetryService) =>
        {
            var payload = await telemetryService.GetLatestAsync(channelId);
            if (payload is null)
                return Results.Json(
                    new ErrorResponse { Error = "No telemetry data found for this channel" },
                    statusCode: 404);

            return Results.Content(payload, "application/json");
        });
    }
}
