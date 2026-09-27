using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Truckload.Ebs.Models;
using Xunit;

namespace Truckload.Ebs.Tests;

public class ChannelKeyEndpointTests : IClassFixture<TestWebAppFactory>
{
    private readonly TestWebAppFactory _factory;

    public ChannelKeyEndpointTests(TestWebAppFactory factory) => _factory = factory;

    private async Task<HttpClient> ClientAsync()
    {
        await _factory.EnsureDatabaseCreatedAsync();
        return _factory.CreateClient();
    }

    [Fact]
    public async Task Get_WithoutAuthorizationHeader_Returns401()
    {
        var client = await ClientAsync();
        var response = await client.GetAsync("/api/channels/keys");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithInvalidSignature_Returns401()
    {
        var client = await ClientAsync();
        var badToken = TestJwt.CreateBroadcasterToken("chan-1", secretBase64: Convert.ToBase64String(new byte[32]));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", badToken);

        var response = await client.GetAsync("/api/channels/keys");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithExpiredToken_Returns401()
    {
        var client = await ClientAsync();
        var expired = TestJwt.CreateExpiredToken("chan-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", expired);

        var response = await client.GetAsync("/api/channels/keys");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithViewerRole_Returns403()
    {
        var client = await ClientAsync();
        var viewerToken = TestJwt.CreateViewerToken("chan-1");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", viewerToken);

        var response = await client.GetAsync("/api/channels/keys");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithNoExistingKey_Returns404()
    {
        var client = await ClientAsync();
        var token = TestJwt.CreateBroadcasterToken("chan-never-generated");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/channels/keys");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Post_GeneratesKey_AndGetReturnsTheSameKey()
    {
        var client = await ClientAsync();
        var token = TestJwt.CreateBroadcasterToken("chan-2");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var postResponse = await client.PostAsync("/api/channels/keys", content: null);
        Assert.Equal(HttpStatusCode.OK, postResponse.StatusCode);
        var generated = await postResponse.Content.ReadFromJsonAsync<IngestKeyResponse>();
        Assert.NotNull(generated);
        Assert.Equal("chan-2", generated!.ChannelId);
        Assert.False(string.IsNullOrWhiteSpace(generated.IngestKey));

        var getResponse = await client.GetAsync("/api/channels/keys");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        var fetched = await getResponse.Content.ReadFromJsonAsync<IngestKeyResponse>();
        Assert.Equal(generated.IngestKey, fetched!.IngestKey);
    }

    [Fact]
    public async Task Post_Twice_RotatesTheKey()
    {
        var client = await ClientAsync();
        var token = TestJwt.CreateBroadcasterToken("chan-3");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var first = await (await client.PostAsync("/api/channels/keys", content: null))
            .Content.ReadFromJsonAsync<IngestKeyResponse>();
        var second = await (await client.PostAsync("/api/channels/keys", content: null))
            .Content.ReadFromJsonAsync<IngestKeyResponse>();

        Assert.NotEqual(first!.IngestKey, second!.IngestKey);
    }
}
