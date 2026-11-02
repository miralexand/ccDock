using CcDock.App;
using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Pages;

public sealed class TasksPage : PageBase
{
    public TasksPage(MainForm host)
        : base(host, "备份任务", "管理已建立的同步与备份任务。")
    {
        var refresh = Ui.Button("刷新", ButtonKind.Secondary, Glyphs.Refresh, 102);
        refresh.Click += async (_, _) => await Host.RefreshCurrentPageAsync();
        AddAction(refresh);

        var create = Ui.Button("新建备份任务", ButtonKind.Primary, Glyphs.Add, 162);
        create.Click += async (_, _) => await Host.CreateBackupAsync();
        AddAction(create);
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
                    "启动同步引擎后才能读取和管理备份任务。",
                    "启动引擎",
                    async (_, _) => await Host.RestartEngineAsync()),
                190);
            return;
        }

        try
        {
            var folders = await Service.GetFolderViewsAsync();
            if (folders.Count == 0)
            {
                AddScaledRow(BuildEmptyCard(), 236);
                return;
            }

            foreach (var folder in folders)
            {
                AddScaledRow(BuildTaskCard(folder), 196);
            }
        }
        catch (Exception ex)
        {
            AppLog.Error("Unable to load backup tasks.", ex);
            AddScaledRow(
                BuildMessageCard(
                    Glyphs.Warning,
                    "无法读取备份任务",
                    ex.Message,
                    "重试",
                    async (_, _) => await Host.RefreshCurrentPageAsync()),
                190);
        }
    }

    private Control BuildEmptyCard()
    {
        var card = Ui.Card();
        var glyph = Ui.Text(Glyphs.Folder, 30F, Theme.Primary);
        var title = Ui.Text("还没有备份任务", 13F, Theme.TextPrimary, FontStyle.Bold);
        var description = Ui.Wrap(
            "选择一个文件夹，把它安全地同步到另一台电脑。\n文件只会在你的设备之间直接传输。",
            560, 9.5F, Theme.TextSecondary);
        var button = Ui.Button("新建备份任务", ButtonKind.Primary, Glyphs.Add, 162);
        button.Click += async (_, _) => await Host.CreateBackupAsync();

        card.Controls.Add(glyph);
        card.Controls.Add(title);
        card.Controls.Add(description);
        card.Controls.Add(button);
        card.Resize += (_, _) =>
        {
            var center = card.ClientSize.Width / 2;
            glyph.Location = new Point(center - glyph.Width / 2, Ui.Px(card, 34));
            title.Location = new Point(center - title.Width / 2, Ui.Px(card, 84));
            description.Location = new Point(center - description.Width / 2, Ui.Px(card, 116));
            button.Location = new Point(center - button.Width / 2, Ui.Px(card, 168));
        };
        return card;
    }

    private Control BuildTaskCard(FolderView view)
    {
        var folder = view.Folder;
        var card = Ui.Card();

        var glyph = Ui.Text(Glyphs.Folder, 18F, Theme.Primary);
        glyph.Location = new Point(24, 24);

        var title = Ui.Text(
            string.IsNullOrWhiteSpace(folder.Label) ? folder.Id : folder.Label,
            12.5F, Theme.TextPrimary, FontStyle.Bold);
        title.AutoSize = false;
        title.AutoEllipsis = true;
        title.Location = new Point(52, 20);
        title.Height = 24;

        var typeBadge = new Badge();
        if (folder.Type.Equals("receiveencrypted", StringComparison.OrdinalIgnoreCase))
        {
            typeBadge.Apply("加密接收", Theme.WarningSoft, Theme.Warning);
        }
        else if (view.IsReceiveOnly)
        {
            typeBadge.Apply("接收任务", Theme.SuccessSoft, Theme.Success);
        }
        else
        {
            typeBadge.Apply("备份任务", Theme.PrimarySoft, Theme.Primary);
        }

        var path = Ui.Text(folder.Path, 9F, Theme.TextSecondary);
        path.AutoSize = false;
        path.AutoEllipsis = true;
        path.Location = new Point(52, 48);
        path.Height = 18;

        var remoteNames = view.RemoteDevices.Count == 0
            ? "尚未配置接收设备"
            : string.Join("、", view.RemoteDevices.Select(device => device.Name));
        var connectionColor = view.RemoteDevices.Count == 0
            ? Theme.TextMuted
            : view.Connected ? Theme.Success : Theme.TextMuted;
        var connectionText = view.RemoteDevices.Count == 0
            ? string.Empty
            : view.Connected ? " · 已连接" : " · 未连接";

        var remoteDot = Ui.Text("●", 7F, connectionColor);
        remoteDot.Location = new Point(52, 76);
        var remote = Ui.Text("对方：" + remoteNames + connectionText, 9.5F, Theme.TextMuted);
        remote.AutoSize = false;
        remote.AutoEllipsis = true;
        remote.Location = new Point(66, 72);
        remote.Height = 20;

        var bar = new ThinProgressBar
        {
            Location = new Point(24, 108),
            Height = 8
        };
        var progress = ComputeProgress(view);
        bar.Value = progress;
        bar.BarColor = view.Status.Errors > 0 || view.Status.State == "error"
            ? Theme.Danger
            : progress >= 100 ? Theme.Success : Theme.Primary;

        var status = Ui.Text(StatusText(view), 9.5F, StatusColor(view));
        status.AutoSize = false;
        status.AutoEllipsis = true;
        status.Location = new Point(24, 124);
        status.Height = 20;

        var open = Ui.Button("打开文件夹", ButtonKind.Secondary, Glyphs.OpenFolder, 136);
        open.Height = 34;
        open.Click += (_, _) =>
        {
            if (!Ui.TryOpen(folder.Path))
            {
                Host.ShowToast("文件夹不存在或无法打开。", isError: true);
            }
        };

        var remove = Ui.Button("移除", ButtonKind.Ghost, Glyphs.Delete, 88);
        remove.Height = 34;
        remove.ForeColor = Theme.Danger;
        remove.Click += async (_, _) => await RemoveFolderAsync(folder);

        card.Controls.Add(glyph);
        card.Controls.Add(title);
        card.Controls.Add(typeBadge);
        card.Controls.Add(path);
        card.Controls.Add(remoteDot);
        card.Controls.Add(remote);
        card.Controls.Add(bar);
        card.Controls.Add(status);
        card.Controls.Add(open);
        card.Controls.Add(remove);

        card.Resize += (_, _) =>
        {
            var width = card.ClientSize.Width;
            title.Width = Math.Max(
                Ui.Px(card, 120),
                width - Ui.Px(card, 52) - typeBadge.Width - Ui.Px(card, 48));
            path.Width = Math.Max(Ui.Px(card, 120), width - Ui.Px(card, 76));
            remote.Width = Math.Max(Ui.Px(card, 120), width - Ui.Px(card, 90));
            bar.Width = Math.Max(Ui.Px(card, 80), width - Ui.Px(card, 48));
            status.Width = Math.Max(Ui.Px(card, 80), width - Ui.Px(card, 300));
            typeBadge.Location = new Point(
                width - typeBadge.Width - Ui.Px(card, 24),
                Ui.Px(card, 24));
            remove.Location = new Point(
                width - remove.Width - Ui.Px(card, 24),
                Ui.Px(card, 146));
            open.Location = new Point(
                remove.Left - open.Width - Ui.Px(card, 8),
                Ui.Px(card, 146));
        };

        return card;
    }

    private async Task RemoveFolderAsync(FolderInfo folder)
    {
        var label = string.IsNullOrWhiteSpace(folder.Label) ? folder.Id : folder.Label;
        var result = MessageBox.Show(
            this,
            $"确定要移除任务“{label}”吗？\n\n只会移除 ccDock 中的同步任务，不会删除本机文件。",
            "移除备份任务",
            MessageBoxButtons.OKCancel,
            MessageBoxIcon.Warning,
            MessageBoxDefaultButton.Button2);
        if (result != DialogResult.OK)
        {
            return;
        }

        await Host.RunTaskAsync(
            async () => await Service.RemoveFolderAsync(folder.Id),
            "备份任务已移除。");
    }

    private static double ComputeProgress(FolderView view)
    {
        if (view.Completion is { } completion)
        {
            return Math.Clamp(completion * 100.0, 0, 100);
        }

        if (view.Status.Errors > 0 || view.Status.State == "error")
        {
            return 0;
        }

        return view.Status.NeedFiles == 0 ? 100 : 0;
    }

    private static string StatusText(FolderView view)
    {
        if (view.Folder.Paused)
        {
            return "任务已暂停";
        }

        var state = view.Status.State;
        if (view.Status.Errors > 0 || !string.IsNullOrWhiteSpace(view.Status.ErrorMessage))
        {
            var detail = string.IsNullOrWhiteSpace(view.Status.ErrorMessage)
                ? $"{view.Status.Errors} 个错误"
                : view.Status.ErrorMessage;
            return "同步出错：" + detail;
        }

        if (state is "error")
        {
            return "同步出错，请打开对方电脑确认任务状态。";
        }

        if (state is "paused")
        {
            return "任务已暂停";
        }

        if (view.Status.NeedFiles > 0)
        {
            var bytes = Ui.FormatBytes(view.Status.NeedBytes);
            return state switch
            {
                "scanning" => $"正在扫描文件夹（{view.Status.NeedFiles} 个文件待处理）",
                "syncing" or "sync-preparing" or "sync-waiting" =>
                    $"正在同步 {view.Status.NeedFiles} 个文件 · {bytes}",
                _ => $"{view.Status.NeedFiles} 个文件待同步 · {bytes}"
            };
        }

        if (view.Completion is { } completion && completion < 0.999 && view.IsSendOnly)
        {
            return $"对方已接收 {completion:P0}，正在等待对方完成。";
        }

        return view.IsReceiveOnly ? "已是最新 · 仅接收对方修改" : "已同步 · 备份最新";
    }

    private static Color StatusColor(FolderView view)
    {
        if (view.Status.Errors > 0 || view.Status.State == "error")
        {
            return Theme.Danger;
        }
        if (view.Folder.Paused || view.Status.NeedFiles > 0)
        {
            return Theme.Warning;
        }
        return Theme.Success;
    }
}
