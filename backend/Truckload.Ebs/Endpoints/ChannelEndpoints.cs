using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Truckload.Ebs.Configuration;
using Truckload.Ebs.Models;
using Truckload.Ebs.Services;

namespace Truckload.Ebs.Endpoints;

public static class ChannelEndpoints
{
    public static void MapChannelEndpoints(this WebApplication app)
    {
        app.MapGet("/api/channels/keys", async (
            HttpContext context,
            IOptions<TwitchSettings> settings,
            IChannelKeyService keyService) =>
        {
            var (channelId, error) = VerifyBroadcasterJwt(context, settings.Value);
            if (error is not null) return error;

            var result = await keyService.GetKeyAsync(channelId!);
            if (result is null)
                return Results.Json(
                    new ErrorResponse { Error = "No key found. Generate one first." },
                    statusCode: 404);

            return Results.Ok(result);
        });

        app.MapPost("/api/channels/keys", async (
            HttpContext context,
            IOptions<TwitchSettings> settings,
            IChannelKeyService keyService) =>
        {
            var (channelId, error) = VerifyBroadcasterJwt(context, settings.Value);
            if (error is not null) return error;

            var result = await keyService.GenerateKeyAsync(channelId!);
            return Results.Ok(result);
        });
    }

    private static (string? ChannelId, IResult? Error) VerifyBroadcasterJwt(
        HttpContext context, TwitchSettings settings)
    {
        var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
            return (null, Results.Json(new ErrorResponse { Error = "Missing Authorization header" }, statusCode: 401));

        var token = authHeader["Bearer ".Length..];

        try
        {
            var secretBytes = Convert.FromBase64String(settings.ExtensionSecret);
            var handler = new JwtSecurityTokenHandler
            {
                // Without this, JwtSecurityTokenHandler remaps short inbound claim names
                // (e.g. "role") to long .NET ClaimTypes URIs by default, so
                // FindFirstValue("role") below would always return null and every
                // broadcaster request would be rejected as 403 regardless of the token's
                // actual role. Twitch's own JWTs, and the ones this backend signs itself,
                // use the short names, so keep them as issued.
                MapInboundClaims = false,
            };
            var parameters = new TokenValidationParameters
            {
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                IssuerSigningKey = new SymmetricSecurityKey(secretBytes),
                ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 }
            };

            var principal = handler.ValidateToken(token, parameters, out _);

            var channelId = principal.FindFirstValue("channel_id");
            var role = principal.FindFirstValue("role");

            if (string.IsNullOrEmpty(channelId))
                return (null, Results.Json(new ErrorResponse { Error = "Invalid token: missing channel_id" }, statusCode: 401));

            if (role != "broadcaster")
                return (null, Results.Json(new ErrorResponse { Error = "Only broadcasters can manage keys" }, statusCode: 403));

            return (channelId, null);
        }
        catch (SecurityTokenException)
        {
            return (null, Results.Json(new ErrorResponse { Error = "Invalid or expired token" }, statusCode: 401));
        }
    }
}
