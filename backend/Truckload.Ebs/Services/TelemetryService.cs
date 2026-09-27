using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Truckload.Contracts;
using Truckload.Ebs.Data;
using Truckload.Ebs.Entities;

namespace Truckload.Ebs.Services;

public class TelemetryService : ITelemetryService
{
    /// <summary>
    /// Session-only retention cap: v1.1 keeps only the most recent completed jobs per
    /// channel rather than a full archive, pruning older rows whenever a new one is added.
    /// </summary>
    public const int MaxJobHistoryRowsPerChannel = 20;

    private readonly AppDbContext _db;
    private readonly TimeProvider _time;

    public TelemetryService(AppDbContext db, TimeProvider timeProvider)
    {
        _db = db;
        _time = timeProvider;
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
                UpdatedAt = _time.GetUtcNow().UtcDateTime
            };
            _db.TelemetrySnapshots.Add(snapshot);
            await _db.SaveChangesAsync();
            return;
        }

        // The bridge only ever sends whole-state snapshots — it has no concept of a "job
        // completed" event of its own — so a completed job has to be detected here,
        // server-side, by comparing what was previously stored against what's about to
        // replace it, before the previous snapshot is overwritten below.
        await RecordCompletedJobIfAnyAsync(channelId, previousPayloadJson: snapshot.PayloadJson, incomingPayloadJson: payloadJson);

        snapshot.PayloadJson = payloadJson;
        snapshot.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();
    }

    public async Task<string?> GetLatestAsync(string channelId)
    {
        var snapshot = await _db.TelemetrySnapshots.FindAsync(channelId);
        return snapshot?.PayloadJson;
    }

    public async Task<IReadOnlyList<JobHistoryEntry>> GetHistoryAsync(string channelId, int limit)
    {
        return await _db.JobHistoryEntries
            .Where(h => h.ChannelId == channelId)
            .OrderByDescending(h => h.CompletedAt)
            .Take(limit)
            .ToListAsync();
    }

    /// <summary>
    /// Compares the job on the previously stored snapshot against the job on the payload
    /// about to replace it. If the previous job was active and the new one isn't (absent or
    /// <c>Active: false</c>), that's a completed job: archive it to history and prune the
    /// channel's history back down to <see cref="MaxJobHistoryRowsPerChannel"/> rows.
    /// Kept simple and synchronous within this upsert path — no background worker or queue.
    /// </summary>
    private async Task RecordCompletedJobIfAnyAsync(string channelId, string previousPayloadJson, string incomingPayloadJson)
    {
        var previousJob = TryGetJob(previousPayloadJson);
        if (previousJob is not { Active: true })
            return; // no active job to have completed

        var incomingJob = TryGetJob(incomingPayloadJson);
        if (incomingJob is { Active: true })
            return; // still an active job (the same one, or a new one) — not a completion

        _db.JobHistoryEntries.Add(new JobHistoryEntry
        {
            Id = Guid.NewGuid(),
            ChannelId = channelId,
            Cargo = previousJob.Cargo,
            Source = previousJob.Source,
            Destination = previousJob.Destination,
            Distance = previousJob.Distance,
            CompletedAt = _time.GetUtcNow().UtcDateTime
        });
        await _db.SaveChangesAsync();

        await PruneHistoryAsync(channelId);
    }

    private async Task PruneHistoryAsync(string channelId)
    {
        var stale = await _db.JobHistoryEntries
            .Where(h => h.ChannelId == channelId)
            .OrderByDescending(h => h.CompletedAt)
            .Skip(MaxJobHistoryRowsPerChannel)
            .ToListAsync();

        if (stale.Count == 0)
            return;

        _db.JobHistoryEntries.RemoveRange(stale);
        await _db.SaveChangesAsync();
    }

    private static JobInfo? TryGetJob(string payloadJson)
    {
        try
        {
            return JsonSerializer.Deserialize(payloadJson, TelemetryJsonContext.Default.TelemetryPayload)?.Job;
        }
        catch (JsonException)
        {
            // Malformed or legacy stored payload — treat as "no job info available"
            // rather than letting a bad row block the rest of the upsert.
            return null;
        }
    }
}
