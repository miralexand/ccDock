using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Dialogs;

public sealed class AcceptFolderDialog : DialogBase
{
    private readonly SyncthingService _service;
    private readonly PendingFolderOffer _offer;
    private readonly TextBox _path;
    private readonly CheckBox _keepVersions;

    public AcceptFolderDialog(SyncthingService service, PendingFolderOffer offer)
        : base(
            "接收共享文件夹",
            $"{offer.DeviceName} 希望与你共享“{offer.Label}”。选择一个本机位置来保存这些文件。",
            "接收",
            new Size(540, 440))
    {
        _service = service;
        _offer = offer;

        var summary = Ui.Wrap(
            offer.ReceiveEncrypted
                ? "这是一个加密共享文件夹，内容只能由接收方解密。"
                : "接收后，该文件夹会以“仅接收”模式保存，对方的修改会同步到本机，本机的修改不会回传。",
            Content.Width, 10F, Theme.TextMuted);
        summary.Location = new Point(0, 0);

        var pathLabel = Ui.Text("保存到", 10F, Theme.TextSecondary);
        pathLabel.Location = new Point(0, 54);
        _path = Ui.Input("选择保存位置");
        _path.Location = new Point(0, 74);
        _path.Width = Content.Width - 98;
        _path.Text = DefaultPath(offer.Label);
        var browse = Ui.Button("浏览…", ButtonKind.Secondary, Glyphs.OpenFolder, 90);
        browse.Height = 34;
        browse.Location = new Point(Content.Width - 90, 74);
        browse.Click += (_, _) => BrowseForFolder();

        _keepVersions = Ui.Check("保留历史版本（30 天内可恢复被覆盖或删除的文件）");
        _keepVersions.Location = new Point(0, 122);

        var hint = Ui.Wrap(
            "建议选择一个新的空文件夹作为保存位置，避免与已有文件混淆。",
            Content.Width, 9.5F, Theme.TextMuted);
        hint.Location = new Point(0, 158);

        Content.Controls.Add(summary);
        Content.Controls.Add(pathLabel);
        Content.Controls.Add(_path);
        Content.Controls.Add(browse);
        Content.Controls.Add(_keepVersions);
        Content.Controls.Add(hint);
    }

    private static string DefaultPath(string label)
    {
        var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        return Path.Combine(documents, "ccDock", Ui.SafeFileName(label));
    }

    private void BrowseForFolder()
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "选择共享文件夹的保存位置",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = true
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _path.Text = dialog.SelectedPath;
        }
    }

    protected override async Task OnSubmitAsync()
    {
        var path = _path.Text.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            throw new SyncthingException("请选择保存位置。");
        }

        await _service.AcceptPendingFolderAsync(
            _offer.FolderId,
            _offer.Label,
            _offer.DeviceId,
            path,
            _keepVersions.Checked);
        CloseWithSuccess();
    }
}
