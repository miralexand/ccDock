using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Dialogs;

public sealed class DeviceAddressDialog : DialogBase
{
    private readonly SyncthingService _service;
    private readonly DeviceInfo _device;
    private readonly TextBox _addresses;

    public DeviceAddressDialog(SyncthingService service, DeviceInfo device)
        : base(
            "连接设置",
            $"为“{device.Name}”指定连接地址。跨网段或经过 NAT 时，静态地址比自动发现更可靠。",
            "保存",
            new Size(580, 560))
    {
        _service = service;
        _device = device;

        var label = Ui.Text("连接地址（每行一个）", 10F, Theme.TextSecondary);
        label.Location = new Point(0, 0);

        _addresses = Ui.Input(multiline: true);
        _addresses.Location = new Point(0, 22);
        _addresses.Width = Content.Width;
        _addresses.Height = 150;
        _addresses.Text = string.Join(
            Environment.NewLine,
            device.Addresses.Count == 0 ? new[] { "dynamic" } : device.Addresses);

        var reset = Ui.Button("恢复自动发现", ButtonKind.Ghost, null, 132);
        reset.Height = 32;
        reset.Location = new Point(0, 186);
        reset.Click += (_, _) => _addresses.Text = "dynamic";

        var hint = Ui.Wrap(
            "dynamic：由 Syncthing 自动发现地址（默认）。\n" +
            "tcp://192.168.1.50:22000：直连指定 IP。若对方在另一网段的下游，请在“下游设备”上填写上游设备的局域网地址。\n" +
            "quic://192.168.1.50:22000：使用 QUIC 直连；relay://… 为中继地址。\n" +
            "防火墙需要放行 TCP/UDP 22000，上游设备无需主动访问下游设备。",
            Content.Width, 9.5F, Theme.TextMuted);
        hint.Location = new Point(0, 238);

        var id = Ui.Text("设备 ID：" + device.DeviceId, 9.25F, Theme.TextMuted);
        id.AutoSize = false;
        id.AutoEllipsis = true;
        id.Location = new Point(0, 344);
        id.Height = 20;
        id.Width = Content.Width;
        id.Font = Theme.MonoFont(9.25F);

        Content.Controls.Add(label);
        Content.Controls.Add(_addresses);
        Content.Controls.Add(reset);
        Content.Controls.Add(hint);
        Content.Controls.Add(id);
    }

    protected override async Task OnSubmitAsync()
    {
        var lines = _addresses.Text
            .Replace("\r\n", "\n")
            .Replace('\r', '\n')
            .Split('\n')
            .Select(line => line.Trim())
            .Where(line => line.Length > 0)
            .ToArray();

        await _service.SetDeviceAddressesAsync(_device.DeviceId, lines);
        CloseWithSuccess();
    }
}
