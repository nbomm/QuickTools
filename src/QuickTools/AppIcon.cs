using System.Drawing.Drawing2D;
using QuickTools.Tray;

namespace QuickTools;

/// <summary>Draws the app's tray icon at runtime so the project needs no binary .ico asset.</summary>
internal static class AppIcon
{
    public static Icon Create()
    {
        using var bitmap = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.Clear(Color.Transparent);

            var bounds = new Rectangle(1, 1, 30, 30);
            using var background = RoundedRectangle(bounds, 7);
            using var fill = new LinearGradientBrush(bounds, Color.FromArgb(0, 120, 212), Color.FromArgb(0, 84, 166), 90f);
            g.FillPath(fill, background);

            Point[] bolt = [new(19, 3), new(8, 18), new(15, 18), new(12, 29), new(24, 13), new(17, 13), new(22, 3)];
            g.FillPolygon(Brushes.White, bolt);
        }

        IntPtr handle = bitmap.GetHicon();
        try
        {
            using var temporary = Icon.FromHandle(handle);
            return (Icon)temporary.Clone();
        }
        finally
        {
            NativeMethods.DestroyIcon(handle);
        }
    }

    private static GraphicsPath RoundedRectangle(Rectangle bounds, int radius)
    {
        int diameter = radius * 2;
        var path = new GraphicsPath();
        path.AddArc(bounds.Left, bounds.Top, diameter, diameter, 180, 90);
        path.AddArc(bounds.Right - diameter, bounds.Top, diameter, diameter, 270, 90);
        path.AddArc(bounds.Right - diameter, bounds.Bottom - diameter, diameter, diameter, 0, 90);
        path.AddArc(bounds.Left, bounds.Bottom - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();
        return path;
    }
}
