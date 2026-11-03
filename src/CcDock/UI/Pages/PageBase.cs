using CcDock.Syncthing;
using CcDock.UI.Controls;

namespace CcDock.UI.Pages;

public abstract class PageBase : UserControl
{
    private readonly Label _title;
    private readonly Label _subtitle;
    private readonly FlowLayoutPanel _actions;
    private readonly Panel _header;
    private readonly BufferedPanel _scroll;
    protected readonly TableLayoutPanel Body;

    public virtual bool AutoRefresh => false;

    protected MainForm Host { get; }

    protected SyncthingService Service => Host.Service;

    protected PageBase(MainForm host, string title, string subtitle)
    {
        Host = host;
        Dock = DockStyle.Fill;
        BackColor = Theme.PageBackground;

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = Theme.PageBackground,
            Margin = Padding.Empty
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 100F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        Controls.Add(root);

        _header = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Theme.PageBackground,
            Margin = Padding.Empty
        };
        root.Controls.Add(_header, 0, 0);

        _title = Ui.Text(title, 20F, Theme.TextPrimary, FontStyle.Bold);
        _title.Location = new Point(28, 18);
        _subtitle = Ui.Text(subtitle, 10.5F, Theme.TextSecondary);
        _subtitle.AutoSize = false;
        _subtitle.AutoEllipsis = true;
        _subtitle.Location = new Point(29, 56);
        _subtitle.Height = 26;

        _actions = new FlowLayoutPanel
        {
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false,
            BackColor = Color.Transparent,
            Margin = Padding.Empty
        };

        _header.Controls.Add(_title);
        _header.Controls.Add(_subtitle);
        _header.Controls.Add(_actions);
        _header.Resize += (_, _) => LayoutHeader();
        LayoutHeader();

        _scroll = new BufferedPanel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            BackColor = Theme.PageBackground,
            Margin = Padding.Empty,
            Padding = Padding.Empty
        };
        root.Controls.Add(_scroll, 0, 1);

        Body = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            AutoSizeMode = AutoSizeMode.GrowAndShrink,
            ColumnCount = 1,
            RowCount = 0,
            BackColor = Color.Transparent,
            Padding = new Padding(28, 4, 28, 24),
            Margin = Padding.Empty
        };
        Body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        _scroll.Controls.Add(Body);
    }

    public abstract Task RefreshAsync();

    protected void AddAction(Control control)
    {
        control.Margin = new Padding(0, 0, 8, 0);
        if (control is RoundedButton button)
        {
            button.BackColor = Theme.PageBackground;
        }
        _actions.Controls.Add(control);
        LayoutHeader();
    }

    protected void AddRow(Control card, int height, int gap = 16)
    {
        card.Dock = DockStyle.Fill;
        card.Margin = new Padding(0, 0, 0, gap);
        Body.RowStyles.Add(new RowStyle(SizeType.Absolute, height + gap));
        Body.RowCount = Body.RowStyles.Count;
        Body.Controls.Add(card, 0, Body.RowCount - 1);
    }

    protected void AddPlainRow(Control control, int height, int gap = 8)
    {
        control.Dock = DockStyle.Fill;
        control.Margin = new Padding(0, 0, 0, gap);
        Body.RowStyles.Add(new RowStyle(SizeType.Absolute, height + gap));
        Body.RowCount = Body.RowStyles.Count;
        Body.Controls.Add(control, 0, Body.RowCount - 1);
    }

    /// <summary>
    /// Adds a control that was created after the form finished its initial
    /// DPI scaling. The control itself is scaled, and the row height is
    /// converted from design units to device pixels.
    /// </summary>
    protected void AddScaledRow(Control card, int logicalHeight, int logicalGap = 16)
    {
        Ui.ScaleToDpi(card);
        AddRow(card, Ui.Px(Body, logicalHeight), Ui.Px(Body, logicalGap));
    }

    protected void AddScaledPlainRow(Control control, int logicalHeight, int logicalGap = 8)
    {
        Ui.ScaleToDpi(control);
        AddPlainRow(control, Ui.Px(Body, logicalHeight), Ui.Px(Body, logicalGap));
    }

    protected void ClearBody()
    {
        Body.SuspendLayout();
        foreach (Control control in Body.Controls.Cast<Control>().ToArray())
        {
            control.Dispose();
        }
        Body.Controls.Clear();
        Body.RowStyles.Clear();
        Body.RowCount = 0;
        Body.ResumeLayout();
    }

    protected RoundedPanel BuildMessageCard(
        string glyph, string title, string message, string? buttonText = null, EventHandler? onClick = null)
    {
        var card = Ui.Card();
        var glyphLabel = Ui.Text(glyph, 22F, Theme.Primary);
        glyphLabel.Location = new Point(24, 22);
        var titleLabel = Ui.Text(title, 13F, Theme.TextPrimary, FontStyle.Bold);
        titleLabel.Location = new Point(66, 24);
        var messageLabel = Ui.Text(message, 10F, Theme.TextSecondary);
        messageLabel.AutoSize = false;
        messageLabel.AutoEllipsis = true;
        messageLabel.Location = new Point(26, 66);
        messageLabel.Height = 26;
        card.Controls.Add(glyphLabel);
        card.Controls.Add(titleLabel);
        card.Controls.Add(messageLabel);

        if (buttonText is not null)
        {
            var button = Ui.Button(buttonText, ButtonKind.Primary, null, 132);
            button.Location = new Point(26, 102);
            if (onClick is not null)
            {
                button.Click += onClick;
            }
            card.Controls.Add(button);
        }

        card.Resize += (_, _) =>
        {
            messageLabel.Width = Math.Max(
                Ui.Px(card, 200), card.ClientSize.Width - Ui.Px(card, 52));
        };
        return card;
    }

    private void LayoutHeader()
    {
        var x = Math.Max(Ui.Px(_header, 28), _header.ClientSize.Width - _actions.Width - Ui.Px(_header, 24));
        _actions.Location = new Point(x, Ui.Px(_header, 28));

        var subtitleWidth = Math.Max(
            Ui.Px(_header, 160),
            _actions.Left - Ui.Px(_header, 29) - Ui.Px(_header, 16));
        _subtitle.SetBounds(
            Ui.Px(_header, 29),
            Ui.Px(_header, 56),
            subtitleWidth,
            Ui.Px(_header, 24));
    }
}
