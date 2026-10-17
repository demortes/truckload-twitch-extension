using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;

namespace Truckload.Bridge.Tray;

/// <summary>The tray "status light": a coloured dot, drawn at runtime so there are no extra assets to ship.</summary>
public sealed class StatusIcons : IDisposable
{
    private readonly Dictionary<Color, Icon> _cache = new();

    public static Color ColorFor(BridgeStatus status, bool paused) =>
        paused ? Color.Gray
        : status switch
        {
            BridgeStatus.Live => Color.FromArgb(46, 184, 92),
            BridgeStatus.SendFailing or BridgeStatus.InvalidKey => Color.FromArgb(222, 64, 64),
            _ => Color.FromArgb(240, 170, 30), // starting / waiting for the game
        };

    public static string Describe(BridgeStatus status, bool paused) =>
        paused ? "Paused"
        : status switch
        {
            BridgeStatus.Starting => "Starting...",
            BridgeStatus.WaitingForGame => "Waiting for the game",
            BridgeStatus.Live => "Live: sending telemetry",
            BridgeStatus.SendFailing => "Cannot reach the Truckload backend (retrying)",
            BridgeStatus.InvalidKey => "Ingest key rejected: open Settings",
            _ => status.ToString(),
        };

    public Icon Get(Color color)
    {
        if (_cache.TryGetValue(color, out var cached))
            return cached;

        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using var fill = new SolidBrush(color);
            using var ring = new Pen(Color.FromArgb(220, 30, 30, 30), 2f);
            g.FillEllipse(fill, 3, 3, 26, 26);
            g.DrawEllipse(ring, 3, 3, 26, 26);
        }

        // GetHicon makes an unmanaged handle; clone into a managed Icon and release the handle.
        var handle = bitmap.GetHicon();
        try
        {
            var icon = (Icon)Icon.FromHandle(handle).Clone();
            _cache[color] = icon;
            return icon;
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    public void Dispose()
    {
        foreach (var icon in _cache.Values)
            icon.Dispose();
        _cache.Clear();
    }

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr handle);
}
