using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Truckload.Ebs.Configuration;
using Truckload.Ebs.Models;

namespace Truckload.Ebs.Services;

public class TwitchPubSubService : ITwitchPubSubService
{
    private readonly HttpClient _httpClient;
    private readonly TwitchSettings _settings;
    private readonly ILogger<TwitchPubSubService> _logger;
    private byte[]? _secretBytes;

    public TwitchPubSubService(HttpClient httpClient, IOptions<TwitchSettings> settings, ILogger<TwitchPubSubService> logger)
    {
        _httpClient = httpClient;
        _settings = settings.Value;
        _logger = logger;
        // Options are validated at startup (see TwitchSettingsValidator), so decoding here should never throw
        // in practice; it is still deferred rather than done in the constructor to fail a single request,
        // not the whole app, if that ever changes.
    }

    private byte[] SecretBytes => _secretBytes ??= Convert.FromBase64String(_settings.ExtensionSecret);

    private string SignJwt(string channelId)
    {
        var key = new SymmetricSecurityKey(SecretBytes);
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

    public async Task<BroadcastResult> BroadcastAsync(string channelId, string messageJson, CancellationToken cancellationToken = default)
    {
        if (!_settings.BroadcastEnabled)
            return new BroadcastResult(BroadcastOutcome.Disabled);

        try
        {
            var jwt = SignJwt(channelId);

            var body = new
            {
                target = new[] { "broadcast" },
                broadcaster_id = channelId,
                is_global_broadcast = false,
                message = messageJson
            };

            var request = new HttpRequestMessage(HttpMethod.Post, _settings.PubSubUrl)
            {
                Content = JsonContent.Create(body)
            };
            request.Headers.Add("Client-Id", _settings.ClientId);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);

            var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode)
                return new BroadcastResult(BroadcastOutcome.Sent, (int)response.StatusCode);

            if ((int)response.StatusCode == 429)
            {
                var retryAfter = response.Headers.RetryAfter?.Delta ?? TimeSpan.FromSeconds(5);
                _logger.LogWarning("Twitch PubSub rate limited channel {ChannelId}; backing off {RetryAfter}", channelId, retryAfter);
                return new BroadcastResult(BroadcastOutcome.RateLimited, 429, retryAfter);
            }

            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogWarning(
                "Twitch PubSub broadcast for channel {ChannelId} failed with {StatusCode}: {Body}",
                channelId, (int)response.StatusCode, Truncate(responseBody, 500));
            return new BroadcastResult(BroadcastOutcome.Failed, (int)response.StatusCode);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            _logger.LogWarning(ex, "Twitch PubSub broadcast for channel {ChannelId} failed to send", channelId);
            return new BroadcastResult(BroadcastOutcome.Failed);
        }
    }

    private static string Truncate(string value, int maxLength) =>
        value.Length <= maxLength ? value : value[..maxLength] + "...";
}
