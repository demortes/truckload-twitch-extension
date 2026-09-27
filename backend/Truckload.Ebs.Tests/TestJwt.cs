using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Truckload.Ebs.Tests;

/// <summary>Mints broadcaster JWTs the same way Twitch would, signed with the shared test secret.</summary>
public static class TestJwt
{
    public static string CreateBroadcasterToken(string channelId, string secretBase64 = TestWebAppFactory.TestSecret) =>
        Create(channelId, "broadcaster", secretBase64);

    public static string CreateViewerToken(string channelId, string secretBase64 = TestWebAppFactory.TestSecret) =>
        Create(channelId, "viewer", secretBase64);

    public static string CreateExpiredToken(string channelId, string secretBase64 = TestWebAppFactory.TestSecret)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(secretBase64));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("channel_id", channelId),
            new Claim("role", "broadcaster"),
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(-5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private static string Create(string channelId, string role, string secretBase64)
    {
        var key = new SymmetricSecurityKey(Convert.FromBase64String(secretBase64));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var claims = new[]
        {
            new Claim("channel_id", channelId),
            new Claim("role", role),
        };
        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: credentials);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
