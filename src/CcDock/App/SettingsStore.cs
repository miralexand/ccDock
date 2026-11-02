using System.Text.Json;

namespace CcDock.App;

public sealed class AppSettings
{
    public bool CloseToTray { get; set; } = true;

    public bool MinimizeToTray { get; set; } = true;

    public bool TrayHintShown { get; set; }

    public int WindowWidth { get; set; } = 1100;

    public int WindowHeight { get; set; } = 720;

    public bool WindowMaximized { get; set; }
}

public sealed class SettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly object _gate = new();
    private AppSettings _current;

    public SettingsStore()
    {
        _current = Load();
    }

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _current;
            }
        }
    }

    public void Save()
    {
        lock (_gate)
        {
            try
            {
                AppPaths.EnsureCreated();
                var temp = AppPaths.SettingsFile + ".tmp";
                File.WriteAllText(temp, JsonSerializer.Serialize(_current, JsonOptions));
                File.Move(temp, AppPaths.SettingsFile, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLog.Error("Unable to save application settings.", ex);
            }
        }
    }

    public void Update(Action<AppSettings> change)
    {
        lock (_gate)
        {
            change(_current);
        }
        Save();
    }

    private static AppSettings Load()
    {
        try
        {
            if (File.Exists(AppPaths.SettingsFile))
            {
                var json = File.ReadAllText(AppPaths.SettingsFile);
                var settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (settings is not null)
                {
                    return settings;
                }
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to read application settings; using defaults.", ex);
        }

        return new AppSettings();
    }
}
