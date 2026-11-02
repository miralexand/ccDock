namespace CcDock.App;

public static class AppPaths
{
    public static string InstallDirectory => AppContext.BaseDirectory;

    public static string SyncthingExe => Path.Combine(InstallDirectory, "syncthing", "syncthing.exe");

    public static string SyncthingLicense => Path.Combine(InstallDirectory, "syncthing", "LICENSE.txt");

    public static string DataRoot => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ccDock");

    public static string SyncthingHome => Path.Combine(DataRoot, "syncthing");

    public static string LogsDirectory => Path.Combine(DataRoot, "logs");

    public static string SettingsFile => Path.Combine(DataRoot, "settings.json");

    public static string AppLogFile => Path.Combine(LogsDirectory, "app.log");

    public static string SyncthingConfigFile => Path.Combine(SyncthingHome, "config.xml");

    public static void EnsureCreated()
    {
        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(SyncthingHome);
        Directory.CreateDirectory(LogsDirectory);
    }
}
