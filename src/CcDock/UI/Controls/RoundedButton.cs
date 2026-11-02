using System.Drawing.Drawing2D;

namespace CcDock.UI.Controls;

public enum ButtonKind
{
    Primary,
    Secondary,
    Ghost,
    Danger
}

public class RoundedButton : Button
{
    private bool _hover;
    private bool _pressed;

    public RoundedButton()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = Theme.Card;
        ForeColor = Color.White;
        Font = Theme.UIFont(10F);
        Cursor = Cursors.Hand;
        Height = 40;
        Kind = ButtonKind.Primary;
    }

    public ButtonKind Kind
    {
        get;
        set;
    } = ButtonKind.Primary;

    public string? Glyph { get; set; }

    public int CornerRadius { get; set; } = 9;

    protected override void OnMouseEnter(EventArgs e)
    {
        _hover = true;
        Invalidate();
        base.OnMouseEnter(e);
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        _hover = false;
        _pressed = false;
        Invalidate();
        base.OnMouseLeave(e);
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        _pressed = true;
        Invalidate();
        base.OnMouseDown(mevent);
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        _pressed = false;
        Invalidate();
        base.OnMouseUp(mevent);
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        Invalidate();
        base.OnEnabledChanged(e);
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var background = new SolidBrush(BackColor))
        {
            pevent.Graphics.FillRectangle(background, ClientRectangle);
        }

        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundedRect(bounds, Ui.Px(this, CornerRadius));

        var (fill, text, border) = ResolveColors();
        using (var brush = new SolidBrush(fill))
        {
            pevent.Graphics.FillPath(brush, path);
        }

        if (border.A > 0)
        {
            using var pen = new Pen(border, 1f);
            pevent.Graphics.DrawPath(pen, path);
        }

        var textColor = Enabled ? text : Color.FromArgb(120, text);
        var flags = TextFormatFlags.VerticalCenter |
                    TextFormatFlags.SingleLine |
                    TextFormatFlags.NoPadding |
                    TextFormatFlags.NoPrefix;
        var textSize = TextRenderer.MeasureText(Text, Font, new Size(int.MaxValue, int.MaxValue), flags);
        var glyphSize = string.IsNullOrEmpty(Glyph)
            ? Size.Empty
            : TextRenderer.MeasureText(Glyph, Theme.IconFont, new Size(int.MaxValue, int.MaxValue), flags);
        var gap = Ui.Px(this, 7);
        var totalWidth = glyphSize.Width + (string.IsNullOrEmpty(Glyph) ? 0 : gap) + textSize.Width;
        var x = Math.Max(Ui.Px(this, 6), (Width - totalWidth) / 2);
        var contentHeight = Height;

        if (!string.IsNullOrEmpty(Glyph))
        {
            var glyphBounds = new Rectangle(x, 0, glyphSize.Width, contentHeight);
            TextRenderer.DrawText(pevent.Graphics, Glyph, Theme.IconFont, glyphBounds, textColor, flags);
            x += glyphSize.Width + gap;
        }

        var textBounds = new Rectangle(x, 0, Math.Max(0, Width - x), contentHeight);
        TextRenderer.DrawText(pevent.Graphics, Text, Font, textBounds, textColor, flags);

        if (Focused && ShowFocusCues)
        {
            var focusBounds = Rectangle.Inflate(bounds, -4, -4);
            ControlPaint.DrawFocusRectangle(pevent.Graphics, focusBounds, textColor, fill);
        }
    }

    private (Color Fill, Color Text, Color Border) ResolveColors()
    {
        var disabled = !Enabled;
        return Kind switch
        {
            ButtonKind.Primary => (
                disabled ? ColorTranslator.FromHtml("#AFC5F3") : _pressed ? Theme.PrimaryPressed : _hover ? Theme.PrimaryHover : Theme.Primary,
                Color.White,
                Color.Transparent),
            ButtonKind.Secondary => (
                disabled ? Theme.InputBackground : _pressed ? ColorTranslator.FromHtml("#EEF2F8") : _hover ? ColorTranslator.FromHtml("#F8FAFD") : Theme.Card,
                disabled ? Theme.TextMuted : Theme.TextPrimary,
                Theme.InputBorder),
            ButtonKind.Ghost => (
                disabled ? Color.Transparent : _pressed ? ColorTranslator.FromHtml("#EEF2F8") : _hover ? Theme.PrimarySoft : Color.Transparent,
                disabled ? Theme.TextMuted : Theme.Primary,
                Color.Transparent),
            ButtonKind.Danger => (
                disabled ? ColorTranslator.FromHtml("#F3B7B2") : _pressed ? ColorTranslator.FromHtml("#B42318") : _hover ? ColorTranslator.FromHtml("#C9271B") : Theme.Danger,
                Color.White,
                Color.Transparent),
            _ => (Theme.Card, Theme.TextPrimary, Theme.CardBorder)
        };
    }
}
