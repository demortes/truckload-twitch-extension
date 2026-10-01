namespace Truckload.Ebs.Configuration;

/// <summary>
/// Request limits for the public, write-capable endpoints (section "RateLimiting"). They are generous for
/// legitimate use (the Bridge sends at most one payload per second) and exist to blunt floods against the
/// database now that the API is documented in a public repository.
///
/// Limits are per ingest key or global rather than per client IP on purpose: behind Cloudflare/a proxy the
/// client IP is only trustworthy with forwarded-header configuration that is specific to each deployment.
/// </summary>
public class RateLimitSettings
{
    /// <summary>Sustained requests per second allowed for one ingest key on POST /api/ingest (burst is twice this).</summary>
    public int IngestPerKeyPerSecond { get; set; } = 5;

    /// <summary>Sustained requests per second allowed across all callers on POST /api/ingest (burst is twice this).</summary>
    public int IngestGlobalPerSecond { get; set; } = 500;

    /// <summary>Sustained requests per second allowed across all callers on /api/channels/keys (burst is twice this).</summary>
    public int KeyManagementGlobalPerSecond { get; set; } = 20;
}
