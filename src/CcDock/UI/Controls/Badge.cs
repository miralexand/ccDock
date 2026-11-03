using System.Drawing.Drawing2D;

namespace CcDock.UI.Controls;

public class Badge : Control
{
    public Badge()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Font = Theme.UIFont(9F, FontStyle.Bold);
        Height = 22;
        Width = 64;
        Text = string.Empty;
        FillColor = Theme.PrimarySoft;
        TextColor = Theme.Primary;
    }

    public Color FillColor { get; set; }

    public Color TextColor { get; set; }

    public int CornerRadius { get; set; } = 8;

    public void Apply(string text, Color fill, Color fore)
    {
        Text = text;
        FillColor = fill;
        TextColor = fore;
        RecalculateSize();
        Invalidate();
    }

    private void RecalculateSize()
    {
        var size = TextRenderer.MeasureText(
            Text,
            Font,
            new Size(int.MaxValue, int.MaxValue),
            TextFormatFlags.NoPadding | TextFormatFlags.SingleLine);
        Width = Math.Max(Ui.Px(this, 30), size.Width + Ui.Px(this, 18));
        Height = Ui.Px(this, 22);
    }

    protected override void ScaleControl(SizeF factor, BoundsSpecified specified)
    {
        base.ScaleControl(factor, specified);
        // The caption is measured in device pixels, so a scaled width would
        // leave the pill far wider than its text on high-DPI displays.
        RecalculateSize();
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        pevent.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundedRect(bounds, Ui.Px(this, CornerRadius));
        using (var brush = new SolidBrush(FillColor))
        {
            pevent.Graphics.FillPath(brush, path);
        }

        TextRenderer.DrawText(
            pevent.Graphics,
            Text,
            Font,
            bounds,
            TextColor,
            TextFormatFlags.HorizontalCenter |
            TextFormatFlags.VerticalCenter |
            TextFormatFlags.SingleLine |
            TextFormatFlags.NoPadding);
    }
}
