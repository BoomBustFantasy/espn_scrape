namespace ESPNScrape.Utils;

public static class NFLCalendar
{
    public static int GetCurrentSeason()
    {
        var now = DateTime.Now;
        var currentYear = now.Year;
        if (now.Month <= 7)
            return currentYear - 1;
        return currentYear;
    }

    public static int EstimateCurrentWeek(DateTime date)
    {
        if (date.Month == 9)
        {
            return Math.Max(1, (date.Day - 5) / 7); // Weeks 1-4 in September
        }
        else if (date.Month == 10)
        {
            // Late October 2025 - we're likely in week 8 or 9
            if (date.Day >= 26) return 9;  // Late October likely week 9
            if (date.Day >= 19) return 8;  // Mid-late October likely week 8
            return Math.Min(8, 4 + (date.Day / 7)); // Earlier October weeks 5-7
        }
        else if (date.Month == 11)
        {
            return Math.Min(12, 8 + (date.Day / 7)); // Weeks 9-12 in November
        }
        else if (date.Month == 12)
        {
            return Math.Min(17, 12 + (date.Day / 7)); // Weeks 13-17 in December
        }
        else if (date.Month == 1)
        {
            return 18; // Week 18 typically in January
        }

        return 7; // Default fallback
    }

    /// <summary>
    /// The given week plus the one before it, clamped to the regular season. Scanning a
    /// two-week window means games that finalise late — Monday night, or a stat correction
    /// a few days on — still get a second pass.
    /// </summary>
    public static List<int> WeekWindow(int week)
    {
        var endWeek = Math.Clamp(week, 1, 18);
        var startWeek = Math.Max(1, endWeek - 1);
        return Enumerable.Range(startWeek, endWeek - startWeek + 1).ToList();
    }

    public static List<int> GetWeeksToCheck(int season, DateTime date)
    {
        return WeekWindow(EstimateCurrentWeek(date));
    }
}
