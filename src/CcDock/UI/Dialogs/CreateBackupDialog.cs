using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Dialogs;

public sealed class CreateBackupDialog : DialogBase
{
    private readonly SyncthingService _service;
    private readonly TextBox _name;
    private readonly TextBox _path;
    private readonly TextBox _deviceId;
    private readonly ComboBox _deviceCombo;

    private sealed record DeviceChoice(DeviceInfo Device)
    {
        public override string ToString()
            => $"{Device.Name}（{DeviceIdValidator.Shorten(Device.DeviceId)}）";
    }

    public CreateBackupDialog(SyncthingService service, IReadOnlyList<DeviceInfo> knownDevices)
        : base(
            "新建备份任务",
            "选择要备份的文件夹和接收方电脑。创建后对方确认即可开始同步。",
            "创建任务",
            new Size(520, 520))
    {
        _service = service;

        var nameLabel = Ui.Text("任务名称（可选）", 9.5F, Theme.TextSecondary);
        nameLabel.Location = new Point(0, 0);
        _name = Ui.Input("例如：工作文档备份");
        _name.Location = new Point(0, 20);
        _name.Width = Content.Width;

        var pathLabel = Ui.Text("要备份的文件夹", 9.5F, Theme.TextSecondary);
        pathLabel.Location = new Point(0, 64);
        _path = Ui.Input("选择本机文件夹");
        _path.Location = new Point(0, 84);
        _path.Width = Content.Width - 98;
        var browse = Ui.Button("浏览…", ButtonKind.Secondary, Glyphs.OpenFolder, 90);
        browse.Height = 34;
        browse.Location = new Point(Content.Width - 90, 84);
        browse.Click += (_, _) => BrowseForFolder();

        var deviceLabel = Ui.Text("从已有设备中选择（可选）", 9.5F, Theme.TextSecondary);
        deviceLabel.Location = new Point(0, 130);
        _deviceCombo = Ui.Combo();
        _deviceCombo.Location = new Point(0, 150);
        _deviceCombo.Width = Content.Width;
        _deviceCombo.Items.Add("手动输入设备 ID…");
        foreach (var device in knownDevices)
        {
            _deviceCombo.Items.Add(new DeviceChoice(device));
        }
        _deviceCombo.SelectedIndex = 0;

        var idLabel = Ui.Text("对方的设备 ID", 9.5F, Theme.TextSecondary);
        idLabel.Location = new Point(0, 190);
        _deviceId = Ui.Input("XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX");
        _deviceId.Location = new Point(0, 210);
        _deviceId.Width = Content.Width;
        _deviceCombo.SelectedIndexChanged += (_, _) =>
        {
            if (_deviceCombo.SelectedItem is DeviceChoice choice)
            {
                _deviceId.Text = choice.Device.DeviceId;
            }
        };

        var hint = Ui.Wrap(
            "在对方电脑打开 ccDock，进入“设备”页面复制该电脑的设备 ID。\n" +
            "创建后，本机会把该文件夹设为“仅发送”，对方接收后即可开始备份。",
            Content.Width, 9F, Theme.TextMuted);
        hint.Location = new Point(0, 252);

        Content.Controls.Add(nameLabel);
        Content.Controls.Add(_name);
        Content.Controls.Add(pathLabel);
        Content.Controls.Add(_path);
        Content.Controls.Add(browse);
        Content.Controls.Add(deviceLabel);
        Content.Controls.Add(_deviceCombo);
        Content.Controls.Add(idLabel);
        Content.Controls.Add(_deviceId);
        Content.Controls.Add(hint);
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择要备份的文件夹",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        _path.Text = dialog.SelectedPath;
        if (string.IsNullOrWhiteSpace(_name.Text))
        {
            _name.Text = Path.GetFileName(
                dialog.SelectedPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        }
    }

    protected override async Task OnSubmitAsync()
    {
        var path = _path.Text.Trim();
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            throw new SyncthingException("请选择一个存在的文件夹。");
        }

        if (!DeviceIdValidator.TryNormalize(_deviceId.Text, out var deviceId, out var error))
        {
            throw new SyncthingException(error ?? "设备 ID 无效。");
        }

        await _service.CreateBackupTaskAsync(path, deviceId, _name.Text.Trim());
        CloseWithSuccess();
    }
}
