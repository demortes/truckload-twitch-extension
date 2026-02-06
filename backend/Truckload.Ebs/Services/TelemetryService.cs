using Truckload.Ebs.Data;
using Truckload.Ebs.Entities;

namespace Truckload.Ebs.Services;

public class TelemetryService : ITelemetryService
{
    private readonly AppDbContext _db;

    public TelemetryService(AppDbContext db)
    {
        _db = db;
    }

    public async Task UpsertAsync(string channelId, string payloadJson)
    {
        var snapshot = await _db.TelemetrySnapshots.FindAsync(channelId);
        if (snapshot is null)
        {
            snapshot = new TelemetrySnapshot
            {
                ChannelId = channelId,
                PayloadJson = payloadJson,
                UpdatedAt = DateTime.UtcNow
            };
            _db.TelemetrySnapshots.Add(snapshot);
        }
        else
        {
            snapshot.PayloadJson = payloadJson;
            snapshot.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetLatestAsync(string channelId)
    {
        var snapshot = await _db.TelemetrySnapshots.FindAsync(channelId);
        return snapshot?.PayloadJson;
    }
}
