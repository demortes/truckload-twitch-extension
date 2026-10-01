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

    [Theory]
    [InlineData("/logo.png")]
    [InlineData("/favicon.ico")]
    public async Task Logo_IsServedAsAPng(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(new byte[] { 0x89, 0x50, 0x4E, 0x47 }, bytes[..4]); // PNG signature
        Assert.Contains("max-age", response.Headers.CacheControl?.ToString());
    }

    [Fact]
    public async Task Root_ShowsTheLogo_AndTheCspAllowsOnlySelfImages()
    {
        var response = await _factory.CreateClient().GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Contains("src=\"/logo.png\"", html);
        Assert.Contains("img-src 'self'", string.Join(";", response.Headers.GetValues("Content-Security-Policy")));
    }
}
