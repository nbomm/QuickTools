using System.Drawing;
using QuickTools.Menus;

namespace QuickTools.Tests;

public class MenuPlacementTests
{
    private static readonly Size Menu = new(200, 300);

    private static void AssertInside(Rectangle workArea, Point location, Size size) =>
        Assert.True(workArea.Contains(new Rectangle(location, size)), $"{new Rectangle(location, size)} is outside {workArea}");

    [Fact]
    public void IconInsideWorkArea_PutsBottomRightCornerOnIconTopLeft()
    {
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var icon = new Rectangle(1700, 1000, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(1700 - 200, 1000 - 300), location);
    }

    [Fact]
    public void BottomTaskbar_MenuStopsAtTaskbarEdgeInsteadOfCoveringIt()
    {
        // Tray icons sit inside the taskbar, i.e. below the work area, so the menu's bottom edge
        // lands on the taskbar's top edge rather than exactly on the icon's top.
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var icon = new Rectangle(1700, 1044, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(1700 - 200, 1040 - 300), location);
        AssertInside(workArea, location, Menu);
    }

    [Fact]
    public void IconNearLeftEdge_FlipsToRightOfIcon()
    {
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var icon = new Rectangle(50, 1044, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(82, 740), location);
        AssertInside(workArea, location, Menu);
    }

    [Fact]
    public void TopTaskbar_FlipsBelowIcon()
    {
        var workArea = new Rectangle(0, 40, 1920, 1040);
        var icon = new Rectangle(1700, 4, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(1500, 40), location);
        AssertInside(workArea, location, Menu);
    }

    [Fact]
    public void LeftTaskbar_FlipsRightOfIconAndStaysInWorkArea()
    {
        var workArea = new Rectangle(60, 0, 1860, 1080);
        var icon = new Rectangle(14, 1000, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(60, 700), location);
        AssertInside(workArea, location, Menu);
    }

    [Fact]
    public void RightTaskbar_MenuStopsAtTaskbarEdge()
    {
        var workArea = new Rectangle(0, 0, 1860, 1080);
        var icon = new Rectangle(1874, 1000, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(1860 - 200, 700), location);
        AssertInside(workArea, location, Menu);
    }

    [Fact]
    public void IconInOverflowFlyout_UsesPreferredPlacement()
    {
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var icon = new Rectangle(1600, 950, 32, 32);

        Assert.Equal(new Point(1400, 650), MenuPlacement.Calculate(icon, Menu, workArea));
    }

    [Fact]
    public void SecondaryMonitorWithNegativeCoordinates()
    {
        var workArea = new Rectangle(-1920, 0, 1920, 1040);
        var icon = new Rectangle(-300, 1044, 32, 32);

        var location = MenuPlacement.Calculate(icon, Menu, workArea);

        Assert.Equal(new Point(-500, 740), location);
        AssertInside(workArea, location, Menu);
    }

    [Fact]
    public void MenuTallerThanWorkArea_PinsToTop()
    {
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var icon = new Rectangle(1700, 1044, 32, 32);
        var tallMenu = new Size(200, 3000);

        var location = MenuPlacement.Calculate(icon, tallMenu, workArea);

        Assert.Equal(new Point(1500, 0), location);
    }

    [Fact]
    public void MenuWiderThanWorkArea_PinsToLeft()
    {
        var workArea = new Rectangle(0, 0, 800, 600);
        var icon = new Rectangle(700, 604, 32, 32);

        Assert.Equal(0, MenuPlacement.Calculate(icon, new Size(1000, 300), workArea).X);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1900, 0)]
    [InlineData(0, 1030)]
    [InlineData(1900, 1030)]
    [InlineData(960, 520)]
    public void AnyAnchorInsideWorkArea_KeepsMenuFullyVisible(int x, int y)
    {
        var workArea = new Rectangle(0, 0, 1920, 1040);
        var icon = new Rectangle(x, y, 1, 1);

        AssertInside(workArea, MenuPlacement.Calculate(icon, Menu, workArea), Menu);
    }
}
