using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Dialogs;

public sealed class AddDeviceDialog : DialogBase
{
    private readonly SyncthingService _service;
    private readonly TextBox _deviceId;

    public AddDeviceDialog(SyncthingService service)
        : base(
            "添加设备",
            "输入对方电脑的设备 ID，配对后即可建立同步连接。",
            "添加",
            new Size(520, 380))
    {
        _service = service;

        var label = Ui.Text("对方的设备 ID", 10F, Theme.TextSecondary);
        label.Location = new Point(0, 0);
        _deviceId = Ui.Input(
            "XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX-XXXXXXX",
            multiline: true);
        _deviceId.Location = new Point(0, 20);
        _deviceId.Width = Content.Width;
        _deviceId.Height = 82;

        var hint = Ui.Wrap(
            "在对方电脑打开 ccDock，进入“设备”页面，点击“复制 ID”后粘贴到这里。\n" +
            "设备 ID 共 56 位字符，可分为 8 组，每组 7 位。",
            Content.Width, 9.5F, Theme.TextMuted);
        hint.Location = new Point(0, 116);

        Content.Controls.Add(label);
        Content.Controls.Add(_deviceId);
        Content.Controls.Add(hint);
    }

    protected override async Task OnSubmitAsync()
    {
        if (!DeviceIdValidator.TryNormalize(_deviceId.Text, out var deviceId, out var error))
        {
            throw new SyncthingException(error ?? "设备 ID 无效。");
        }

        await _service.AcceptPendingDeviceAsync(deviceId);
        CloseWithSuccess();
    }
}
