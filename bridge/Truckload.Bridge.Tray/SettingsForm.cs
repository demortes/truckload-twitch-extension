namespace Truckload.Bridge.Tray;

/// <summary>Collects the ingest key and URL; shown on first run and from the tray menu.</summary>
public sealed class SettingsForm : Form
{
    private readonly TextBox _key = new() { UseSystemPasswordChar = true, Dock = DockStyle.Fill };
    private readonly TextBox _url = new() { Dock = DockStyle.Fill };
    private readonly CheckBox _show = new() { Text = "Show key", AutoSize = true };
    private readonly CheckBox _startup = new() { Text = "Start with Windows", AutoSize = true };

    public string IngestKey => _key.Text.Trim();
    public string IngestUrl => string.IsNullOrWhiteSpace(_url.Text) ? BridgeOptions.DefaultIngestUrl : _url.Text.Trim();
    public bool StartWithWindows => _startup.Checked;

    public SettingsForm(BridgeOptions options, bool firstRun)
    {
        Text = firstRun ? "Welcome to Truckload Bridge" : "Truckload Bridge settings";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(470, firstRun ? 270 : 230);
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);

        _key.Text = options.IngestKey ?? "";
        _url.Text = options.IngestUrl ?? BridgeOptions.DefaultIngestUrl;
        _startup.Checked = StartupRegistration.IsEnabled() || firstRun;
        _show.CheckedChanged += (_, _) => _key.UseSystemPasswordChar = !_show.Checked;

        var intro = new Label
        {
            AutoSize = false,
            Dock = DockStyle.Fill,
            Text = firstRun
                ? "One step to go. Open the Truckload extension's Config page on Twitch, click \"Generate API Key\", and paste the key below. Then start ATS or ETS2; the tray light turns green when telemetry is flowing."
                : "Paste a new ingest key if you generated one. Leave the URL alone unless you were told otherwise.",
        };

        var save = new Button { Text = "Save", DialogResult = DialogResult.OK, AutoSize = true };
        var cancel = new Button { Text = firstRun ? "Quit" : "Cancel", DialogResult = DialogResult.Cancel, AutoSize = true };
        AcceptButton = save;
        CancelButton = cancel;

        var buttons = new FlowLayoutPanel { FlowDirection = FlowDirection.RightToLeft, Dock = DockStyle.Fill, AutoSize = true };
        buttons.Controls.AddRange([cancel, save]);

        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, Padding = new Padding(14), ColumnCount = 2 };
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
        layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        layout.Controls.Add(intro, 0, 0);
        layout.SetColumnSpan(intro, 2);
        layout.Controls.Add(new Label { Text = "Ingest key", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 0) }, 0, 1);
        layout.Controls.Add(_key, 1, 1);
        layout.Controls.Add(new Label { Text = "Backend URL", AutoSize = true, Anchor = AnchorStyles.Left, Margin = new Padding(0, 8, 8, 0) }, 0, 2);
        layout.Controls.Add(_url, 1, 2);
        layout.Controls.Add(_show, 1, 3);
        layout.Controls.Add(_startup, 1, 4);
        layout.Controls.Add(buttons, 0, 5);
        layout.SetColumnSpan(buttons, 2);
        Controls.Add(layout);

        FormClosing += (_, e) =>
        {
            if (DialogResult == DialogResult.OK && IngestKey.Length == 0)
            {
                MessageBox.Show(this, "Paste your ingest key first.", Text, MessageBoxButtons.OK, MessageBoxIcon.Information);
                e.Cancel = true;
            }
        };
    }
}
