using System.Drawing.Drawing2D;

namespace CcDock.UI.Controls;

public class ThinProgressBar : Control
{
    private double _value;

    public ThinProgressBar()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint |
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.UserPaint |
            ControlStyles.SupportsTransparentBackColor,
            true);
        BackColor = Color.Transparent;
        Height = 8;
        TrackColor = ColorTranslator.FromHtml("#EDF0F5");
        BarColor = Theme.Primary;
    }

    public double Value
    {
        get => _value;
        set
        {
            _value = Math.Clamp(value, 0, 100);
            Invalidate();
        }
    }

    public Color TrackColor { get; set; }

    public Color BarColor { get; set; }

    public int CornerRadius { get; set; } = 4;

    protected override void OnPaint(PaintEventArgs e)
    {
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        using (var trackPath = Theme.RoundedRect(new Rectangle(0, 0, Width, Height), CornerRadius))
        using (var trackBrush = new SolidBrush(TrackColor))
        {
            e.Graphics.FillPath(trackBrush, trackPath);
        }

        var width = (int)Math.Round(Width * _value / 100.0);
        if (width <= 1)
        {
            return;
        }

        using var barPath = Theme.RoundedRect(new Rectangle(0, 0, width, Height), CornerRadius);
        using var barBrush = new SolidBrush(BarColor);
        e.Graphics.FillPath(barBrush, barPath);
    }
}
