using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Dialogs;

public abstract class DialogBase : Form
{
    private readonly Label _error;
    private bool _busy;

    protected readonly RoundedButton OkAction;
    protected readonly RoundedButton CancelAction;
    protected readonly Panel Content;

    protected DialogBase(string title, string description, string okText, Size size)
    {
        Text = title;
        Ui.StyleDialog(this);
        ClientSize = size;
        BackColor = Theme.PageBackground;

        var heading = Ui.Text(title, 16F, Theme.TextPrimary, FontStyle.Bold);
        heading.Location = new Point(28, 22);
        Controls.Add(heading);

        var caption = Ui.Wrap(description, size.Width - 56, 10F, Theme.TextSecondary);
        caption.Location = new Point(28, 56);
        Controls.Add(caption);

        Content = new Panel
        {
            Location = new Point(28, 104),
            Size = new Size(size.Width - 56, size.Height - 104 - 88),
            BackColor = Color.Transparent
        };
        Controls.Add(Content);

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 76,
            BackColor = Theme.Card
        };
        var line = new Panel
        {
            Dock = DockStyle.Top,
            Height = 1,
            BackColor = Theme.Divider
        };
        footer.Controls.Add(line);

        _error = Ui.Text(string.Empty, 9.5F, Theme.Danger);
        _error.AutoSize = false;
        _error.AutoEllipsis = true;
        _error.Location = new Point(28, 27);
        _error.Size = new Size(size.Width - 340, 34);
        _error.Visible = false;
        footer.Controls.Add(_error);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 280,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            BackColor = Color.Transparent,
            Padding = new Padding(0, 18, 20, 0)
        };
        OkAction = Ui.Button(okText, ButtonKind.Primary, null, 118);
        OkAction.Click += async (_, _) => await SubmitAsync();
        CancelAction = Ui.Button("取消", ButtonKind.Secondary, null, 90);
        CancelAction.Click += (_, _) => DialogResult = DialogResult.Cancel;
        buttons.Controls.Add(OkAction);
        buttons.Controls.Add(CancelAction);
        footer.Controls.Add(buttons);
        Controls.Add(footer);

        AcceptButton = OkAction;
        CancelButton = CancelAction;
        ActiveControl = OkAction;
    }

    protected override void OnLoad(EventArgs e)
    {
        // All fields and buttons exist by now; scale the complete dialog in
        // one pass (see the note in MainForm's constructor).
        base.OnLoad(e);
        AutoScaleDimensions = new SizeF(96F, 96F);
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);

        // CenterParent positions the dialog before OnLoad scaled it, which
        // pushes a high-DPI dialog below the visible area. Re-center once the
        // final size is known.
        if (Owner is { } owner)
        {
            var area = owner.RectangleToScreen(owner.ClientRectangle);
            Location = new Point(
                area.Left + (area.Width - Width) / 2,
                area.Top + (area.Height - Height) / 2);
        }
    }

    protected abstract Task OnSubmitAsync();

    protected void CloseWithSuccess()
    {
        DialogResult = DialogResult.OK;
        Close();
    }

    protected void ShowError(string message)
    {
        _error.Text = message;
        _error.Visible = true;
    }

    private async Task SubmitAsync()
    {
        if (_busy)
        {
            return;
        }

        _busy = true;
        _error.Visible = false;
        OkAction.Enabled = false;
        CancelAction.Enabled = false;
        UseWaitCursor = true;
        try
        {
            await OnSubmitAsync();
        }
        catch (Exception ex)
        {
            ShowError(ex is SyncthingException ? ex.Message : "操作失败：" + ex.Message);
        }
        finally
        {
            _busy = false;
            OkAction.Enabled = true;
            CancelAction.Enabled = true;
            UseWaitCursor = false;
        }
    }
}
