using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Truckload.Ebs.Configuration;

namespace Truckload.Ebs.Services;

public class TwitchPubSubService : ITwitchPubSubService
{
    private readonly HttpClient _httpClient;
    private readonly TwitchSettings _settings;
    private readonly byte[] _secretBytes;

    public TwitchPubSubService(HttpClient httpClient, IOptions<TwitchSettings> settings)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _secretBytes = Convert.FromBase64String(_settings.ExtensionSecret);
    }

    private string SignJwt(string channelId)
    {
        var key = new SymmetricSecurityKey(_secretBytes);
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var pubsubPerms = JsonSerializer.Serialize(new { send = new[] { "broadcast" } });

        var claims = new[]
        {
            new Claim("channel_id", channelId),
            new Claim("role", "external"),
            new Claim("pubsub_perms", pubsubPerms, JsonClaimValueTypes.Json),
        };

        var token = new JwtSecurityToken(
            expires: DateTime.UtcNow.AddMinutes(1),
            claims: claims,
            signingCredentials: credentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public async Task<bool> BroadcastAsync(string channelId, string messageJson)
    {
        var jwt = SignJwt(channelId);

        var body = new
        {
            target = new[] { "broadcast" },
            broadcaster_id = channelId,
            is_global_broadcast = false,
            message = messageJson
        };

        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.twitch.tv/helix/extensions/pubsub")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Add("Client-Id", _settings.ClientId);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

        var response = await _httpClient.SendAsync(request);
        return response.IsSuccessStatusCode;
    }
}
