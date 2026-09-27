using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Time.Testing;
using Truckload.Ebs.Data;
using Truckload.Ebs.Services;

namespace Truckload.Ebs.Tests;

/// <summary>
/// Boots the real app (Program.cs) against an in-memory SQLite database instead of Postgres,
/// with the outbound Twitch PubSub call replaced by a scriptable fake and a controllable clock.
/// </summary>
public class TestWebAppFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public FakePubSubService PubSub { get; } = new();
    public FakeTimeProvider Time { get; } = new(DateTimeOffset.UtcNow);

    // A valid-looking base64 secret; tests mint JWTs signed with this same value.
    public const string TestSecret = "dGVzdC1zZWNyZXQtdGVzdC1zZWNyZXQtdGVzdC1zZWNyZXQh";
    public const string TestClientId = "test-client-id";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        _connection.Open();

        builder.UseEnvironment("Testing");

        builder.ConfigureAppConfiguration((_, config) =>
        {
            config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Twitch:ClientId"] = TestClientId,
                ["Twitch:ExtensionSecret"] = TestSecret,
                ["Twitch:MinBroadcastIntervalMs"] = "1000",
                ["Database:AutoMigrate"] = "false",
            });
        });

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.AddDbContext<AppDbContext>(options => options.UseSqlite(_connection));

            services.RemoveAll<ITwitchPubSubService>();
            services.AddSingleton<ITwitchPubSubService>(PubSub);

            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Time);
        });
    }

    /// <summary>Creates the SQLite schema directly from the model (no migrations needed for tests).</summary>
    public async Task EnsureDatabaseCreatedAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Database.EnsureCreatedAsync();
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
            _connection.Dispose();
    }
}
