using CcDock.App;
using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Pages;

public sealed class OverviewPage : PageBase
{
    private readonly Label _engineDot;
    private readonly Label _engineTitle;
    private readonly Label _engineDetail;
    private readonly Label _engineError;
    private readonly RoundedButton _engineButton;

    private readonly Label _statTasks;
    private readonly Label _statDevices;
    private readonly Label _statPending;

    private readonly Label _deviceName;
    private readonly Label _deviceId;

    private readonly Panel _pendingHost;
    private readonly TableLayoutPanel _pendingList;
    private readonly Label _pendingEmpty;
    private string _pendingSignature = "\0";

    public override bool AutoRefresh => true;

    public OverviewPage(MainForm host)
        : base(host, "概览", "同步引擎状态、备份任务与待处理的分享请求。")
    {
        var create = Ui.Button("新建备份任务", ButtonKind.Primary, Glyphs.Add, 162);
        create.Click += async (_, _) => await Host.CreateBackupAsync();
        AddAction(create);

        // Engine status card.
        var engineCard = Ui.Card();
        _engineDot = Ui.Text("●", 18F, Theme.Success);
        _engineDot.Location = new Point(22, 20);
        _engineTitle = Ui.Text("同步引擎", 13F, Theme.TextPrimary, FontStyle.Bold);
        _engineTitle.Location = new Point(52, 18);
        _engineDetail = Ui.Text(string.Empty, 10F, Theme.TextSecondary);
        _engineDetail.AutoSize = false;
        _engineDetail.AutoEllipsis = true;
        _engineDetail.Location = new Point(53, 46);
        _engineError = Ui.Text(string.Empty, 9.5F, Theme.Danger);
        _engineError.AutoSize = false;
        _engineError.AutoEllipsis = true;
        _engineError.Visible = false;
        _engineButton = Ui.Button("重启引擎", ButtonKind.Secondary, Glyphs.Refresh, 122);
        _engineButton.Height = 36;
        _engineButton.Click += async (_, _) => await Host.RestartEngineAsync();

        engineCard.Controls.Add(_engineDot);
        engineCard.Controls.Add(_engineTitle);
        engineCard.Controls.Add(_engineDetail);
        engineCard.Controls.Add(_engineError);
        engineCard.Controls.Add(_engineButton);
        engineCard.Resize += (_, _) =>
        {
            _engineButton.Location = new Point(
                engineCard.ClientSize.Width - _engineButton.Width - Ui.Px(engineCard, 24),
                Ui.Px(engineCard, 24));
            _engineDetail.Width = Math.Max(
                Ui.Px(engineCard, 120),
                engineCard.ClientSize.Width - Ui.Px(engineCard, 53) - Ui.Px(engineCard, 170));
            _engineError.SetBounds(
                Ui.Px(engineCard, 53),
                Ui.Px(engineCard, 68),
                Math.Max(Ui.Px(engineCard, 120),
                    engineCard.ClientSize.Width - Ui.Px(engineCard, 53) - Ui.Px(engineCard, 170)),
                Ui.Px(engineCard, 44));
        };
        AddRow(engineCard, 140);

        // Summary tiles.
        var stats = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3,
            RowCount = 1,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        for (var i = 0; i < 3; i++)
        {
            stats.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));
        }
        _statTasks = AddStatTile(stats, 0, "备份任务");
        _statDevices = AddStatTile(stats, 1, "在线设备");
        _statPending = AddStatTile(stats, 2, "待处理请求");
        AddRow(stats, 102);

        // Local device identity card.
        var deviceCard = Ui.Card();
        var deviceTitle = Ui.Text("本机设备 ID", 11.5F, Theme.TextPrimary, FontStyle.Bold);
        deviceTitle.Location = new Point(22, 16);
        _deviceName = Ui.Text("设备名称：—", 10F, Theme.TextSecondary);
        _deviceName.AutoSize = false;
        _deviceName.AutoEllipsis = true;
        _deviceName.Location = new Point(22, 42);
        _deviceName.Height = 22;
        _deviceId = Ui.Text("—", 10F, Theme.Primary);
        _deviceId.Font = Theme.MonoFont(10F);
        _deviceId.AutoSize = false;
        _deviceId.AutoEllipsis = true;
        _deviceId.Location = new Point(22, 66);
        var copyButton = Ui.Button("复制", ButtonKind.Secondary, Glyphs.Copy, 88);
        copyButton.Height = 34;
        copyButton.Click += (_, _) => CopyDeviceId();
        var idHint = Ui.Text("在对方电脑输入此设备 ID 即可建立连接。", 9.5F, Theme.TextMuted);
        idHint.Location = new Point(22, 100);
        deviceCard.Controls.Add(deviceTitle);
        deviceCard.Controls.Add(_deviceName);
        deviceCard.Controls.Add(_deviceId);
        deviceCard.Controls.Add(copyButton);
        deviceCard.Controls.Add(idHint);
        deviceCard.Resize += (_, _) =>
        {
            copyButton.Location = new Point(
                deviceCard.ClientSize.Width - copyButton.Width - Ui.Px(deviceCard, 22),
                Ui.Px(deviceCard, 18));
            var textWidth = Math.Max(
                Ui.Px(deviceCard, 120),
                deviceCard.ClientSize.Width - Ui.Px(deviceCard, 22) - copyButton.Width - Ui.Px(deviceCard, 34));
            _deviceId.Width = textWidth;
            _deviceName.Width = Math.Max(
                Ui.Px(deviceCard, 120),
                deviceCard.ClientSize.Width - Ui.Px(deviceCard, 44));
        };
        AddRow(deviceCard, 128);

        // Pending requests card.
        var pendingCard = Ui.Card();
        var pendingTitle = Ui.Text("待处理的分享请求", 11.5F, Theme.TextPrimary, FontStyle.Bold);
        pendingTitle.Location = new Point(22, 16);
        _pendingHost = new Panel
        {
            Location = new Point(12, 46),
            BackColor = Color.Transparent,
            AutoScroll = true
        };
        _pendingList = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };
        _pendingList.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _pendingHost.Controls.Add(_pendingList);
        _pendingEmpty = Ui.Wrap(
            "暂无待处理的请求。当对方电脑添加本机或共享文件夹时，会显示在这里。",
            640, 10F, Theme.TextMuted);
        _pendingEmpty.Location = new Point(24, 58);
        pendingCard.Controls.Add(pendingTitle);
        pendingCard.Controls.Add(_pendingHost);
        pendingCard.Controls.Add(_pendingEmpty);
        pendingCard.Resize += (_, _) =>
        {
            _pendingHost.Size = new Size(
                Math.Max(Ui.Px(pendingCard, 120), pendingCard.ClientSize.Width - Ui.Px(pendingCard, 24)),
                Math.Max(Ui.Px(pendingCard, 80), pendingCard.ClientSize.Height - Ui.Px(pendingCard, 58)));
            _pendingEmpty.MaximumSize = new Size(
                Math.Max(Ui.Px(pendingCard, 200), pendingCard.ClientSize.Width - Ui.Px(pendingCard, 50)), 0);
        };
        AddRow(pendingCard, 236);
    }

    public override async Task RefreshAsync()
    {
        UpdateEngineCard();

        if (!Service.IsRunning)
        {
            _statTasks.Text = "—";
            _statDevices.Text = "—";
            _statPending.Text = "—";
            _deviceName.Text = "设备名称：—";
            _deviceId.Text = "—";
            SetPendingMessage("同步引擎未运行。启动引擎后即可查看待处理请求。");
            _pendingSignature = "\0";
            return;
        }

        try
        {
            var foldersTask = Service.GetFolderViewsAsync();
            var devicesTask = Service.GetDeviceViewsAsync();
            var pendingTask = Service.GetPendingAsync();
            await Task.WhenAll(foldersTask, devicesTask, pendingTask);

            var folders = await foldersTask;
            var devices = await devicesTask;
            var pending = await pendingTask;

            _statTasks.Text = folders.Count.ToString();
            _statDevices.Text = devices.Count(view => !view.IsSelf && view.Connected).ToString();
            _statPending.Text = (pending.Devices.Count + pending.Folders.Count).ToString();

            _deviceName.Text = "设备名称：" + (string.IsNullOrWhiteSpace(Service.DeviceName)
                ? Environment.MachineName
                : Service.DeviceName);
            _deviceId.Text = string.IsNullOrWhiteSpace(Service.DeviceId) ? "—" : Service.DeviceId;

            RebuildPending(pending);
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unable to refresh the overview page: " + ex.Message);
            _engineError.Text = "无法读取同步状态：" + ex.Message;
            _engineError.Visible = true;
        }
    }

    private static Label AddStatTile(TableLayoutPanel stats, int column, string caption)
    {
        var card = Ui.Card();
        card.Margin = new Padding(0, 0, column == 2 ? 0 : 12, 0);

        var value = Ui.Text("—", 21F, Theme.TextPrimary, FontStyle.Bold);
        value.Location = new Point(20, 14);
        var label = Ui.Text(caption, 10F, Theme.TextSecondary);
        label.Location = new Point(21, 54);
        card.Controls.Add(value);
        card.Controls.Add(label);

        stats.Controls.Add(card, column, 0);
        return value;
    }

    private void UpdateEngineCard()
    {
        var state = Service.State;
        var color = state switch
        {
            EngineState.Running => Theme.Success,
            EngineState.Starting => Theme.Warning,
            EngineState.Error => Theme.Danger,
            _ => Theme.TextMuted
        };

        _engineDot.ForeColor = color;
        switch (state)
        {
            case EngineState.Running:
                _engineTitle.Text = "同步引擎运行中";
                _engineDetail.Text = $"{Service.DeviceName} · Syncthing {Service.Version}";
                _engineError.Visible = false;
                break;
            case EngineState.Starting:
                _engineTitle.Text = "正在启动同步引擎…";
                _engineDetail.Text = "正在准备本地索引，请稍候。";
                _engineError.Visible = false;
                break;
            case EngineState.Error:
                _engineTitle.Text = "同步引擎出现问题";
                _engineDetail.Text = "请尝试重新启动引擎；如仍失败，请查看日志。";
                _engineError.Text = Service.LastError ?? string.Empty;
                _engineError.Visible = !string.IsNullOrWhiteSpace(_engineError.Text);
                break;
            default:
                _engineTitle.Text = "同步引擎已停止";
                _engineDetail.Text = "启动引擎后即可继续同步备份。";
                _engineError.Visible = false;
                break;
        }

        _engineButton.Text = state == EngineState.Running ? "重启引擎" : "启动引擎";
        _engineButton.Enabled = state != EngineState.Starting;
    }

    private void CopyDeviceId()
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(Service.DeviceId))
            {
                Clipboard.SetText(Service.DeviceId);
                Host.ShowToast("设备 ID 已复制到剪贴板。");
            }
        }
        catch (Exception ex)
        {
            AppLog.Warn("Unable to copy the device ID: " + ex.Message);
            Host.ShowToast("无法复制设备 ID。", isError: true);
        }
    }

    private void SetPendingMessage(string message)
    {
        _pendingList.SuspendLayout();
        foreach (Control control in _pendingList.Controls.Cast<Control>().ToArray())
        {
            control.Dispose();
        }
        _pendingList.Controls.Clear();
        _pendingList.RowStyles.Clear();
        _pendingList.RowCount = 0;
        _pendingList.ResumeLayout();
        _pendingEmpty.Text = message;
        _pendingEmpty.Visible = true;
        _pendingEmpty.BringToFront();
    }

    private void RebuildPending(
        (IReadOnlyList<PendingDeviceInfo> Devices, IReadOnlyList<PendingFolderOffer> Folders) pending)
    {
        var signature = string.Join(
            "|",
            pending.Folders.Select(offer => "F:" + offer.FolderId + ":" + offer.DeviceId).Concat(
                pending.Devices.Select(device => "D:" + device.DeviceId)));
        if (string.Equals(signature, _pendingSignature, StringComparison.Ordinal))
        {
            return;
        }
        _pendingSignature = signature;

        _pendingList.SuspendLayout();
        foreach (Control control in _pendingList.Controls.Cast<Control>().ToArray())
        {
            control.Dispose();
        }
        _pendingList.Controls.Clear();
        _pendingList.RowStyles.Clear();
        _pendingList.RowCount = 0;

        foreach (var offer in pending.Folders)
        {
            var current = offer;
            EventHandler accept = async (_, _) => await Host.AcceptFolderAsync(current);
            AddPendingRow(Ui.ActionRow(
                $"「{current.Label}」来自 {current.DeviceName}",
                current.ReceiveEncrypted ? "对方希望共享一个加密文件夹" : "对方希望与您共享一个文件夹",
                ("接收", ButtonKind.Primary, accept)));
        }

        foreach (var device in pending.Devices)
        {
            var current = device;
            EventHandler add = async (_, _) => await Host.RunTaskAsync(
                async () => await Service.AcceptPendingDeviceAsync(current.DeviceId, current.Name),
                "设备已添加。");
            EventHandler ignore = async (_, _) => await Host.RunTaskAsync(
                async () => await Service.IgnorePendingDeviceAsync(current.DeviceId),
                "已忽略该设备请求。");
            AddPendingRow(Ui.ActionRow(
                string.IsNullOrWhiteSpace(current.Name)
                    ? DeviceIdValidator.Shorten(current.DeviceId)
                    : current.Name,
                "新的设备连接请求 · " + DeviceIdValidator.Shorten(current.DeviceId),
                ("添加", ButtonKind.Primary, add),
                ("忽略", ButtonKind.Ghost, ignore)));
        }

        var empty = pending.Folders.Count == 0 && pending.Devices.Count == 0;
        _pendingEmpty.Text = empty
            ? "暂无待处理的请求。当对方电脑添加本机或共享文件夹时，会显示在这里。"
            : string.Empty;
        _pendingEmpty.Visible = empty;
        if (empty)
        {
            _pendingEmpty.BringToFront();
        }
        _pendingList.ResumeLayout();
    }

    private void AddPendingRow(Control row)
    {
        _pendingList.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.Px(_pendingList, 58)));
        _pendingList.RowCount = _pendingList.RowStyles.Count;
        _pendingList.Controls.Add(row, 0, _pendingList.RowCount - 1);
    }
}
