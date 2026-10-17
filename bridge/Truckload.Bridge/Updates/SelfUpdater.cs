using System.IO.Compression;

namespace Truckload.Bridge.Updates;

public enum SelfUpdateResult { Updated, Unsupported, DownloadFailed, ReplaceFailed }

/// <summary>
/// Replaces the running single-file exe with the one from a release zip. Windows will not let a
/// running exe be overwritten or deleted, but it does let it be renamed, so the old exe is moved
/// aside to <c>*.old</c> (removed on the next start) and the new one takes its name. The running
/// process keeps going on the old file; the streamer restarts the bridge to pick up the update.
/// </summary>
public sealed class SelfUpdater
{
    public const string OldSuffix = ".old";
    private const string StagedSuffix = ".new";
    private const long MaxZipBytes = 200L * 1024 * 1024;

    // Release assets only ever come from GitHub; refuse to download from anywhere else.
    private static readonly string[] AllowedHosts = ["github.com", "objects.githubusercontent.com", "release-assets.githubusercontent.com"];

    private readonly HttpClient _http;
    private readonly Action<string> _log;

    public SelfUpdater(HttpClient http, Action<string> log)
    {
        _http = http;
        _log = log;
    }

    public static bool IsAllowedDownloadUrl(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Scheme == Uri.UriSchemeHttps
        && AllowedHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase);

    /// <summary>Deletes the exe a previous update moved aside. Best effort: it may still be locked.</summary>
    public static void CleanUpOldExe(string exePath)
    {
        try
        {
            var old = exePath + OldSuffix;
            if (File.Exists(old))
                File.Delete(old);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    /// <param name="exePath">The running exe (<see cref="Environment.ProcessPath"/>); null when unknown.</param>
    public async Task<SelfUpdateResult> ApplyAsync(UpdateInfo update, string? exePath, CancellationToken cancellationToken)
    {
        // `dotnet run` / `dotnet Truckload.Bridge.dll` have no single-file exe of their own to replace.
        if (exePath is null || update.AssetUrl is null
            || string.Equals(Path.GetFileNameWithoutExtension(exePath), "dotnet", StringComparison.OrdinalIgnoreCase))
        {
            return SelfUpdateResult.Unsupported;
        }

        if (!IsAllowedDownloadUrl(update.AssetUrl))
        {
            _log($"[error] Refusing to download an update from {update.AssetUrl}.");
            return SelfUpdateResult.DownloadFailed;
        }

        var tempZip = Path.Combine(Path.GetTempPath(), $"truckload-bridge-update-{Guid.NewGuid():N}.zip");
        var staged = exePath + StagedSuffix;
        var old = exePath + OldSuffix;

        try
        {
            _log($"Downloading {update.AssetName}...");
            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, update.AssetUrl);
                request.Headers.UserAgent.ParseAdd($"Truckload-Bridge/{BridgeVersion.Current}");
                using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength > MaxZipBytes)
                    throw new IOException("update is unexpectedly large");

                await using var zipFile = File.Create(tempZip);
                await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
                await CopyLimitedAsync(body, zipFile, MaxZipBytes, cancellationToken);
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                _log($"[error] Could not download the update: {ex.Message}");
                return SelfUpdateResult.DownloadFailed;
            }

            try
            {
                // The zip holds the exe next to a README; take only the exe, by the exact name of the running one.
                using var archive = ZipFile.OpenRead(tempZip);
                var entry = archive.GetEntry(Path.GetFileName(exePath))
                    ?? throw new InvalidDataException($"{Path.GetFileName(exePath)} not found in the update zip");
                entry.ExtractToFile(staged, overwrite: true);
                if (!OperatingSystem.IsWindows())
                    File.SetUnixFileMode(staged, File.GetUnixFileMode(exePath));
            }
            catch (Exception ex) when (ex is InvalidDataException or IOException or UnauthorizedAccessException)
            {
                _log($"[error] The downloaded update is not usable: {ex.Message}");
                return SelfUpdateResult.DownloadFailed;
            }

            try
            {
                if (File.Exists(old))
                    File.Delete(old);
                File.Move(exePath, old);
                try
                {
                    File.Move(staged, exePath);
                }
                catch
                {
                    File.Move(old, exePath); // put the working exe back
                    throw;
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log($"[error] Could not replace {exePath}: {ex.Message}");
                return SelfUpdateResult.ReplaceFailed;
            }

            return SelfUpdateResult.Updated;
        }
        finally
        {
            TryDelete(tempZip);
            TryDelete(staged);
        }
    }

    private static async Task CopyLimitedAsync(Stream source, Stream destination, long limit, CancellationToken cancellationToken)
    {
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, cancellationToken)) > 0)
        {
            total += read;
            if (total > limit)
                throw new IOException("update is unexpectedly large");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
                File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }
}
