using Microsoft.EntityFrameworkCore;
using Truckload.Ebs.Configuration;
using Truckload.Ebs.Data;
using Truckload.Ebs.Endpoints;
using Truckload.Ebs.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Services.Configure<TwitchSettings>(builder.Configuration.GetSection("Twitch"));

// Database
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Services
builder.Services.AddScoped<IChannelKeyService, ChannelKeyService>();
builder.Services.AddScoped<ITelemetryService, TelemetryService>();
builder.Services.AddHttpClient<ITwitchPubSubService, TwitchPubSubService>();

// CORS — Twitch extensions run in iframes on various domains
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

app.UseCors();

// Map endpoints
app.MapHealthEndpoints();
app.MapIngestEndpoints();
app.MapTelemetryEndpoints();
app.MapChannelEndpoints();

// Auto-migrate in development
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.Run();
