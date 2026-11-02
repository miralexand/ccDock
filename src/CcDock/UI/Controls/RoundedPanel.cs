using System.Drawing.Drawing2D;

namespace CcDock.UI.Controls;

public class RoundedPanel : Panel
{
    private int _cornerRadius = 14;

    public RoundedPanel()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        FillColor = Theme.Card;
        BorderColor = Theme.CardBorder;
        Padding = new Padding(20);
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
    }

    public Color FillColor { get; set; }

    public Color BorderColor { get; set; }

    public int CornerRadius
    {
        get => _cornerRadius;
        set
        {
            _cornerRadius = Math.Max(0, value);
            Invalidate();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        var bounds = new Rectangle(0, 0, Width - 1, Height - 1);
        using var path = Theme.RoundedRect(bounds, Ui.Px(this, _cornerRadius));
        using var fill = new SolidBrush(FillColor);
        e.Graphics.FillPath(fill, path);

        if (BorderColor.A > 0)
        {
            using var pen = new Pen(BorderColor, 1f);
            e.Graphics.DrawPath(pen, path);
        }

        base.OnPaint(e);
    }
}
