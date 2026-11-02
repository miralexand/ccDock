using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Dialogs;

public sealed class RenameDeviceDialog : DialogBase
{
    private readonly SyncthingService _service;
    private readonly TextBox _name;

    public RenameDeviceDialog(SyncthingService service, string currentName)
        : base(
            "重命名本机",
            "该名称会显示在对方电脑的设备列表中。",
            "保存",
            new Size(500, 320))
    {
        _service = service;

        var label = Ui.Text("设备名称", 9.5F, Theme.TextSecondary);
        label.Location = new Point(0, 0);
        _name = Ui.Input("例如：家里的台式机");
        _name.Location = new Point(0, 20);
        _name.Width = Content.Width;
        _name.Text = currentName;
        _name.SelectAll();

        var hint = Ui.Text("最多 64 个字符。", 9F, Theme.TextMuted);
        hint.Location = new Point(0, 64);

        Content.Controls.Add(label);
        Content.Controls.Add(_name);
        Content.Controls.Add(hint);
    }

    protected override async Task OnSubmitAsync()
    {
        await _service.RenameLocalDeviceAsync(_name.Text);
        CloseWithSuccess();
    }
}
