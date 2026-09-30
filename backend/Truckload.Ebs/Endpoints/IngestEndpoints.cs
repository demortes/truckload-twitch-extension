using System.Text.Json;
using Truckload.Contracts;
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
            ITwitchPubSubService pubSub,
            IBroadcastThrottle throttle,
            ILogger<Program> logger) =>
        {
            var apiKey = context.Request.Headers["X-Api-Key"].FirstOrDefault()
                      ?? context.Request.Query["key"].FirstOrDefault();

            if (string.IsNullOrEmpty(apiKey))
                {
                AppMetrics.RecordIngest("missing_key");
                return Results.Json(new ErrorResponse { Error = "Missing API key" }, statusCode: 401);
            }

            var channelId = await keyService.ValidateKeyAsync(apiKey);
            if (channelId is null)
                {
                AppMetrics.RecordIngest("invalid_key");
                return Results.Json(new ErrorResponse { Error = "Invalid API key" }, statusCode: 401);
            }

            if (context.Request.ContentLength is { } contentLength && contentLength > TelemetryPayloadValidator.MaxBodyBytes)
            {
                AppMetrics.RecordIngest("too_large");
                return Results.Json(
                    new ErrorResponse { Error = $"Request body exceeds {TelemetryPayloadValidator.MaxBodyBytes} bytes" },
                    statusCode: 413);
            }

            byte[] rawBytes;
            using (var buffer = new MemoryStream())
            {
                await context.Request.Body.CopyToAsync(buffer, TelemetryPayloadValidator.MaxBodyBytes + 1);
                rawBytes = buffer.ToArray();
            }

            if (rawBytes.Length == 0)
                {
                AppMetrics.RecordIngest("empty");
                return Results.Json(new ErrorResponse { Error = "Empty request body" }, statusCode: 400);
            }

            if (!TelemetryPayloadValidator.WithinSizeLimit(rawBytes))
            {
                AppMetrics.RecordIngest("too_large");
                return Results.Json(
                    new ErrorResponse { Error = $"Request body exceeds {TelemetryPayloadValidator.MaxBodyBytes} bytes" },
                    statusCode: 413);
            }

            TelemetryPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize(rawBytes, TelemetryJsonContext.Default.TelemetryPayload);
            }
            catch (JsonException ex)
            {
                // Log details internally only — the raw exception message can echo back
                // fragments of the request body / internal type names and shouldn't be
                // exposed to a caller that only needs to know its payload was rejected.
                AppMetrics.RecordIngest("malformed");
                logger.LogInformation(ex, "Rejected malformed ingest payload for channel {ChannelId}", channelId);
                return Results.Json(
                    new ErrorResponse { Error = "Invalid telemetry payload" },
                    statusCode: 400);
            }

            var errors = TelemetryPayloadValidator.Validate(payload);
            if (errors.Count > 0)
            {
                AppMetrics.RecordIngest("invalid");
                return Results.Json(
                    new ErrorResponse { Error = "Telemetry payload failed validation", Details = string.Join(" ", errors) },
                    statusCode: 400);
            }

            var canonicalJson = JsonSerializer.Serialize(payload!, TelemetryJsonContext.Default.TelemetryPayload);

            await telemetryService.UpsertAsync(channelId, canonicalJson);
            AppMetrics.RecordIngest("stored");
            AppMetrics.RecordPayloadSize(rawBytes.Length);

            if (!throttle.TryAcquire(channelId))
            {
                AppMetrics.RecordBroadcast("throttled");
                return Results.Ok(new { stored = true, broadcast = "throttled" });
            }

            var result = await pubSub.BroadcastAsync(channelId, canonicalJson, context.RequestAborted);

            AppMetrics.RecordBroadcast(result.OutcomeText);

            if (result.Outcome == BroadcastOutcome.RateLimited && result.RetryAfter is { } retryAfter)
                throttle.Suppress(channelId, retryAfter);

            return Results.Ok(new
            {
                stored = true,
                broadcast = result.OutcomeText,
                retryAfterMs = result.RetryAfter?.TotalMilliseconds,
            });
        });
    }
}
