using System.Drawing.Drawing2D;
using System.Reflection;

namespace CcDock.UI;

public static class Theme
{
    public static readonly Color PageBackground = ColorTranslator.FromHtml("#F4F6FA");
    public static readonly Color SidebarBackground = ColorTranslator.FromHtml("#111827");
    public static readonly Color SidebarHover = ColorTranslator.FromHtml("#1F2937");
    public static readonly Color SidebarActive = ColorTranslator.FromHtml("#2563EB");
    public static readonly Color SidebarText = ColorTranslator.FromHtml("#D1D5DB");
    public static readonly Color SidebarMuted = ColorTranslator.FromHtml("#9CA3AF");
    // Brighter variants keep status dots and error text legible on #111827.
    public static readonly Color SidebarSuccess = ColorTranslator.FromHtml("#34D399");
    public static readonly Color SidebarWarning = ColorTranslator.FromHtml("#F5B84D");
    public static readonly Color SidebarDanger = ColorTranslator.FromHtml("#F97066");
    public static readonly Color Card = Color.White;
    public static readonly Color CardBorder = ColorTranslator.FromHtml("#E4E9F2");
    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#111827");
    // Secondary and muted text need at least 4.5:1 contrast on the light card
    // background (#FFFFFF) to stay readable at small point sizes.
    public static readonly Color TextSecondary = ColorTranslator.FromHtml("#475467");
    public static readonly Color TextMuted = ColorTranslator.FromHtml("#667085");
    public static readonly Color Primary = ColorTranslator.FromHtml("#2563EB");
    public static readonly Color PrimaryHover = ColorTranslator.FromHtml("#1D4ED8");
    public static readonly Color PrimaryPressed = ColorTranslator.FromHtml("#1E40AF");
    public static readonly Color PrimarySoft = ColorTranslator.FromHtml("#EFF4FF");
    public static readonly Color Success = ColorTranslator.FromHtml("#0B7A4B");
    public static readonly Color SuccessSoft = ColorTranslator.FromHtml("#E9F9F1");
    public static readonly Color Warning = ColorTranslator.FromHtml("#B45309");
    public static readonly Color WarningSoft = ColorTranslator.FromHtml("#FFF7E6");
    public static readonly Color Danger = ColorTranslator.FromHtml("#D92D20");
    public static readonly Color DangerSoft = ColorTranslator.FromHtml("#FEF1F0");
    public static readonly Color Divider = ColorTranslator.FromHtml("#EAECF0");
    public static readonly Color InputBackground = ColorTranslator.FromHtml("#FBFCFE");
    public static readonly Color InputBorder = ColorTranslator.FromHtml("#D6DCE8");

    public static Font UIFont(float size, FontStyle style = FontStyle.Regular)
        => new("Microsoft YaHei UI", size, style, GraphicsUnit.Point);

    public static Font MonoFont(float size, FontStyle style = FontStyle.Regular)
        => new("Consolas", size, style, GraphicsUnit.Point);

    public static readonly Font IconFont = new("Segoe MDL2 Assets", 12F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font IconFontLarge = new("Segoe MDL2 Assets", 20F, FontStyle.Regular, GraphicsUnit.Point);
    public static readonly Font IconFontSmall = new("Segoe MDL2 Assets", 9F, FontStyle.Regular, GraphicsUnit.Point);

    public static Icon AppIcon
    {
        get
        {
            var assembly = Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("CcDock.app.ico");
            return stream is null ? SystemIcons.Application : new Icon(stream);
        }
    }

    public static GraphicsPath RoundedRect(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        if (bounds.Width <= 0 || bounds.Height <= 0)
        {
            return path;
        }

        var diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1)
        {
            path.AddRectangle(bounds);
            return path;
        }

        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    public static void ConfigureForm(Form form)
    {
        form.Font = UIFont(10.5F);
        form.BackColor = PageBackground;
        form.ForeColor = TextPrimary;
        form.Icon = AppIcon;
        form.AutoScaleMode = AutoScaleMode.Dpi;
    }
}

public static class Glyphs
{
    public const string Add = "";
    public const string Folder = "";
    public const string OpenFolder = "";
    public const string Sync = "";
    public const string Download = "";
    public const string Settings = "";
    public const string Home = "";
    public const string Copy = "";
    public const string Refresh = "";
    public const string Delete = "";
    public const string Check = "";
    public const string Warning = "";
    public const string Info = "";
    public const string Link = "";
    public const string Power = "";
    public const string ChevronRight = "";
    public const string Device = "";
    public const string Shield = "";
    public const string Clock = "";
    public const string History = "";
}
