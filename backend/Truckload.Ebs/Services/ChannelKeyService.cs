using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Truckload.Ebs.Data;
using Truckload.Ebs.Entities;
using Truckload.Ebs.Models;

namespace Truckload.Ebs.Services;

public class ChannelKeyService : IChannelKeyService
{
    private readonly AppDbContext _db;

    public ChannelKeyService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<IngestKeyResponse> GenerateKeyAsync(string channelId)
    {
        var key = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));

        var channel = await _db.Channels.FindAsync(channelId);
        if (channel is null)
        {
            channel = new Channel
            {
                ChannelId = channelId,
                IngestKey = key,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            _db.Channels.Add(channel);
        }
        else
        {
            channel.IngestKey = key;
            channel.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();

        return new IngestKeyResponse
        {
            ChannelId = channelId,
            IngestKey = key,
            IngestUrl = "/api/ingest"
        };
    }

    public async Task<IngestKeyResponse?> GetKeyAsync(string channelId)
    {
        var channel = await _db.Channels.FindAsync(channelId);
        if (channel is null) return null;

        return new IngestKeyResponse
        {
            ChannelId = channelId,
            IngestKey = channel.IngestKey,
            IngestUrl = "/api/ingest"
        };
    }

    public async Task<string?> ValidateKeyAsync(string apiKey)
    {
        var channelId = await _db.Channels
            .Where(c => c.IngestKey == apiKey)
            .Select(c => c.ChannelId)
            .FirstOrDefaultAsync();
        return channelId;
    }
}
