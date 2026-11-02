namespace CcDock.UI.Controls;

/// <summary>
/// A flicker-free scroll container for pages that rebuild their content.
/// </summary>
public class BufferedPanel : Panel
{
    public BufferedPanel()
    {
        SetStyle(
            ControlStyles.OptimizedDoubleBuffer |
            ControlStyles.AllPaintingInWmPaint,
            true);
        UpdateStyles();
    }
}
