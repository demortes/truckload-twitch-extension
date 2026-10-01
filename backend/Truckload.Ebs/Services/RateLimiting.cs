using System.Globalization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Truckload.Ebs.Configuration;
using Truckload.Ebs.Models;

namespace Truckload.Ebs.Services;

/// <summary>Rate limiting for the public write endpoints; see <see cref="RateLimitSettings"/> for the reasoning.</summary>
public static class RateLimiting
{
    public const string IngestKeyPolicy = "ingest-key";

    private const string MalformedKeyPartition = "malformed";

    public static IServiceCollection AddEbsRateLimiting(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RateLimitSettings>(configuration.GetSection("RateLimiting"));

        // Settings come from IOptions (resolved when the limiter is first built), not read eagerly at startup, so
        // configuration supplied later (environment variables, test overrides) is honoured like every other setting.
        services.AddRateLimiter(_ => { });
        services.AddOptions<RateLimiterOptions>().Configure<IOptions<RateLimitSettings>>((options, rateLimits) =>
        {
            var settings = rateLimits.Value;
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

            // Per ingest key. Only well-formed keys get their own bucket; everything else shares one, so a caller
            // spraying random strings can neither create unbounded partitions nor dodge the limit.
            options.AddPolicy(IngestKeyPolicy, context =>
                RateLimitPartition.GetTokenBucketLimiter(
                    PartitionKeyFor(context.Request.Headers["X-Api-Key"].FirstOrDefault()),
                    _ => Bucket(settings.IngestPerKeyPerSecond)));

            // Global ceilings per endpoint group. Everything else is unlimited here.
            options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
            {
                var path = context.Request.Path;
                if (path.StartsWithSegments("/api/ingest"))
                    return RateLimitPartition.GetTokenBucketLimiter("ingest", _ => Bucket(settings.IngestGlobalPerSecond));
                if (path.StartsWithSegments("/api/channels/keys"))
                    return RateLimitPartition.GetTokenBucketLimiter("keys", _ => Bucket(settings.KeyManagementGlobalPerSecond));
                return RateLimitPartition.GetNoLimiter("unlimited");
            });

            options.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var delay)
                    ? Math.Max(1, (int)Math.Ceiling(delay.TotalSeconds))
                    : 1;
                context.HttpContext.Response.Headers.RetryAfter = retryAfter.ToString(CultureInfo.InvariantCulture);

                if (context.HttpContext.Request.Path.StartsWithSegments("/api/ingest"))
                    AppMetrics.RecordIngest("rate_limited");

                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ErrorResponse { Error = "Too many requests. Slow down and retry shortly." }, cancellationToken);
            };
        });

        return services;
    }

    /// <summary>A key shaped like a real one (64 hex characters) is its own partition, anything else is shared.</summary>
    public static string PartitionKeyFor(string? apiKey) =>
        apiKey is { Length: 64 } && apiKey.All(Uri.IsHexDigit) ? apiKey : MalformedKeyPartition;

    private static TokenBucketRateLimiterOptions Bucket(int perSecond)
    {
        var rate = Math.Max(1, perSecond);
        return new TokenBucketRateLimiterOptions
        {
            TokenLimit = rate * 2, // burst
            TokensPerPeriod = rate,
            ReplenishmentPeriod = TimeSpan.FromSeconds(1),
            QueueLimit = 0,
            AutoReplenishment = true,
        };
    }
}
