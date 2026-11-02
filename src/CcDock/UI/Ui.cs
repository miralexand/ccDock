using System.Diagnostics;
using CcDock.UI.Controls;

namespace CcDock.UI;

internal static class Ui
{
    public static int Px(Control control, int logical)
        => (int)Math.Round(logical * control.DeviceDpi / 96.0);

    public static float DpiFactor(Control control) => control.DeviceDpi / 96F;

    public static void ScaleToDpi(Control control)
    {
        var factor = DpiFactor(control);
        if (factor > 1.001F)
        {
            control.Scale(new SizeF(factor, factor));
        }
    }

    public static Label Text(
        string text, float size = 10F, Color? color = null, FontStyle style = FontStyle.Regular)
        => new()
        {
            Text = text,
            AutoSize = true,
            Font = Theme.UIFont(size, style),
            ForeColor = color ?? Theme.TextPrimary,
            BackColor = Color.Transparent,
            UseCompatibleTextRendering = false,
            Margin = Padding.Empty
        };

    public static Label Wrap(
        string text, int width, float size = 10F, Color? color = null, FontStyle style = FontStyle.Regular)
    {
        var label = Text(text, size, color, style);
        label.MaximumSize = new Size(Math.Max(40, width), 0);
        return label;
    }

    public static RoundedPanel Card()
        => new()
        {
            Dock = DockStyle.Fill,
            FillColor = Theme.Card,
            BorderColor = Theme.CardBorder,
            CornerRadius = 14,
            Padding = Padding.Empty
        };

    public static RoundedButton Button(
        string text, ButtonKind kind = ButtonKind.Primary, string? glyph = null, int width = 112)
        => new()
        {
            Text = text,
            Kind = kind,
            Glyph = glyph,
            Width = width,
            Height = 38,
            Margin = new Padding(0, 0, 8, 0)
        };

    public static TextBox Input(string? placeholder = null, bool multiline = false)
    {
        var box = new TextBox
        {
            Font = Theme.UIFont(10F),
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = Theme.InputBackground,
            ForeColor = Theme.TextPrimary,
            AutoSize = false,
            Multiline = multiline,
            Height = multiline ? 84 : 34,
            ScrollBars = multiline ? ScrollBars.Vertical : ScrollBars.None
        };
        if (!string.IsNullOrEmpty(placeholder))
        {
            box.PlaceholderText = placeholder;
        }
        return box;
    }

    public static ComboBox Combo()
        => new()
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            FlatStyle = FlatStyle.Flat,
            Font = Theme.UIFont(10F),
            BackColor = Theme.Card,
            ForeColor = Theme.TextPrimary,
            Height = 32
        };

    public static CheckBox Check(string text)
        => new()
        {
            Text = text,
            AutoSize = true,
            Font = Theme.UIFont(10F),
            ForeColor = Theme.TextPrimary,
            BackColor = Color.Transparent,
            Cursor = Cursors.Hand,
            Margin = Padding.Empty
        };

    public static Panel ActionRow(
        string title,
        string subtitle,
        params (string Text, ButtonKind Kind, EventHandler Click)[] actions)
    {
        var row = new Panel
        {
            Height = 58,
            Dock = DockStyle.Fill,
            BackColor = Color.Transparent,
            Margin = new Padding(0, 0, 0, 2)
        };

        // Rows are built at runtime, after the window has already been
        // DPI-scaled, so all metrics are converted to device pixels here.
        row.Height = Px(row, 58);

        var titleLabel = Text(title, 10F, Theme.TextPrimary, FontStyle.Bold);
        titleLabel.AutoSize = false;
        titleLabel.AutoEllipsis = true;

        var subtitleLabel = Text(subtitle, 9F, Theme.TextMuted);
        subtitleLabel.AutoSize = false;
        subtitleLabel.AutoEllipsis = true;

        row.Controls.Add(titleLabel);
        row.Controls.Add(subtitleLabel);

        var buttons = new List<RoundedButton>(actions.Length);
        foreach (var action in actions)
        {
            var button = Button(action.Text, action.Kind, null, 78);
            button.Width = Px(row, 78);
            button.Height = Px(row, 30);
            button.Click += action.Click;
            buttons.Add(button);
            row.Controls.Add(button);
        }

        row.Resize += (_, _) =>
        {
            var buttonArea = buttons.Count * Px(row, 78) + Math.Max(0, buttons.Count - 1) * Px(row, 6);
            var textWidth = Math.Max(Px(row, 60), row.ClientSize.Width - buttonArea - Px(row, 24));
            titleLabel.SetBounds(Px(row, 8), Px(row, 6), textWidth, Px(row, 20));
            subtitleLabel.SetBounds(Px(row, 8), Px(row, 28), textWidth, Px(row, 18));
            var x = row.ClientSize.Width - Px(row, 8) - buttonArea;
            foreach (var button in buttons)
            {
                button.Location = new Point(x, Px(row, 14));
                x += button.Width + Px(row, 6);
            }
        };

        return row;
    }

    public static void StyleDialog(Form form)
    {
        form.FormBorderStyle = FormBorderStyle.FixedDialog;
        form.MaximizeBox = false;
        form.MinimizeBox = false;
        form.ShowInTaskbar = false;
        form.StartPosition = FormStartPosition.CenterParent;
        form.BackColor = Theme.PageBackground;
        form.ForeColor = Theme.TextPrimary;
        form.Font = Theme.UIFont(10F);
        form.AutoScaleMode = AutoScaleMode.Dpi;
        form.AutoScaleDimensions = new SizeF(96F, 96F);
        form.Icon = Theme.AppIcon;
    }

    public static string FormatBytes(long bytes)
    {
        if (bytes <= 0)
        {
            return "0 B";
        }

        string[] units = { "B", "KB", "MB", "GB", "TB" };
        var value = (double)bytes;
        var unit = 0;
        while (value >= 1024 && unit < units.Length - 1)
        {
            value /= 1024;
            unit++;
        }

        return unit == 0 ? $"{value:0} {units[unit]}" : $"{value:0.#} {units[unit]}";
    }

    public static bool TryOpen(string path)
    {
        try
        {
            if (!Directory.Exists(path) && !File.Exists(path))
            {
                return false;
            }

            Process.Start(new ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true
            });
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static string SafeFileName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = value.Trim().Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        var result = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(result) ? "共享文件夹" : result;
    }
}
