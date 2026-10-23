using System.Reflection;

namespace QuickTools.Menus;

/// <summary>
/// Makes a long menu (or any long submenu of it) easy to scroll. WinForms shows scroll arrows on a menu taller than
/// the screen, but they only work while the mouse button is held down, and the wheel does nothing. This scrolls
/// while the pointer rests on an arrow (faster the longer it stays) and scrolls with the wheel.
/// </summary>
/// <remarks>
/// WinForms has no public way to scroll a drop-down, so this calls its internal scroll method. If that method
/// isn't found (a future .NET changed it) or fails, this does nothing and the arrows keep their normal behavior.
/// </remarks>
internal sealed class MenuScroller : IMessageFilter, IDisposable
{
    private const int WM_MOUSEWHEEL = 0x020A;
    private const int WheelDeltaPerNotch = 120;
    private const int ItemsPerWheelNotch = 3;
    private const int TickMilliseconds = 50;

    // Hovering for this many ticks (about 0.6 s) speeds the scrolling up one step.
    private const int TicksPerSpeedStep = 12;
    private const int MaxItemsPerTick = 4;

    private readonly ContextMenuStrip _menu;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = TickMilliseconds };
    private ToolStripDropDownMenu? _hoverMenu;
    private bool _hoverUp;
    private int _hoverTicks;
    private bool _running;

    public MenuScroller(ContextMenuStrip menu)
    {
        _menu = menu;
        _timer.Tick += (_, _) => OnTick();
        menu.Opened += (_, _) => Start();
        menu.Closed += (_, _) => Stop();
        menu.Disposed += (_, _) => Dispose();
    }

    /// <summary>How many items to scroll on a tick when the pointer has rested on an arrow for <paramref name="hoverTicks"/> ticks.</summary>
    public static int ItemsPerTick(int hoverTicks) => Math.Min(1 + hoverTicks / TicksPerSpeedStep, MaxItemsPerTick);

    /// <summary>How many items one wheel message scrolls; always at least one, even for the small deltas of precision touchpads.</summary>
    public static int ItemsForWheel(int delta) => Math.Max(1, Math.Abs(delta) * ItemsPerWheelNotch / WheelDeltaPerNotch);

    public void Dispose()
    {
        Stop();
        _timer.Dispose();
    }

    bool IMessageFilter.PreFilterMessage(ref Message m)
    {
        if (m.Msg != WM_MOUSEWHEEL)
        {
            return false;
        }

        int delta = (short)(((long)m.WParam >> 16) & 0xFFFF);
        if (delta == 0 || FindDropDownAt(_menu, Cursor.Position) is not { } dropDown || !DropDownScroll.NeedsScrolling(dropDown))
        {
            return false;
        }

        // A positive delta is the wheel rolled away from the user, which scrolls up.
        DropDownScroll.Scroll(dropDown, up: delta > 0, ItemsForWheel(delta));
        return true;
    }

    private void Start()
    {
        if (_running || !DropDownScroll.IsSupported)
        {
            return;
        }

        _running = true;
        ResetHover();
        Application.AddMessageFilter(this);
        _timer.Start();
    }

    private void Stop()
    {
        if (!_running)
        {
            return;
        }

        _running = false;
        _timer.Stop();
        Application.RemoveMessageFilter(this);
    }

    private void OnTick()
    {
        Point cursor = Cursor.Position;
        if (FindDropDownAt(_menu, cursor) is not { } dropDown ||
            !DropDownScroll.TryGetArrowAt(dropDown, cursor, out bool up))
        {
            ResetHover();
            return;
        }

        // Moving to the other arrow, or to another submenu, starts slowly again.
        if (dropDown != _hoverMenu || up != _hoverUp)
        {
            _hoverMenu = dropDown;
            _hoverUp = up;
            _hoverTicks = 0;
        }

        DropDownScroll.Scroll(dropDown, up, ItemsPerTick(_hoverTicks++));
    }

    private void ResetHover()
    {
        _hoverMenu = null;
        _hoverTicks = 0;
    }

    /// <summary>The innermost open drop-down that contains the screen point, or null.</summary>
    private static ToolStripDropDownMenu? FindDropDownAt(ToolStripDropDown dropDown, Point screenPoint)
    {
        if (!dropDown.Visible)
        {
            return null;
        }

        foreach (ToolStripItem item in dropDown.Items)
        {
            if (item is ToolStripMenuItem { HasDropDownItems: true } menuItem &&
                FindDropDownAt(menuItem.DropDown, screenPoint) is { } inner)
            {
                return inner;
            }
        }

        return dropDown is ToolStripDropDownMenu menu && menu.Bounds.Contains(screenPoint) ? menu : null;
    }

    /// <summary>The reflection on WinForms internals, kept in one place so a failure disables only scrolling.</summary>
    private static class DropDownScroll
    {
        private const BindingFlags Members = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private static readonly MethodInfo? s_scroll =
            typeof(ToolStripDropDownMenu).GetMethod("ScrollInternal", Members, [typeof(bool)]);

        private static readonly PropertyInfo? s_requiresButtons =
            typeof(ToolStripDropDownMenu).GetProperty("RequiresScrollButtons", Members);

        private static readonly PropertyInfo? s_upButton =
            typeof(ToolStripDropDownMenu).GetProperty("UpScrollButton", Members);

        private static readonly PropertyInfo? s_downButton =
            typeof(ToolStripDropDownMenu).GetProperty("DownScrollButton", Members);

        private static bool s_failed;

        public static bool IsSupported =>
            !s_failed && s_scroll is not null && s_requiresButtons?.PropertyType == typeof(bool)
            && s_upButton is not null && s_downButton is not null;

        /// <summary>True if the drop-down is too tall for the screen, so it shows scroll arrows.</summary>
        public static bool NeedsScrolling(ToolStripDropDownMenu dropDown) =>
            IsSupported && Try(() => (bool)s_requiresButtons!.GetValue(dropDown)!);

        /// <summary>True if the screen point is over one of the drop-down's scroll arrows; <paramref name="up"/> says which.</summary>
        public static bool TryGetArrowAt(ToolStripDropDownMenu dropDown, Point screenPoint, out bool up)
        {
            bool isUp = false;
            bool found = NeedsScrolling(dropDown) && Try(() =>
            {
                if (ButtonContains(dropDown, s_upButton!, screenPoint))
                {
                    isUp = true;
                    return true;
                }

                return ButtonContains(dropDown, s_downButton!, screenPoint);
            });

            up = isUp;
            return found;
        }

        /// <summary>Scrolls by <paramref name="items"/> items, the same step the arrows take.</summary>
        public static void Scroll(ToolStripDropDownMenu dropDown, bool up, int items)
        {
            Try(() =>
            {
                for (int i = 0; i < items; i++)
                {
                    s_scroll!.Invoke(dropDown, [up]);
                }

                return true;
            });
        }

        private static bool ButtonContains(ToolStripDropDownMenu dropDown, PropertyInfo button, Point screenPoint) =>
            button.GetValue(dropDown) is ToolStripItem { Visible: true } item &&
            dropDown.RectangleToScreen(item.Bounds).Contains(screenPoint);

        private static bool Try(Func<bool> action)
        {
            try
            {
                return action();
            }
            catch (Exception ex) when (ex is TargetInvocationException or InvalidCastException or NullReferenceException)
            {
                // WinForms changed under us; stop trying and leave the built-in behavior.
                s_failed = true;
                return false;
            }
        }
    }
}
