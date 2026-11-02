using CcDock.App;
using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Pages;

public sealed class SettingsPage : PageBase
{
    private readonly CheckBox _autoStart;
    private readonly CheckBox _minimizeToTray;
    private readonly CheckBox _closeToTray;
    private readonly Label _engineState;
    private readonly Label _engineVersion;
    private readonly Label _dataDirectory;
    private readonly RoundedButton _engineToggle;
    private readonly CheckBox _globalDiscovery;
    private readonly CheckBox _localDiscovery;
    private readonly CheckBox _relays;
    private readonly CheckBox _natEnabled;
    private readonly Label _listenAddresses;
    private readonly RoundedButton _saveConnection;

    public SettingsPage(MainForm host)
        : base(host, "设置", "调整启动方式、连接策略与同步引擎。")
    {
        _autoStart = Ui.Check("开机自动启动 ccDock（最小化到托盘）");
        _minimizeToTray = Ui.Check("最小化窗口时隐藏到系统托盘");
        _closeToTray = Ui.Check("关闭窗口时继续在后台同步");

        var preferences = Ui.Card();
        var prefsTitle = Ui.Text("通用", 11F, Theme.TextPrimary, FontStyle.Bold);
        prefsTitle.Location = new Point(22, 16);
        _autoStart.Location = new Point(24, 50);
        _minimizeToTray.Location = new Point(24, 82);
        _closeToTray.Location = new Point(24, 114);
        var save = Ui.Button("保存设置", ButtonKind.Primary, null, 116);
        save.Height = 36;
        save.Click += (_, _) => SaveSettings();
        preferences.Controls.Add(prefsTitle);
        preferences.Controls.Add(_autoStart);
        preferences.Controls.Add(_minimizeToTray);
        preferences.Controls.Add(_closeToTray);
        preferences.Controls.Add(save);
        preferences.Resize += (_, _) =>
            save.Location = new Point(
                preferences.ClientSize.Width - save.Width - Ui.Px(preferences, 22),
                Ui.Px(preferences, 138));
        AddRow(preferences, 190);

        var engine = Ui.Card();
        var engineTitle = Ui.Text("同步引擎", 11F, Theme.TextPrimary, FontStyle.Bold);
        engineTitle.Location = new Point(22, 16);
        _engineState = Ui.Text("状态：—", 9.5F, Theme.TextSecondary);
        _engineState.Location = new Point(22, 48);
        _engineVersion = Ui.Text("版本：—", 9.5F, Theme.TextSecondary);
        _engineVersion.Location = new Point(22, 74);
        _dataDirectory = Ui.Text("数据目录：—", 9.5F, Theme.TextMuted);
        _dataDirectory.AutoSize = false;
        _dataDirectory.AutoEllipsis = true;
        _dataDirectory.Height = 20;
        _dataDirectory.Location = new Point(22, 100);

        var restart = Ui.Button("重启引擎", ButtonKind.Secondary, Glyphs.Refresh, 112);
        restart.Height = 34;
        restart.Click += async (_, _) => await Host.RestartEngineAsync();

        _engineToggle = Ui.Button("停止引擎", ButtonKind.Secondary, Glyphs.Power, 112);
        _engineToggle.Height = 34;
        _engineToggle.Click += async (_, _) => await Host.ToggleEngineAsync();

        var openData = Ui.Button("打开数据目录", ButtonKind.Ghost, Glyphs.OpenFolder, 136);
        openData.Height = 34;
        openData.Click += (_, _) =>
        {
            if (!Ui.TryOpen(AppPaths.DataRoot))
            {
                Host.ShowToast("无法打开数据目录。", isError: true);
            }
        };

        var openLogs = Ui.Button("查看日志", ButtonKind.Ghost, Glyphs.Info, 112);
        openLogs.Height = 34;
        openLogs.Click += (_, _) =>
        {
            if (!Ui.TryOpen(AppPaths.LogsDirectory))
            {
                Host.ShowToast("无法打开日志目录。", isError: true);
            }
        };

        engine.Controls.Add(engineTitle);
        engine.Controls.Add(_engineState);
        engine.Controls.Add(_engineVersion);
        engine.Controls.Add(_dataDirectory);
        engine.Controls.Add(restart);
        engine.Controls.Add(_engineToggle);
        engine.Controls.Add(openData);
        engine.Controls.Add(openLogs);

        engine.Resize += (_, _) =>
        {
            var width = engine.ClientSize.Width;
            var y = Ui.Px(engine, 144);
            openLogs.Location = new Point(width - openLogs.Width - Ui.Px(engine, 22), y);
            openData.Location = new Point(openLogs.Left - openData.Width - Ui.Px(engine, 6), y);
            _engineToggle.Location = new Point(openData.Left - _engineToggle.Width - Ui.Px(engine, 20), y);
            restart.Location = new Point(_engineToggle.Left - restart.Width - Ui.Px(engine, 6), y);
            _dataDirectory.Width = Math.Max(Ui.Px(engine, 160), width - Ui.Px(engine, 44));
        };
        AddRow(engine, 208);

        _globalDiscovery = Ui.Check("启用全局发现（通过互联网查找对方设备）");
        _localDiscovery = Ui.Check("启用局域网发现（同一网段自动发现）");
        _relays = Ui.Check("允许中继转发（无法直连时使用，速度较慢）");
        _natEnabled = Ui.Check("启用 NAT 穿透（UPnP / NAT-PMP）");

        var connection = Ui.Card();
        var connectionTitle = Ui.Text("高级连接", 11F, Theme.TextPrimary, FontStyle.Bold);
        connectionTitle.Location = new Point(22, 16);
        _globalDiscovery.Location = new Point(24, 48);
        _localDiscovery.Location = new Point(24, 78);
        _relays.Location = new Point(24, 108);
        _natEnabled.Location = new Point(24, 138);

        _listenAddresses = Ui.Text("监听地址：—", 9F, Theme.TextMuted);
        _listenAddresses.AutoSize = false;
        _listenAddresses.AutoEllipsis = true;
        _listenAddresses.Location = new Point(24, 172);
        _listenAddresses.Height = 20;

        var connectionHint = Ui.Text(
            "跨网段或下游网络：到“设备”页为对方设置静态地址（如 tcp://192.168.1.50:22000）。",
            9F, Theme.TextMuted);
        connectionHint.AutoSize = false;
        connectionHint.AutoEllipsis = true;
        connectionHint.Location = new Point(24, 198);
        connectionHint.Height = 20;

        _saveConnection = Ui.Button("保存连接设置", ButtonKind.Primary, null, 140);
        _saveConnection.Height = 36;
        _saveConnection.Click += async (_, _) => await SaveConnectionSettingsAsync();

        connection.Controls.Add(connectionTitle);
        connection.Controls.Add(_globalDiscovery);
        connection.Controls.Add(_localDiscovery);
        connection.Controls.Add(_relays);
        connection.Controls.Add(_natEnabled);
        connection.Controls.Add(_listenAddresses);
        connection.Controls.Add(connectionHint);
        connection.Controls.Add(_saveConnection);
        connection.Resize += (_, _) =>
        {
            var width = connection.ClientSize.Width;
            _saveConnection.Location = new Point(
                width - _saveConnection.Width - Ui.Px(connection, 22),
                Ui.Px(connection, 190));
            _listenAddresses.Width = Math.Max(
                Ui.Px(connection, 180),
                width - Ui.Px(connection, 44));
            connectionHint.Width = Math.Max(
                Ui.Px(connection, 180),
                width - _saveConnection.Width - Ui.Px(connection, 68));
        };
        AddRow(connection, 244);

        var about = Ui.Card();
        var aboutTitle = Ui.Text("关于 ccDock", 11F, Theme.TextPrimary, FontStyle.Bold);
        aboutTitle.Location = new Point(22, 16);
        var aboutLine = Ui.Text(
            "ccDock 1.1.0 · 基于 Syncthing 2.1.7（MPL-2.0 开源许可）",
            9.5F, Theme.TextSecondary);
        aboutLine.Location = new Point(22, 48);
        var privacy = Ui.Wrap(
            "文件只会在你的电脑之间直接加密传输，不经过任何第三方服务器。ccDock 不会上传或分析你的文件。",
            700, 9.5F, Theme.TextMuted);
        privacy.Location = new Point(22, 74);
        var license = Ui.Button("查看开源许可", ButtonKind.Ghost, Glyphs.Info, 128);
        license.Height = 32;
        license.Click += (_, _) =>
        {
            if (!Ui.TryOpen(AppPaths.SyncthingLicense))
            {
                Host.ShowToast("未找到许可文件。", isError: true);
            }
        };
        about.Controls.Add(aboutTitle);
        about.Controls.Add(aboutLine);
        about.Controls.Add(privacy);
        about.Controls.Add(license);
        about.Resize += (_, _) =>
        {
            privacy.MaximumSize = new Size(
                Math.Max(Ui.Px(about, 200), about.ClientSize.Width - Ui.Px(about, 44)), 0);
            license.Location = new Point(Ui.Px(about, 22), Ui.Px(about, 120));
        };
        AddRow(about, 168);
    }

    public override async Task RefreshAsync()
    {
        var settings = Host.Settings.Current;
        _minimizeToTray.Checked = settings.MinimizeToTray;
        _closeToTray.Checked = settings.CloseToTray;
        try
        {
            _autoStart.Checked = new StartupManager().IsEnabled();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unable to read auto-start state: " + ex.Message);
            _autoStart.Checked = false;
        }

        var state = Service.State;
        _engineState.Text = "状态：" + (state switch
        {
            EngineState.Running => "运行中",
            EngineState.Starting => "正在启动",
            EngineState.Error => "异常",
            _ => "已停止"
        });
        _engineState.ForeColor = state switch
        {
            EngineState.Running => Theme.Success,
            EngineState.Starting => Theme.Warning,
            EngineState.Error => Theme.Danger,
            _ => Theme.TextSecondary
        };
        _engineVersion.Text = "版本：Syncthing " +
            (string.IsNullOrWhiteSpace(Service.Version) ? "—" : Service.Version);
        _dataDirectory.Text = "数据目录：" + AppPaths.DataRoot;
        _engineToggle.Text = state == EngineState.Running ? "停止引擎" : "启动引擎";
        _engineToggle.Enabled = state != EngineState.Starting;

        var running = Service.IsRunning;
        _globalDiscovery.Enabled = running;
        _localDiscovery.Enabled = running;
        _relays.Enabled = running;
        _natEnabled.Enabled = running;
        _saveConnection.Enabled = running;

        if (!running)
        {
            _listenAddresses.Text = "监听地址：—";
            return;
        }

        try
        {
            var connection = await Service.GetConnectionSettingsAsync();
            _globalDiscovery.Checked = connection.GlobalDiscovery;
            _localDiscovery.Checked = connection.LocalDiscovery;
            _relays.Checked = connection.Relays;
            _natEnabled.Checked = connection.NatEnabled;
            _listenAddresses.Text = connection.ListenAddresses.Count == 0
                ? "监听地址：默认"
                : "监听地址：" + string.Join("，", connection.ListenAddresses);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unable to read connection settings: " + ex.Message);
            _listenAddresses.Text = "监听地址：无法读取";
        }
    }

    private async Task SaveConnectionSettingsAsync()
    {
        if (!Service.IsRunning)
        {
            Host.ShowToast("同步引擎未运行，无法应用连接设置。", isError: true);
            return;
        }

        var connection = new ConnectionSettings(
            _globalDiscovery.Checked,
            _localDiscovery.Checked,
            _relays.Checked,
            _natEnabled.Checked,
            Array.Empty<string>());

        await Host.RunTaskAsync(
            async () => await Service.SaveConnectionSettingsAsync(connection),
            "连接设置已保存。");
    }

    private void SaveSettings()
    {
        try
        {
            var autoStart = _autoStart.Checked;
            new StartupManager().SetEnabled(autoStart);
            Host.Settings.Update(settings =>
            {
                settings.MinimizeToTray = _minimizeToTray.Checked;
                settings.CloseToTray = _closeToTray.Checked;
            });
            Host.ShowToast("设置已保存。");
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to save settings.", ex);
            Host.ShowToast("保存设置失败：" + ex.Message, isError: true);
        }
    }
}
