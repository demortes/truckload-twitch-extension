using System.Net;
using System.Net.Http.Json;
using Truckload.Ebs.Models;
using Xunit;

namespace Truckload.Ebs.Tests;

public class HealthEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public HealthEndpointTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Health_ReturnsOk_WhenDatabaseIsReachable()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(body);
        Assert.Equal("ok", body!.Status);
        Assert.Equal("ok", body.Db);
    }

    [Fact]
    public async Task Ready_ReturnsOk_WhenDatabaseIsReachable()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        var response = await _factory.CreateClient().GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body!.Db);
    }

    [Fact]
    public async Task Live_ReturnsOk_WithoutCheckingDatabase()
    {
        var response = await _factory.CreateClient().GetAsync("/api/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.Equal("ok", body!.Status);
        Assert.Equal("unchecked", body.Db);
    }
}
