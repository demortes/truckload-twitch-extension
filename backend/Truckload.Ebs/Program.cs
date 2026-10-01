using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Truckload.Ebs.Configuration;
using Truckload.Ebs.Data;
using Truckload.Ebs.Endpoints;
using Truckload.Ebs.Logging;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using Truckload.Ebs.Services;

var builder = WebApplication.CreateBuilder(args);

// Logging: single-line JSON to stdout using Datadog's reserved attributes (level/timestamp/
// message/error.*). Configured first so everything after (options validation, host build,
// migrations, DB connection) is logged in this format.
builder.Logging.ClearProviders();
builder.Logging.AddConsole(o =>
{
    o.FormatterName = DatadogJsonFormatter.FormatterName;
    o.IncludeScopes = true; // carries the tracer's dd.trace_id / dd.span_id for log-trace correlation
});
builder.Logging.AddConsoleFormatter<DatadogJsonFormatter, Microsoft.Extensions.Logging.Console.ConsoleFormatterOptions>();

// OpenTelemetry metrics -> OTLP (Datadog Agent's OTLP receiver). Endpoint/protocol/temporality come
// from the standard OTEL_* env vars (see compose files). Traces are handled by the Datadog tracer,
// so only metrics are enabled here to avoid duplicate spans.
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r
        .AddService(
            serviceName: builder.Configuration["DD_SERVICE"] ?? "truckload-ebs",
            serviceVersion: builder.Configuration["DD_VERSION"])
        .AddAttributes([new("deployment.environment.name", builder.Configuration["DD_ENV"] ?? "unknown")]))
    .WithMetrics(m => m
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddRuntimeInstrumentation()
        .AddMeter(AppMetrics.MeterName)
        .AddMeter("Npgsql")
        .AddMeter("Microsoft.EntityFrameworkCore")
        .AddMeter("Microsoft.AspNetCore.RateLimiting")
        .AddOtlpExporter());

// Configuration
builder.Services.AddOptions<TwitchSettings>()
    .Bind(builder.Configuration.GetSection("Twitch"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<TwitchSettings>, TwitchSettingsValidator>();

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));
builder.Services.Configure<SiteSettings>(builder.Configuration.GetSection("Site"));

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IBroadcastThrottle, BroadcastThrottle>();
builder.Services.AddScoped<IChannelKeyService, ChannelKeyService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddHttpClient<ITwitchPubSubService, TwitchPubSubService>();

// CORS — Twitch extensions run in iframes on various domains; scope to what the API actually needs.
// Rate limits for the public write endpoints (see Configuration/RateLimitSettings.cs).
builder.Services.AddEbsRateLimiting(builder.Configuration);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .WithMethods("GET", "POST")
              .WithHeaders("Authorization", "Content-Type", "X-Api-Key");
    });
});

builder.WebHost.ConfigureKestrel(options =>
{
    // Comfortably above the telemetry contract's 4KB payload cap, well below anything abusive.
    options.Limits.MaxRequestBodySize = 16 * 1024;
});

var app = builder.Build();

app.UseCors();
app.UseRateLimiter();

// Map endpoints
app.MapHomeEndpoints();
app.MapHealthEndpoints();
app.MapIngestEndpoints();
app.MapTelemetryEndpoints();
app.MapJobHistoryEndpoints();
app.MapChannelEndpoints();

try
{
    if (args.Contains("--migrate-only"))
    {
        using var migrateScope = app.Services.CreateScope();
        var migrateDb = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
        await migrateDb.Database.MigrateAsync();
        return 0;
    }

    var dbSettings = app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
    if (dbSettings.AutoMigrate)
    {
        using var scope = app.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.MigrateAsync();
    }

    app.Run();
    return 0;
}
catch (Exception ex) when (ex is not HostAbortedException)
{
    // Startup failures (e.g. DB unreachable during migration) must also be JSON on stdout,
    // not the runtime's plain-text unhandled-exception dump.
    app.Logger.LogCritical(ex, "Application terminated unexpectedly during startup or run");
    return 1;
}

// Exposes the implicit Program class for WebApplicationFactory<Program> in the test project.
public partial class Program;
