namespace QuickTools.Menus;

/// <summary>
/// Pure placement math, kept free of Win32 calls so every taskbar edge and screen edge can be unit tested.
/// </summary>
internal static class MenuPlacement
{
    /// <summary>
    /// Returns the top-left screen location for a menu so that it is fully inside <paramref name="workArea"/>.
    /// Preferred placement puts the menu's bottom-right corner on the icon's top-left corner; it flips to the
    /// icon's right or bottom side when that would leave the work area (taskbar on the left or top), then
    /// clamps as a final guarantee.
    /// </summary>
    public static Point Calculate(Rectangle iconBounds, Size menuSize, Rectangle workArea)
    {
        int x = iconBounds.Left - menuSize.Width;
        if (x < workArea.Left)
        {
            x = iconBounds.Right;
        }

        int y = iconBounds.Top - menuSize.Height;
        if (y < workArea.Top)
        {
            y = iconBounds.Bottom;
        }

        // When the menu is larger than the work area, pin it to the top/left; WinForms adds scroll arrows.
        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - menuSize.Width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - menuSize.Height));
        return new Point(x, y);
    }

    /// <summary>
    /// Returns the top-left screen location for a menu opened at <paramref name="point"/>, like a context menu:
    /// its top-left corner on the point, flipping to the left and/or above the point where it would run off
    /// <paramref name="workArea"/>, then clamping as a final guarantee.
    /// </summary>
    public static Point CalculateAtPoint(Point point, Size menuSize, Rectangle workArea)
    {
        int x = point.X + menuSize.Width > workArea.Right ? point.X - menuSize.Width : point.X;
        int y = point.Y + menuSize.Height > workArea.Bottom ? point.Y - menuSize.Height : point.Y;

        x = Math.Clamp(x, workArea.Left, Math.Max(workArea.Left, workArea.Right - menuSize.Width));
        y = Math.Clamp(y, workArea.Top, Math.Max(workArea.Top, workArea.Bottom - menuSize.Height));
        return new Point(x, y);
    }
}
