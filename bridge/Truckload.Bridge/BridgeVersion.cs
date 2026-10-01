using System.Reflection;

namespace Truckload.Bridge;

/// <summary>The Bridge's own version (set from the release tag at publish time via -p:Version).</summary>
public static class BridgeVersion
{
    public static string Current { get; } = Resolve();

    private static string Resolve()
    {
        var informational = typeof(BridgeVersion).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;

        // "1.6.0+<commit sha>" -> "1.6.0"
        var plus = informational?.IndexOf('+') ?? -1;
        return string.IsNullOrWhiteSpace(informational) ? "dev" : plus > 0 ? informational[..plus] : informational;
    }
}
