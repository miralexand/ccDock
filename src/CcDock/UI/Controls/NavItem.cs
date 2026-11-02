using System.Drawing.Drawing2D;

namespace CcDock.UI.Controls;

public class NavItem : Control
{
    private bool _hover;
    private bool _active;

    public NavItem()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        SetStyle(ControlStyles.Selectable, false);
        BackColor = Theme.SidebarBackground;
        ForeColor = Theme.SidebarText;
        Font = Theme.UIFont(10.5F);
        Height = 46;
        Cursor = Cursors.Hand;
    }

    public string Key { get; set; } = string.Empty;

    public string Glyph { get; set; } = string.Empty;

    public bool Active
    {
        get => _active;
        set
        {
            _active = value;
            Invalidate();
        }
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new SolidBrush(BackColor))
        {
            e.Graphics.FillRectangle(background, ClientRectangle);
        }

        var bounds = new Rectangle(0, 0, Width, Height);
        if (Active || _hover)
        {
            using var path = Theme.RoundedRect(
                new Rectangle(0, 0, Width - 1, Height - 1), Ui.Px(this, 10));
            using var brush = new SolidBrush(Active ? Theme.SidebarActive : Theme.SidebarHover);
            e.Graphics.FillPath(brush, path);
        }

        var fore = Active || _hover ? Color.White : Theme.SidebarText;
        var flags = TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix;
        var glyphSize = TextRenderer.MeasureText(
            Glyph, Theme.IconFont, new Size(int.MaxValue, int.MaxValue), flags);
        var glyphBounds = new Rectangle(Ui.Px(this, 16), 0, glyphSize.Width, Height);
        TextRenderer.DrawText(e.Graphics, Glyph, Theme.IconFont, glyphBounds, fore, flags);

        var textBounds = new Rectangle(
            Ui.Px(this, 48), 0, Math.Max(0, Width - Ui.Px(this, 56)), Height);
        TextRenderer.DrawText(
            e.Graphics,
            Text,
            Font,
            textBounds,
            fore,
            flags | TextFormatFlags.EndEllipsis);
    }
}
