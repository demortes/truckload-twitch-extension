using System.Diagnostics;
using Truckload.Bridge.Sending;
using Truckload.Bridge.Updates;

namespace Truckload.Bridge.Tray;

/// <summary>
/// The tray application: runs <see cref="BridgeRunner"/> in the background and shows its state as a
/// coloured dot in the notification area, with a menu for settings, pause, the log, and updates.
/// </summary>
public sealed class TrayApp : ApplicationContext
{
    private const string TrayAssetPrefix = "Truckload.Bridge.Tray";

    private readonly BridgeOptions _options;
    private readonly string _baseDirectory;
    private readonly SynchronizationContext _ui;
    private readonly NotifyIcon _tray;
    private readonly StatusIcons _icons = new();
    private readonly HttpClient _http = new();

    private readonly ToolStripMenuItem _statusItem = new() { Enabled = false };
    private readonly ToolStripMenuItem _pauseItem = new("Pause");
    private readonly ToolStripMenuItem _updateItem = new("Check for updates");

    private readonly object _logLock = new();
    private readonly List<string> _log = new();

    private CancellationTokenSource? _runnerCts;
    private HttpClient? _telemetryHttp;
    private BridgeStatus _status = BridgeStatus.Starting;
    private bool _paused;
    private UpdateInfo? _update;
    private Action? _balloonAction;

    public TrayApp(BridgeOptions options, string baseDirectory)
    {
        _options = options;
        _baseDirectory = baseDirectory;
        _ui = SynchronizationContext.Current ?? new SynchronizationContext();

        _options.DryRun = false;

        _pauseItem.Click += (_, _) => TogglePause();
        _updateItem.Click += async (_, _) => await OnUpdateItemClickedAsync();

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(_pauseItem);
        menu.Items.Add("Settings...", null, (_, _) => ShowSettings(firstRun: false));
        menu.Items.Add("Show log...", null, (_, _) => ShowLog());
        menu.Items.Add(_updateItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => ExitThread());

        _tray = new NotifyIcon { ContextMenuStrip = menu, Visible = true };
        _tray.DoubleClick += (_, _) => ShowSettings(firstRun: false);
        _tray.BalloonTipClicked += (_, _) => _balloonAction?.Invoke();
        ApplyStatus();

        // Run the first-run flow once the message loop is up, so a cancelled dialog can exit cleanly.
        _ui.Post(_ => Start(), null);
    }

    private void Start()
    {
        if (string.IsNullOrWhiteSpace(_options.IngestKey) && !ShowSettings(firstRun: true))
        {
            ExitThread();
            return;
        }

        StartRunner();

        if (!_options.NoUpdateCheck)
            _ = CheckForUpdateAsync(userInitiated: false);
    }

    // ---- runner ---------------------------------------------------------------------------

    private void StartRunner()
    {
        StopRunner();
        if (_paused || string.IsNullOrWhiteSpace(_options.IngestKey))
            return;

        _status = BridgeStatus.Starting;
        ApplyStatus();

        try
        {
            _telemetryHttp = TelemetrySourceFactory.CreateTelemetryHttpClient();
            var source = TelemetrySourceFactory.Create(_options, _telemetryHttp, TimeProvider.System);
            var sender = new IngestSender(_http, _options.IngestUrl ?? BridgeOptions.DefaultIngestUrl, _options.IngestKey!);
            var runner = new BridgeRunner(source, sender, _options, TimeProvider.System, Log, status => _ui.Post(_ => SetStatus(status), null));

            var cts = _runnerCts = new CancellationTokenSource();
            _ = Task.Run(async () =>
            {
                var state = await runner.RunAsync(cts.Token);
                if (state.ExitRequested == BridgeExitReason.InvalidKey)
                    _ui.Post(_ => OnInvalidKey(), null);
            });
        }
        catch (ArgumentException ex)
        {
            Log($"[error] {ex.Message}");
            SetStatus(BridgeStatus.SendFailing);
        }
    }

    private void StopRunner()
    {
        _runnerCts?.Cancel();
        _runnerCts = null;
        _telemetryHttp?.Dispose();
        _telemetryHttp = null;
    }

    private void OnInvalidKey()
    {
        ShowBalloon("Ingest key rejected", "The Truckload backend did not accept your key. Click to enter a new one.", () => ShowSettings(firstRun: false));
    }

    private void TogglePause()
    {
        _paused = !_paused;
        _pauseItem.Text = _paused ? "Resume" : "Pause";
        if (_paused)
        {
            StopRunner();
            ApplyStatus();
        }
        else
        {
            StartRunner();
        }
    }

    // ---- status light ---------------------------------------------------------------------

    private void SetStatus(BridgeStatus status)
    {
        if (_runnerCts is null && !_paused)
            return; // a late report from a runner that has already been stopped
        _status = status;
        ApplyStatus();
    }

    private void ApplyStatus()
    {
        var text = StatusIcons.Describe(_status, _paused);
        _tray.Icon = _icons.Get(StatusIcons.ColorFor(_status, _paused));
        _tray.Text = Truncate($"Truckload Bridge: {text}", 63); // NotifyIcon tooltips are capped at 63 characters
        _statusItem.Text = text;
    }

    private static string Truncate(string value, int max) => value.Length <= max ? value : value[..max];

    // ---- dialogs --------------------------------------------------------------------------

    /// <returns>True when the user saved settings.</returns>
    private bool ShowSettings(bool firstRun)
    {
        using var form = new SettingsForm(_options, firstRun);
        if (form.ShowDialog() != DialogResult.OK)
            return false;

        _options.IngestKey = form.IngestKey;
        _options.IngestUrl = form.IngestUrl;
        try
        {
            _options.SaveTo(_baseDirectory);
            StartupRegistration.Set(form.StartWithWindows);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            MessageBox.Show($"Could not save your settings: {ex.Message}", "Truckload Bridge", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        if (!firstRun)
            StartRunner();
        return true;
    }

    private void ShowLog()
    {
        string[] lines;
        lock (_logLock)
            lines = _log.ToArray();
        using var form = new LogForm(lines);
        form.ShowDialog();
    }

    private void ShowBalloon(string title, string text, Action? onClick = null)
    {
        _balloonAction = onClick;
        _tray.ShowBalloonTip(8000, title, text, ToolTipIcon.Info);
    }

    private void Log(string line)
    {
        lock (_logLock)
        {
            _log.Add($"{DateTime.Now:HH:mm:ss} {line}");
            if (_log.Count > 500)
                _log.RemoveRange(0, _log.Count - 500);
        }
    }

    // ---- updates --------------------------------------------------------------------------

    private async Task OnUpdateItemClickedAsync()
    {
        if (_update is not null)
            await InstallUpdateAsync();
        else
            await CheckForUpdateAsync(userInitiated: true);
    }

    private async Task CheckForUpdateAsync(bool userInitiated)
    {
        var update = await new UpdateChecker(_http).CheckAsync(
            BridgeVersion.Current, UpdateChecker.CurrentRuntimeId(), CancellationToken.None, TrayAssetPrefix);

        if (update is null)
        {
            if (userInitiated)
                ShowBalloon("Truckload Bridge", "You are on the latest version.");
            return;
        }

        _update = update;
        _updateItem.Text = $"Install update v{update.Version}";
        ShowBalloon("Update available", $"Truckload Bridge v{update.Version} is ready. Click to install.", () => _ = InstallUpdateAsync());
    }

    private async Task InstallUpdateAsync()
    {
        if (_update is null)
            return;

        var result = await new SelfUpdater(_http, Log).ApplyAsync(_update, Environment.ProcessPath, CancellationToken.None);
        switch (result)
        {
            case SelfUpdateResult.Updated:
                RestartIntoNewVersion();
                break;
            case SelfUpdateResult.Unsupported:
                OpenReleasePage(_update.ReleaseUrl);
                break;
            default:
                ShowBalloon("Update failed", "Click to download it manually.", () => OpenReleasePage(_update.ReleaseUrl));
                break;
        }
    }

    private void RestartIntoNewVersion()
    {
        if (Environment.ProcessPath is { } exe)
            Process.Start(new ProcessStartInfo(exe, "--restarted") { UseShellExecute = false });
        ExitThread();
    }

    private static void OpenReleasePage(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && uri.Scheme == Uri.UriSchemeHttps)
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
    }

    // ---- shutdown -------------------------------------------------------------------------

    protected override void ExitThreadCore()
    {
        StopRunner();
        _tray.Visible = false;
        _tray.Dispose();
        _icons.Dispose();
        _http.Dispose();
        base.ExitThreadCore();
    }
}
