using ESPNScrape.Utils;
using Xunit;

namespace ESPNScrape.Tests.Utils;

public class NFLCalendarTests
{
    [Theory]
    [InlineData(1, new[] { 1 })]           // no week 0 to look back at
    [InlineData(2, new[] { 1, 2 })]
    [InlineData(5, new[] { 4, 5 })]
    [InlineData(18, new[] { 17, 18 })]
    public void WeekWindow_ReturnsWeekAndThePrecedingOne(int week, int[] expected)
    {
        Assert.Equal(expected, NFLCalendar.WeekWindow(week));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-3)]
    [InlineData(19)]
    [InlineData(25)]
    public void WeekWindow_ClampsOutOfRangeWeeksInsteadOfThrowing(int week)
    {
        // ESPN should never report these for the regular season, but a bad value
        // must not take the job down with an ArgumentOutOfRangeException.
        var window = NFLCalendar.WeekWindow(week);

        Assert.NotEmpty(window);
        Assert.All(window, w => Assert.InRange(w, 1, 18));
    }
}
