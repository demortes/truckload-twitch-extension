namespace Truckload.Bridge.Tray;

/// <summary>A simple window onto the bridge's log lines, for answering "why is the light amber".</summary>
public sealed class LogForm : Form
{
    private readonly TextBox _text = new()
    {
        Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Both, WordWrap = false,
        Dock = DockStyle.Fill, Font = new Font("Consolas", 9f),
    };

    public LogForm(IEnumerable<string> lines)
    {
        Text = "Truckload Bridge log";
        ClientSize = new Size(720, 380);
        StartPosition = FormStartPosition.CenterScreen;
        Icon = Icon.ExtractAssociatedIcon(Environment.ProcessPath ?? Application.ExecutablePath);
        Controls.Add(_text);
        _text.Text = string.Join(Environment.NewLine, lines);
        Shown += (_, _) => { _text.SelectionStart = _text.TextLength; _text.ScrollToCaret(); };
    }
}
