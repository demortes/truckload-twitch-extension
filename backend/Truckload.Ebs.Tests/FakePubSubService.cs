using Truckload.Ebs.Models;
using Truckload.Ebs.Services;

namespace Truckload.Ebs.Tests;

/// <summary>A scriptable stand-in for the real Twitch PubSub HTTP call.</summary>
public class FakePubSubService : ITwitchPubSubService
{
    private readonly Queue<BroadcastResult> _scriptedResults = new();

    public List<(string ChannelId, string MessageJson)> Calls { get; } = new();

    /// <summary>Queues the result the next N calls should return; defaults to Sent when the queue is empty.</summary>
    public void Enqueue(BroadcastResult result) => _scriptedResults.Enqueue(result);

    public Task<BroadcastResult> BroadcastAsync(string channelId, string messageJson, CancellationToken cancellationToken = default)
    {
        Calls.Add((channelId, messageJson));
        var result = _scriptedResults.Count > 0
            ? _scriptedResults.Dequeue()
            : new BroadcastResult(BroadcastOutcome.Sent, 204);
        return Task.FromResult(result);
    }
}
