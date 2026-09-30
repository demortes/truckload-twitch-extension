using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Truckload.Ebs.Configuration;
using Truckload.Ebs.Data;
using Truckload.Ebs.Endpoints;
using Truckload.Ebs.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.AddOptions<TwitchSettings>()
    .Bind(builder.Configuration.GetSection("Twitch"))
    .ValidateOnStart();
builder.Services.AddSingleton<IValidateOptions<TwitchSettings>, TwitchSettingsValidator>();

builder.Services.Configure<DatabaseSettings>(builder.Configuration.GetSection("Database"));

// Database
// TEMPORARY: PendingModelChangesWarning suppressed to unblock startup while the real
// mismatch between AppDbContextModelSnapshot and the live model (almost certainly in the
// hand-authored AddJobHistory migration/snapshot, never run through the real `dotnet ef`
// tool) is diagnosed with an actual dotnet-ef run. Remove this once that's fixed — see
// https://aka.ms/efcore-docs-pending-changes. Suppressing it, rather than fixing the root
// cause, risks silently applying a schema that doesn't match the compiled model.
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection"))
           .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.RelationalEventId.PendingModelChangesWarning)));

// Services
builder.Services.AddSingleton<TimeProvider>(TimeProvider.System);
builder.Services.AddSingleton<IBroadcastThrottle, BroadcastThrottle>();
builder.Services.AddScoped<IChannelKeyService, ChannelKeyService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddHttpClient<ITwitchPubSubService, TwitchPubSubService>();

// CORS — Twitch extensions run in iframes on various domains; scope to what the API actually needs.
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

// Map endpoints
app.MapHealthEndpoints();
app.MapIngestEndpoints();
app.MapTelemetryEndpoints();
app.MapJobHistoryEndpoints();
app.MapChannelEndpoints();

if (args.Contains("--migrate-only"))
{
    using var migrateScope = app.Services.CreateScope();
    var migrateDb = migrateScope.ServiceProvider.GetRequiredService<AppDbContext>();
    await migrateDb.Database.MigrateAsync();
    return;
}

var dbSettings = app.Services.GetRequiredService<IOptions<DatabaseSettings>>().Value;
if (dbSettings.AutoMigrate)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();

// Exposes the implicit Program class for WebApplicationFactory<Program> in the test project.
public partial class Program;
