using System.Net;

namespace Truckload.Bridge.Tests;

public sealed class FakeHttpMessageHandler : HttpMessageHandler
{
    private readonly Queue<Func<HttpResponseMessage>> _responses = new();

    public List<HttpRequestMessage> Requests { get; } = new();

    public void Enqueue(Func<HttpResponseMessage> factory) => _responses.Enqueue(factory);

    public void EnqueueJson(HttpStatusCode status, string json)
    {
        _responses.Enqueue(() => new HttpResponseMessage(status)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json"),
        });
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Requests.Add(request);
        var factory = _responses.Count > 0 ? _responses.Dequeue() : () => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"stored":true,"broadcast":"sent"}"""),
        };
        return Task.FromResult(factory());
    }
}
