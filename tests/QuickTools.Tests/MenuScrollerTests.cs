using QuickTools.Menus;

namespace QuickTools.Tests;

public class MenuScrollerTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(11, 1)]    // just under 0.6 s of hovering
    [InlineData(12, 2)]
    [InlineData(24, 3)]
    [InlineData(36, 4)]
    [InlineData(1000, 4)]  // capped, so a long hover can't skip past what the user is looking for
    public void HoveringAnArrowLongerScrollsFaster(int hoverTicks, int expected) =>
        Assert.Equal(expected, MenuScroller.ItemsPerTick(hoverTicks));

    [Theory]
    [InlineData(120, 3)]
    [InlineData(-120, 3)]
    [InlineData(240, 6)]
    [InlineData(30, 1)]    // precision touchpads send small deltas; each still moves one item
    [InlineData(-1, 1)]
    public void WheelScrollsThreeItemsPerNotch(int delta, int expected) =>
        Assert.Equal(expected, MenuScroller.ItemsForWheel(delta));
}
