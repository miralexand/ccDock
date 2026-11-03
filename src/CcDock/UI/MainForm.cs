using CcDock.App;
using CcDock.Syncthing;
using CcDock.UI.Controls;
using CcDock.UI.Dialogs;
using CcDock.UI.Pages;

namespace CcDock.UI;

public sealed class MainForm : Form
{
    private readonly SettingsStore _settings;
    private readonly SyncthingService _service = new();
    private readonly Dictionary<string, PageBase> _pages = new();
    private readonly Dictionary<string, NavItem> _navItems = new();

    private readonly Panel _main;
    private readonly Panel _pageHost;
    private readonly Panel _toast;
    private readonly Label _toastLabel;
    private readonly System.Windows.Forms.Timer _autoRefresh;
    private readonly System.Windows.Forms.Timer _toastTimer;
    private readonly NotifyIcon _tray;

    private readonly Label _stateDot;
    private readonly Label _stateText;
    private readonly Label _versionLabel;

    private readonly bool _startMinimized;
    private string _currentKey = "overview";
    private bool _refreshing;
    private bool _engineBusy;
    private bool _reallyExit;
    private bool _shuttingDown;

    public MainForm(SettingsStore settings, bool startMinimized)
    {
        _settings = settings;
        _startMinimized = startMinimized;

        Text = "ccDock";
        BackColor = Theme.PageBackground;
        ForeColor = Theme.TextPrimary;
        Font = Theme.UIFont(10.5F);
        Icon = Theme.AppIcon;
        AutoScaleMode = AutoScaleMode.Dpi;
        DoubleBuffered = true;
        MinimumSize = new Size(1000, 640);
        StartPosition = FormStartPosition.CenterScreen;

        var saved = settings.Current;
        ClientSize = saved.WindowWidth >= 1000 && saved.WindowHeight >= 640
            ? new Size(saved.WindowWidth, saved.WindowHeight)
            : new Size(1120, 740);
        if (saved.WindowMaximized && !startMinimized)
        {
            WindowState = FormWindowState.Maximized;
        }

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = Theme.PageBackground,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 224F));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        var sidebar = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.SidebarBackground,
            Margin = Padding.Empty
        };
        root.Controls.Add(sidebar, 0, 0);

        _main = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.PageBackground,
            Margin = Padding.Empty
        };
        root.Controls.Add(_main, 1, 0);

        var sidebarState = BuildSidebar(sidebar);
        _stateDot = sidebarState.StateDot;
        _stateText = sidebarState.StateText;
        _versionLabel = sidebarState.VersionLabel;

        _pageHost = new Panel { BackColor = Theme.PageBackground };
        _main.Controls.Add(_pageHost);

        _toast = new Panel
        {
            BackColor = Theme.SuccessSoft,
            Visible = false,
            Padding = new Padding(28, 0, 0, 0)
        };
        _toastLabel = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
            Font = Theme.UIFont(10.5F),
            ForeColor = Theme.Success,
            BackColor = Color.Transparent
        };
        _toast.Controls.Add(_toastLabel);
        _main.Controls.Add(_toast);
        _main.Resize += (_, _) => LayoutMain();

        AddPage("overview", new OverviewPage(this));
        AddPage("tasks", new TasksPage(this));
        AddPage("devices", new DevicesPage(this));
        AddPage("settings", new SettingsPage(this));

        _navItems["overview"].Active = true;
        _pages["overview"].Visible = true;

        _autoRefresh = new System.Windows.Forms.Timer { Interval = 6000 };
        _autoRefresh.Tick += AutoRefreshTick;
        _autoRefresh.Start();

        _toastTimer = new System.Windows.Forms.Timer { Interval = 6000 };
        _toastTimer.Tick += (_, _) =>
        {
            _toastTimer.Stop();
            _toast.Visible = false;
            LayoutMain();
        };

        _tray = BuildTrayIcon();
        _service.StateChanged += ServiceOnStateChanged;

        // Scale the complete UI tree at once. Setting AutoScaleDimensions
        // earlier would scale only the still-empty form, leaving controls
        // built afterwards at design-time pixel sizes on high-DPI screens.
        AutoScaleDimensions = new SizeF(96F, 96F);

        LayoutMain();
    }

    public SyncthingService Service => _service;

    public SettingsStore Settings => _settings;

    public void ShowToast(string message, bool isError = false)
    {
        if (_shuttingDown)
        {
            return;
        }

        _toastLabel.Text = message;
        _toastLabel.ForeColor = isError ? Theme.Danger : Theme.Success;
        _toast.BackColor = isError ? Theme.DangerSoft : Theme.SuccessSoft;
        _toast.Visible = true;
        LayoutMain();
        _toastTimer.Stop();
        _toastTimer.Start();
    }

    public bool EnsureEngineRunning()
    {
        if (_service.IsRunning)
        {
            return true;
        }

        ShowToast("同步引擎未运行，请先在“设置”中启动引擎。", isError: true);
        return false;
    }

    public async Task RefreshCurrentPageAsync(bool silent = false)
    {
        if (_refreshing || _shuttingDown)
        {
            return;
        }

        if (!_pages.TryGetValue(_currentKey, out var page))
        {
            return;
        }

        _refreshing = true;
        try
        {
            await page.RefreshAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to refresh the current page.", ex);
            if (!silent)
            {
                ShowToast("刷新页面失败：" + ex.Message, isError: true);
            }
        }
        finally
        {
            _refreshing = false;
        }
    }

    public async Task RunTaskAsync(Func<Task> action, string successMessage)
    {
        try
        {
            await action();
            ShowToast(successMessage);
            await RefreshCurrentPageAsync();
        }
        catch (Exception ex)
        {
            AppLog.Error("A background UI action failed.", ex);
            ShowToast(
                ex is SyncthingException ? ex.Message : "操作失败：" + ex.Message,
                isError: true);
        }
    }

    public async Task CreateBackupAsync()
    {
        if (!EnsureEngineRunning())
        {
            return;
        }

        var devices = new List<DeviceInfo>();
        try
        {
            var views = await _service.GetDeviceViewsAsync();
            devices = views.Where(view => !view.IsSelf).Select(view => view.Device).ToList();
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unable to load devices for the backup dialog: " + ex.Message);
        }

        using var dialog = new CreateBackupDialog(_service, devices);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowToast("备份任务已创建，等待对方确认后开始同步。");
            await RefreshCurrentPageAsync();
        }
    }

    public async Task AcceptFolderAsync(PendingFolderOffer offer)
    {
        if (!EnsureEngineRunning())
        {
            return;
        }

        using var dialog = new AcceptFolderDialog(_service, offer);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowToast("已接收共享文件夹，正在开始同步。");
            await RefreshCurrentPageAsync();
        }
    }

    public async Task AddDeviceAsync()
    {
        if (!EnsureEngineRunning())
        {
            return;
        }

        using var dialog = new AddDeviceDialog(_service);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowToast("设备已添加。");
            await RefreshCurrentPageAsync();
        }
    }

    public async Task EditDeviceAddressesAsync(DeviceInfo device)
    {
        if (!EnsureEngineRunning())
        {
            return;
        }

        using var dialog = new DeviceAddressDialog(_service, device);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowToast("设备连接地址已更新。");
            await RefreshCurrentPageAsync();
        }
    }

    public async Task RenameDeviceAsync()
    {
        if (!EnsureEngineRunning())
        {
            return;
        }

        using var dialog = new RenameDeviceDialog(_service, _service.DeviceName);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            ShowToast("设备名称已更新。");
            await RefreshCurrentPageAsync();
        }
    }

    public async Task RestartEngineAsync()
    {
        if (_engineBusy)
        {
            return;
        }

        _engineBusy = true;
        UpdateSidebarState();
        try
        {
            var started = await _service.RestartAsync();
            UpdateSidebarState();
            await RefreshCurrentPageAsync(silent: true);
            ShowToast(
                started ? "同步引擎已重启。" : _service.LastError ?? "无法启动同步引擎。",
                isError: !started);
        }
        finally
        {
            _engineBusy = false;
        }
    }

    public async Task ToggleEngineAsync()
    {
        if (_engineBusy)
        {
            return;
        }

        if (_service.State == EngineState.Running)
        {
            _engineBusy = true;
            UpdateSidebarState();
            try
            {
                await _service.StopAsync();
                UpdateSidebarState();
                ShowToast("同步引擎已停止。");
                await RefreshCurrentPageAsync();
            }
            finally
            {
                _engineBusy = false;
            }
            return;
        }

        await StartEngineAsync(manual: true);
    }

    protected override async void OnShown(EventArgs e)
    {
        base.OnShown(e);

        if (_startMinimized)
        {
            Hide();
            ShowInTaskbar = false;
        }

        await StartEngineAsync(manual: false);
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        if (WindowState == FormWindowState.Minimized && _settings.Current.MinimizeToTray)
        {
            HideToTray(showHint: false);
        }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!_reallyExit &&
            e.CloseReason == CloseReason.UserClosing &&
            _settings.Current.CloseToTray)
        {
            e.Cancel = true;
            HideToTray(showHint: true);
            return;
        }

        _shuttingDown = true;
        SaveWindowState();
        base.OnFormClosing(e);
    }

    protected override void OnFormClosed(FormClosedEventArgs e)
    {
        _autoRefresh.Stop();
        _autoRefresh.Dispose();
        _toastTimer.Stop();
        _toastTimer.Dispose();
        _service.StateChanged -= ServiceOnStateChanged;

        _tray.Visible = false;
        _tray.ContextMenuStrip?.Dispose();
        _tray.Dispose();

        try
        {
            _service.DisposeAsync().AsTask().Wait(TimeSpan.FromSeconds(8));
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unable to shut down the sync engine cleanly: " + ex.Message);
        }

        base.OnFormClosed(e);
    }

    private async Task StartEngineAsync(bool manual)
    {
        if (_engineBusy)
        {
            return;
        }

        _engineBusy = true;
        UpdateSidebarState();
        try
        {
            var started = await _service.StartAsync();
            UpdateSidebarState();
            await RefreshCurrentPageAsync(silent: true);
            if (!started)
            {
                var message = _service.LastError ?? "无法启动同步引擎。";
                ShowToast(message, isError: true);
                if (manual)
                {
                    MessageBox.Show(this, message, "ccDock", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }
        finally
        {
            _engineBusy = false;
        }
    }

    private void AddPage(string key, PageBase page)
    {
        _pages[key] = page;
        page.Visible = false;
        _pageHost.Controls.Add(page);
    }

    private async Task NavigateAsync(string key)
    {
        if (!_pages.TryGetValue(key, out var page))
        {
            return;
        }

        _currentKey = key;
        foreach (var pair in _pages)
        {
            pair.Value.Visible = string.Equals(pair.Key, key, StringComparison.Ordinal);
        }
        foreach (var pair in _navItems)
        {
            pair.Value.Active = string.Equals(pair.Key, key, StringComparison.Ordinal);
        }

        await RefreshCurrentPageAsync();
    }

    private (Label StateDot, Label StateText, Label VersionLabel) BuildSidebar(Panel sidebar)
    {
        var icon = new PictureBox
        {
            Image = Theme.AppIcon.ToBitmap(),
            Location = new Point(20, 20),
            Size = new Size(36, 36),
            SizeMode = PictureBoxSizeMode.Zoom,
            BackColor = Color.Transparent
        };
        var title = new Label
        {
            Text = "ccDock",
            AutoSize = true,
            Font = Theme.UIFont(16F, FontStyle.Bold),
            ForeColor = Color.White,
            BackColor = Color.Transparent,
            Location = new Point(66, 16)
        };
        var subtitle = new Label
        {
            Text = "文件夹同步备份",
            AutoSize = true,
            Font = Theme.UIFont(9.5F),
            ForeColor = Theme.SidebarMuted,
            BackColor = Color.Transparent,
            Location = new Point(67, 46)
        };
        var divider = new Panel
        {
            Location = new Point(20, 86),
            Size = new Size(184, 1),
            BackColor = ColorTranslator.FromHtml("#273345")
        };

        sidebar.Controls.Add(icon);
        sidebar.Controls.Add(title);
        sidebar.Controls.Add(subtitle);
        sidebar.Controls.Add(divider);

        var y = 104;
        AddNav(sidebar, "overview", "概览", Glyphs.Home, y);
        AddNav(sidebar, "tasks", "备份任务", Glyphs.Folder, y + 52);
        AddNav(sidebar, "devices", "设备", Glyphs.Device, y + 104);
        AddNav(sidebar, "settings", "设置", Glyphs.Settings, y + 156);

        var stateDot = new Label
        {
            Text = "●",
            AutoSize = true,
            Font = Theme.UIFont(11F),
            ForeColor = Theme.SidebarMuted,
            BackColor = Color.Transparent
        };
        var stateText = new Label
        {
            Text = "同步引擎未启动",
            AutoSize = false,
            AutoEllipsis = true,
            Font = Theme.UIFont(10F),
            ForeColor = Theme.SidebarText,
            BackColor = Color.Transparent
        };
        var versionLabel = new Label
        {
            Text = "ccDock 1.1.1",
            AutoSize = false,
            AutoEllipsis = true,
            Font = Theme.UIFont(9.5F),
            ForeColor = Theme.SidebarMuted,
            BackColor = Color.Transparent
        };
        sidebar.Controls.Add(stateDot);
        sidebar.Controls.Add(stateText);
        sidebar.Controls.Add(versionLabel);

        sidebar.Resize += (_, _) =>
        {
            stateDot.Location = new Point(Ui.Px(sidebar, 20), sidebar.ClientSize.Height - Ui.Px(sidebar, 66));
            stateText.SetBounds(
                Ui.Px(sidebar, 38),
                sidebar.ClientSize.Height - Ui.Px(sidebar, 70),
                sidebar.ClientSize.Width - Ui.Px(sidebar, 58),
                Ui.Px(sidebar, 20));
            versionLabel.SetBounds(
                Ui.Px(sidebar, 38),
                sidebar.ClientSize.Height - Ui.Px(sidebar, 48),
                sidebar.ClientSize.Width - Ui.Px(sidebar, 58),
                Ui.Px(sidebar, 20));

            var navWidth = Math.Max(Ui.Px(sidebar, 60), sidebar.ClientSize.Width - Ui.Px(sidebar, 28));
            foreach (var item in _navItems.Values)
            {
                item.Left = Ui.Px(sidebar, 14);
                item.Width = navWidth;
            }
        };

        return (stateDot, stateText, versionLabel);
    }

    private void AddNav(Panel sidebar, string key, string text, string glyph, int top)
    {
        var item = new NavItem
        {
            Key = key,
            Text = text,
            Glyph = glyph,
            Location = new Point(14, top),
            Size = new Size(196, 46)
        };
        item.Click += async (_, _) => await NavigateAsync(key);
        sidebar.Controls.Add(item);
        _navItems[key] = item;
    }

    private NotifyIcon BuildTrayIcon()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add(new ToolStripMenuItem("打开 ccDock", null, (_, _) => ShowFromTray()));
        menu.Items.Add(new ToolStripMenuItem("新建备份任务", null, async (_, _) =>
        {
            ShowFromTray();
            await CreateBackupAsync();
        }));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add(new ToolStripMenuItem("退出", null, (_, _) => ExitApplication()));

        var tray = new NotifyIcon
        {
            Icon = Theme.AppIcon,
            Text = "ccDock · 文件夹同步备份",
            Visible = true,
            ContextMenuStrip = menu
        };
        tray.DoubleClick += (_, _) => ShowFromTray();
        return tray;
    }

    private void ShowFromTray()
    {
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;
        Activate();
        BringToFront();
    }

    private void HideToTray(bool showHint)
    {
        Hide();
        ShowInTaskbar = false;
        if (!showHint || _settings.Current.TrayHintShown)
        {
            return;
        }

        _tray.ShowBalloonTip(
            3500,
            "ccDock 仍在运行",
            "同步会继续在后台进行。双击托盘图标即可重新打开。",
            ToolTipIcon.Info);
        _settings.Update(settings => settings.TrayHintShown = true);
    }

    private void ExitApplication()
    {
        _reallyExit = true;
        Close();
    }

    private void ServiceOnStateChanged(object? sender, EngineStateChangedEventArgs e)
        => Post(() =>
        {
            UpdateSidebarState();
            if (_pages.TryGetValue(_currentKey, out var page) && page.AutoRefresh)
            {
                _ = RefreshCurrentPageAsync(silent: true);
            }
        });

    private void UpdateSidebarState()
    {
        var (text, color) = _service.State switch
        {
            EngineState.Running => ("同步引擎运行中", Theme.SidebarSuccess),
            EngineState.Starting => ("正在启动同步引擎…", Theme.SidebarWarning),
            EngineState.Error => ("同步引擎出现异常", Theme.SidebarDanger),
            _ => ("同步引擎未启动", Theme.SidebarMuted)
        };

        _stateDot.ForeColor = color;
        _stateText.Text = text;
        _stateText.ForeColor = _service.State == EngineState.Error
            ? Theme.SidebarDanger
            : Theme.SidebarText;
        _versionLabel.Text = string.IsNullOrWhiteSpace(_service.Version)
            ? "ccDock 1.1.1"
            : "Syncthing " + _service.Version;
    }

    private void AutoRefreshTick(object? sender, EventArgs e)
    {
        if (_shuttingDown || !Visible || WindowState == FormWindowState.Minimized)
        {
            return;
        }

        if (!_pages.TryGetValue(_currentKey, out var page) || !page.AutoRefresh)
        {
            return;
        }

        _ = RefreshCurrentPageAsync(silent: true);
    }

    private void LayoutMain()
    {
        var toastHeight = _toast.Visible ? Ui.Px(_main, 48) : 0;
        var width = _main.ClientSize.Width;
        var height = _main.ClientSize.Height;
        _pageHost.SetBounds(0, 0, width, Math.Max(0, height - toastHeight));
        _toast.SetBounds(0, Math.Max(0, height - toastHeight), width, toastHeight);
    }

    private void SaveWindowState()
    {
        var bounds = WindowState == FormWindowState.Normal ? Bounds : RestoreBounds;
        var maximized = WindowState == FormWindowState.Maximized;
        var factor = DeviceDpi / 96.0;
        var logicalWidth = (int)Math.Round(bounds.Width / factor);
        var logicalHeight = (int)Math.Round(bounds.Height / factor);
        _settings.Update(settings =>
        {
            if (logicalWidth >= 1000 && logicalHeight >= 640)
            {
                settings.WindowWidth = logicalWidth;
                settings.WindowHeight = logicalHeight;
            }
            settings.WindowMaximized = maximized;
        });
    }

    private void Post(Action action)
    {
        if (_shuttingDown || IsDisposed || !IsHandleCreated)
        {
            return;
        }

        try
        {
            BeginInvoke(action);
        }
        catch (InvalidOperationException)
        {
            // The window is closing; drop the update.
        }
    }
}
