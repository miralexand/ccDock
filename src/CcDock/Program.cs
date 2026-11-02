using CcDock.App;
using CcDock.UI;

namespace CcDock;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        using var mutex = new Mutex(true, "ccDock.SingleInstance", out var isFirstInstance);
        if (!isFirstInstance)
        {
            MessageBox.Show(
                "ccDock 已经在运行，请在系统托盘中查看。",
                "ccDock",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        Application.EnableVisualStyles();
        Application.SetCompatibleTextRenderingDefault(false);
        try
        {
            Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        }
        catch (Exception)
        {
            // Older Windows builds may not support per-monitor DPI.
        }

        AppPaths.EnsureCreated();

        Application.ThreadException += (_, e) =>
            AppLog.Error("Unhandled UI thread exception.", e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            AppLog.Error("Unhandled application exception.", e.ExceptionObject as Exception);

        var startMinimized = args.Any(argument =>
            string.Equals(argument, "--minimized", StringComparison.OrdinalIgnoreCase));
        var settings = new SettingsStore();

        using var form = new MainForm(settings, startMinimized);
        Application.Run(form);
    }
}
