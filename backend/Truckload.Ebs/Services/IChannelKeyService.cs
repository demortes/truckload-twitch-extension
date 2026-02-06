using Truckload.Ebs.Models;

namespace Truckload.Ebs.Services;

public interface IChannelKeyService
{
    Task<IngestKeyResponse> GenerateKeyAsync(string channelId);
    Task<IngestKeyResponse?> GetKeyAsync(string channelId);
    Task<string?> ValidateKeyAsync(string apiKey);
}
