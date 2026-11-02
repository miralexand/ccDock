using CcDock.App;
using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Pages;

public sealed class DevicesPage : PageBase
{
    public DevicesPage(MainForm host)
        : base(host, "设备", "管理连接本机的其他电脑。")
    {
        var add = Ui.Button("添加设备", ButtonKind.Primary, Glyphs.Add, 132);
        add.Click += async (_, _) => await Host.AddDeviceAsync();
        AddAction(add);
    }

    public override async Task RefreshAsync()
    {
        ClearBody();

        if (!Service.IsRunning)
        {
            AddScaledRow(
                BuildMessageCard(
                    Glyphs.Power,
                    "同步引擎未运行",
                    "启动同步引擎后才能读取设备列表。",
                    "启动引擎",
                    async (_, _) => await Host.RestartEngineAsync()),
                190);
            return;
        }

        try
        {
            var viewsTask = Service.GetDeviceViewsAsync();
            var pendingTask = Service.GetPendingAsync();
            await Task.WhenAll(viewsTask, pendingTask);
            var views = await viewsTask;
            var pending = await pendingTask;

            var self = views.FirstOrDefault(view => view.IsSelf);
            AddScaledRow(BuildSelfCard(self), 148);

            var others = views.Where(view => !view.IsSelf).ToList();
            var section = Ui.Text("其他设备", 11F, Theme.TextPrimary, FontStyle.Bold);
            section.TextAlign = ContentAlignment.MiddleLeft;
            AddScaledPlainRow(section, 26);

            if (others.Count == 0)
            {
                AddScaledRow(
                    BuildMessageCard(
                        Glyphs.Device,
                        "还没有添加其他设备",
                        "在对方电脑打开 ccDock，复制它的设备 ID，然后在本页添加即可开始配对。",
                        "添加设备",
                        async (_, _) => await Host.AddDeviceAsync()),
                    170);
            }
            else
            {
                foreach (var view in others)
                {
                    AddScaledRow(BuildDeviceCard(view), 94, 12);
                }
            }

            if (pending.Devices.Count > 0)
            {
                var pendingTitle = Ui.Text("待确认的设备请求", 11F, Theme.TextPrimary, FontStyle.Bold);
                pendingTitle.TextAlign = ContentAlignment.MiddleLeft;
                AddScaledPlainRow(pendingTitle, 26);

                var rows = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = pending.Devices.Count,
                    BackColor = Color.Transparent,
                    Margin = Padding.Empty,
                    AutoSize = true,
                    AutoSizeMode = AutoSizeMode.GrowAndShrink
                };
                rows.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
                foreach (var device in pending.Devices)
                {
                    var current = device;
                    EventHandler add = async (_, _) => await Host.RunTaskAsync(
                        async () => await Service.AcceptPendingDeviceAsync(current.DeviceId, current.Name),
                        "设备已添加。");
                    EventHandler ignore = async (_, _) => await Host.RunTaskAsync(
                        async () => await Service.IgnorePendingDeviceAsync(current.DeviceId),
                        "已忽略该设备请求。");
                    rows.RowStyles.Add(new RowStyle(SizeType.Absolute, Ui.Px(rows, 58)));
                    rows.Controls.Add(Ui.ActionRow(
                        string.IsNullOrWhiteSpace(current.Name)
                            ? DeviceIdValidator.Shorten(current.DeviceId)
                            : current.Name,
                        "新的设备连接请求 · " + DeviceIdValidator.Shorten(current.DeviceId),
                        ("添加", ButtonKind.Primary, add),
                        ("忽略", ButtonKind.Ghost, ignore)), 0, rows.RowCount - 1);
                }
                AddRow(
                    rows,
                    Ui.Px(Body, 58 * pending.Devices.Count + 12),
                    Ui.Px(Body, 16));
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to load devices.", ex);
            AddScaledRow(
                BuildMessageCard(
                    Glyphs.Warning,
                    "无法读取设备列表",
                    ex.Message,
                    "重试",
                    async (_, _) => await Host.RefreshCurrentPageAsync()),
                190);
        }
    }

    private Control BuildSelfCard(DeviceView? self)
    {
        var card = Ui.Card();
        var title = Ui.Text("本机", 11F, Theme.TextPrimary, FontStyle.Bold);
        title.Location = new Point(22, 16);

        var name = Ui.Text(
            string.IsNullOrWhiteSpace(Service.DeviceName) ? Environment.MachineName : Service.DeviceName,
            13F, Theme.TextPrimary, FontStyle.Bold);
        name.AutoSize = false;
        name.AutoEllipsis = true;
        name.Location = new Point(22, 42);
        name.Height = 30;

        var id = Ui.Text(
            string.IsNullOrWhiteSpace(Service.DeviceId) ? "—" : Service.DeviceId,
            9.5F, Theme.TextMuted);
        id.Font = Theme.MonoFont(9.5F);
        id.AutoSize = false;
        id.AutoEllipsis = true;
        id.Location = new Point(22, 76);
        id.Height = 22;

        var hint = Ui.Text(
            "把这台电脑的设备 ID 发给对方，对方添加后即可配对。",
            9F, Theme.TextMuted);
        hint.Location = new Point(22, 106);

        var rename = Ui.Button("重命名", ButtonKind.Secondary, null, 96);
        rename.Height = 32;
        rename.Click += async (_, _) => await Host.RenameDeviceAsync();

        var copy = Ui.Button("复制 ID", ButtonKind.Secondary, Glyphs.Copy, 104);
        copy.Height = 32;
        copy.Click += (_, _) => CopyId();

        card.Controls.Add(title);
        card.Controls.Add(name);
        card.Controls.Add(id);
        card.Controls.Add(hint);
        card.Controls.Add(rename);
        card.Controls.Add(copy);

        card.Resize += (_, _) =>
        {
            var width = card.ClientSize.Width;
            copy.Location = new Point(width - copy.Width - Ui.Px(card, 22), Ui.Px(card, 42));
            rename.Location = new Point(copy.Left - rename.Width - Ui.Px(card, 8), Ui.Px(card, 42));
            var textWidth = Math.Max(
                Ui.Px(card, 120),
                width - Ui.Px(card, 22) - rename.Width - copy.Width - Ui.Px(card, 50));
            id.Width = textWidth;
            name.Width = Math.Max(
                Ui.Px(card, 120),
                rename.Left - Ui.Px(card, 22) - Ui.Px(card, 16));
            hint.MaximumSize = new Size(
                Math.Max(Ui.Px(card, 200), width - Ui.Px(card, 44)), 0);
        };

        return card;
    }

    private Control BuildDeviceCard(DeviceView view)
    {
        var card = Ui.Card();
        var device = view.Device;

        var glyph = Ui.Text(Glyphs.Device, 16F, Theme.Primary);
        glyph.Location = new Point(22, 30);

        var name = Ui.Text(device.Name, 12F, Theme.TextPrimary, FontStyle.Bold);
        name.AutoSize = false;
        name.AutoEllipsis = true;
        name.Location = new Point(52, 18);
        name.Height = 22;

        var id = Ui.Text(device.DeviceId, 9F, Theme.TextMuted);
        id.Font = Theme.MonoFont(9F);
        id.AutoSize = false;
        id.AutoEllipsis = true;
        id.Location = new Point(52, 44);
        id.Height = 18;

        var detail = view.Connected
            ? (view.ViaRelay ? "中继连接" : "直连") +
              (string.IsNullOrWhiteSpace(view.Address) ? string.Empty : " · " + view.Address)
            : device.Paused
                ? "已暂停 · 未连接"
                : "未连接 · 等待对方上线或添加静态地址";
        var detailLabel = Ui.Text(
            detail,
            9F,
            view.Connected
                ? (view.ViaRelay ? Theme.Warning : Theme.Success)
                : Theme.TextMuted);
        detailLabel.AutoSize = false;
        detailLabel.AutoEllipsis = true;
        detailLabel.Location = new Point(52, 66);
        detailLabel.Height = 18;

        var badge = new Badge();
        if (view.Connected)
        {
            if (view.ViaRelay)
            {
                badge.Apply("中继连接", Theme.WarningSoft, Theme.Warning);
            }
            else
            {
                badge.Apply("直连", Theme.SuccessSoft, Theme.Success);
            }
        }
        else
        {
            badge.Apply("未连接", ColorTranslator.FromHtml("#F2F4F7"), Theme.TextMuted);
        }

        var configure = Ui.Button("连接设置", ButtonKind.Ghost, Glyphs.Settings, 104);
        configure.Height = 30;
        configure.Click += async (_, _) => await Host.EditDeviceAddressesAsync(device);

        card.Controls.Add(glyph);
        card.Controls.Add(name);
        card.Controls.Add(id);
        card.Controls.Add(detailLabel);
        card.Controls.Add(badge);
        card.Controls.Add(configure);
        card.Resize += (_, _) =>
        {
            var width = card.ClientSize.Width;
            badge.Location = new Point(
                width - badge.Width - Ui.Px(card, 22),
                Ui.Px(card, 22));
            configure.Location = new Point(
                width - configure.Width - Ui.Px(card, 22),
                Ui.Px(card, 52));
            name.Width = Math.Max(
                Ui.Px(card, 100),
                width - Ui.Px(card, 52) - badge.Width - Ui.Px(card, 48));
            id.Width = Math.Max(Ui.Px(card, 100), width - Ui.Px(card, 74));
            detailLabel.Width = Math.Max(
                Ui.Px(card, 100),
                width - Ui.Px(card, 52) - configure.Width - Ui.Px(card, 40));
        };

        return card;
    }

    private void CopyId()
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
}
