using Truckload.Ebs.Models;
using Truckload.Ebs.Services;

namespace Truckload.Ebs.Endpoints;

public static class IngestEndpoints
{
    public static void MapIngestEndpoints(this WebApplication app)
    {
        app.MapPost("/api/ingest", async (
            HttpContext context,
            IChannelKeyService keyService,
            ITelemetryService telemetryService,
            ITwitchPubSubService pubSub) =>
        {
            // Extract API key from header or query param
            var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
                      ?? context.Request.Query["key"].FirstOrDefault();

            if (string.IsNullOrEmpty(apiKey))
                return Results.Json(new ErrorResponse { Error = "Missing API key" }, statusCode: 401);

            var channelId = await keyService.ValidateKeyAsync(apiKey);
            if (channelId is null)
                return Results.Json(new ErrorResponse { Error = "Invalid API key" }, statusCode: 401);

            // Read raw JSON body
            context.Request.EnableBuffering();
            using var reader = new StreamReader(context.Request.Body);
            var rawJson = await reader.ReadToEndAsync();

            if (string.IsNullOrWhiteSpace(rawJson))
                return Results.Json(new ErrorResponse { Error = "Empty request body" }, statusCode: 400);

            // Persist latest telemetry
            await telemetryService.UpsertAsync(channelId, rawJson);

            // Broadcast to Twitch PubSub
            var success = await pubSub.BroadcastAsync(channelId, rawJson);
            if (!success)
                return Results.Json(new ErrorResponse { Error = "Failed to broadcast to Twitch" }, statusCode: 502);

            return Results.Ok(new { success = true, message = "Broadcasted to Twitch" });
        });
    }
}
