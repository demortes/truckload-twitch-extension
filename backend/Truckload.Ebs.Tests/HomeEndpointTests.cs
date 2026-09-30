using System.Net;
using Xunit;

namespace Truckload.Ebs.Tests;

public class HomeEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public HomeEndpointTests(TestWebAppFactory factory) => _factory = factory;

    [Fact]
    public async Task Root_ServesSetupPage_WithExtensionLinkAndIngestUrl()
    {
        var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-Forwarded-Proto", "https");

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("dashboard.twitch.tv/extensions/4xg4ruobtcst87wrh63xur9wkcq8ah-0.0.1", html);
        Assert.Contains("https://localhost/api/ingest", html);
        Assert.Contains("Generate API Key", html);
    }
}
