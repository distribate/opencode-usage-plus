using OpenCodeUsagePlus.Core.Utilities;
using Xunit;

namespace OpenCodeUsagePlus.Tests;

public sealed class FlyoutPositionerTests
{
    private static readonly PixelRect BottomTaskbarWorkArea = new(0, 0, 1920, 1040);

    [Fact]
    public void Opens_above_a_bottom_taskbar_and_clamps_to_the_right_edge()
    {
        var (x, y) = FlyoutPositioner.Place(300, 360, cursorX: 1800, cursorY: 1035, BottomTaskbarWorkArea);

        Assert.Equal(1612, x);
        Assert.Equal(672, y);
    }

    [Fact]
    public void Opens_below_a_top_taskbar()
    {
        var workArea = new PixelRect(0, 40, 1920, 1000);
        var (x, y) = FlyoutPositioner.Place(300, 360, cursorX: 500, cursorY: 45, workArea);

        Assert.Equal(350, x);
        Assert.Equal(48, y);
    }

    [Fact]
    public void Uses_the_left_edge_of_a_left_taskbar()
    {
        var workArea = new PixelRect(60, 0, 1860, 1080);
        var (x, y) = FlyoutPositioner.Place(300, 360, cursorX: 70, cursorY: 20, workArea);

        Assert.Equal(68, x);
        Assert.Equal(8, y);
    }

    [Fact]
    public void Never_leaves_the_working_area_on_a_small_screen()
    {
        var workArea = new PixelRect(0, 0, 250, 600);
        var (x, y) = FlyoutPositioner.Place(300, 500, cursorX: 125, cursorY: 300, workArea);

        Assert.Equal(8, x);
        Assert.Equal(50, y);
    }

    [Fact]
    public void Centres_on_a_floating_click()
    {
        var (x, y) = FlyoutPositioner.Place(300, 200, cursorX: 960, cursorY: 500, BottomTaskbarWorkArea);

        Assert.Equal(810, x);
        Assert.Equal(400, y);
    }
}
