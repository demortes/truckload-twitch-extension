using Truckload.Bridge;
using Truckload.Bridge.Tray;
using Truckload.Bridge.Updates;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        // TrayApp posts runner callbacks back to the UI thread through this context, which Application.Run
        // would otherwise only install after the app object has been constructed.
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());

        // One tray app per user session. A freshly updated copy launches itself with --restarted and
        // waits briefly for the old copy to exit.
        using var mutex = new Mutex(initiallyOwned: true, @"Local\TruckloadBridgeTray", out var createdNew);
        if (!createdNew)
        {
            if (!args.Contains("--restarted") || !WaitForPreviousInstance(mutex))
            {
                MessageBox.Show("Truckload Bridge is already running. Look for its dot in the notification area.",
                    "Truckload Bridge", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
        }

        var baseDirectory = AppContext.BaseDirectory;
        if (Environment.ProcessPath is { } exe)
            SelfUpdater.CleanUpOldExe(exe);

        BridgeOptions options;
        try
        {
            options = BridgeOptions.Resolve(args, baseDirectory);
        }
        catch (ArgumentException ex)
        {
            MessageBox.Show(ex.Message, "Truckload Bridge", MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Application.Run(new TrayApp(options, baseDirectory));
    }

    private static bool WaitForPreviousInstance(Mutex mutex)
    {
        try
        {
            return mutex.WaitOne(TimeSpan.FromSeconds(10));
        }
        catch (AbandonedMutexException)
        {
            return true; // the previous instance crashed; we own the mutex now
        }
    }
}
