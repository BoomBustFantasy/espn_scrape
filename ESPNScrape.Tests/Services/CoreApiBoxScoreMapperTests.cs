using System.Text.Json;
using ESPNScrape.Models;
using ESPNScrape.Services;

namespace ESPNScrape.Tests.Services;

public class CoreApiBoxScoreMapperTests
{
    // --------------- helpers ---------------

    private static StatisticsResponse BuildStats(params StatCategory[] categories)
        => new() { Splits = new StatSplits { Categories = [.. categories] } };

    private static StatCategory BuildCategory(string name, params (string statName, double value)[] stats)
        => new()
        {
            Name = name,
            Stats = [.. stats.Select(s => new StatEntry { Name = s.statName, Value = s.value })]
        };

    private static StatCategory BuildCategoryWithAthletes(string categoryName, params (string athleteId, string statsUrl)[] athletes)
        => new()
        {
            Name = categoryName,
            Athletes = [.. athletes.Select(a => new StatAthleteRef
            {
                Athlete = new AthletesReference { Ref = $"http://sports.core.api.espn.com/v2/.../athletes/{a.athleteId}?lang=en" },
                Statistics = new StatisticsReference { Ref = a.statsUrl }
            })]
        };

    // --------------- ExtractRelevantAthletes ---------------

    [Fact]
    public void ExtractRelevantAthletes_UnionsAcrossPassingRushingReceiving_Deduped()
    {
        var passing = BuildCategoryWithAthletes("passing", ("100", "url-100"));
        var rushing = BuildCategoryWithAthletes("rushing", ("100", "url-100"), ("200", "url-200"));
        var receiving = BuildCategoryWithAthletes("receiving", ("300", "url-300"));

        var teamStats = BuildStats(passing, rushing, receiving);

        var result = CoreApiBoxScoreMapper.ExtractRelevantAthletes(teamStats);

        Assert.Equal(3, result.Count);
        Assert.Contains(result, a => a.Athlete!.GetUrl().Contains("/athletes/100"));
        Assert.Contains(result, a => a.Athlete!.GetUrl().Contains("/athletes/200"));
        Assert.Contains(result, a => a.Athlete!.GetUrl().Contains("/athletes/300"));
    }

    [Fact]
    public void ExtractRelevantAthletes_IgnoresNonOffensiveCategories()
    {
        var defensive = BuildCategoryWithAthletes("defensive", ("999", "url-999"));

        var teamStats = BuildStats(defensive);

        var result = CoreApiBoxScoreMapper.ExtractRelevantAthletes(teamStats);

        Assert.Empty(result);
    }

    [Fact]
    public void ExtractRelevantAthletes_NoCategories_ReturnsEmpty()
    {
        var teamStats = new StatisticsResponse { Splits = null };

        var result = CoreApiBoxScoreMapper.ExtractRelevantAthletes(teamStats);

        Assert.Empty(result);
    }

    // --------------- ExtractAthleteId ---------------

    [Fact]
    public void ExtractAthleteId_ParsesTrailingNumericId()
    {
        var id = CoreApiBoxScoreMapper.ExtractAthleteId("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/2024/athletes/4428331?lang=en&region=us");

        Assert.Equal("4428331", id);
    }

    [Fact]
    public void ExtractAthleteId_NoMatch_ReturnsNull()
    {
        var id = CoreApiBoxScoreMapper.ExtractAthleteId("http://sports.core.api.espn.com/v2/sports/football/leagues/nfl/teams/12");

        Assert.Null(id);
    }

    // --------------- Map ---------------

    [Fact]
    public void Map_RushingOnlyPlayer_ReturnsRushingWithoutPassingOrReceiving()
    {
        var stats = BuildStats(BuildCategory("rushing",
            ("rushingAttempts", 22), ("rushingYards", 110), ("rushingTouchdowns", 1), ("longRushing", 30)));

        var result = CoreApiBoxScoreMapper.Map(stats, "rb-1", "cle-1", "Cleveland Browns", "Nick Chubb", "game-1",
            new DateTime(2024, 9, 8, 17, 0, 0, DateTimeKind.Utc), 2024, 1);

        Assert.NotNull(result.Rushing);
        Assert.Null(result.Passing);
        Assert.Null(result.Receiving);
    }

    [Fact]
    public void Map_PassingAndRushingCategories_ReturnsBothBlobs()
    {
        var stats = BuildStats(
            BuildCategory("passing", ("completions", 22), ("passingAttempts", 30), ("passingYards", 250)),
            BuildCategory("rushing", ("rushingAttempts", 8), ("rushingYards", 72)));

        var result = CoreApiBoxScoreMapper.Map(stats, "qb-1", "bal-1", "Baltimore Ravens", "Lamar Jackson", "game-2",
            new DateTime(2024, 10, 5, 17, 0, 0, DateTimeKind.Utc), 2024, 5);

        Assert.NotNull(result.Passing);
        Assert.NotNull(result.Rushing);
        Assert.Null(result.Receiving);
    }

    [Fact]
    public void Map_GeneralCategory_SetsFumblesAndFumblesLost()
    {
        var stats = BuildStats(
            BuildCategory("rushing", ("rushingAttempts", 20), ("rushingYards", 90)),
            BuildCategory("general", ("fumbles", 2), ("fumblesLost", 1)));

        var result = CoreApiBoxScoreMapper.Map(stats, "rb-2", "cle-1", "Cleveland Browns", "Nick Chubb", "game-3",
            new DateTime(2024, 9, 15, 17, 0, 0, DateTimeKind.Utc), 2024, 2);

        Assert.Equal(2, result.Fumbles);
        Assert.Equal(1, result.FumblesLost);
    }

    [Fact]
    public void Map_NoGeneralCategory_DefaultsFumblesToZero()
    {
        var stats = BuildStats(BuildCategory("receiving", ("receptions", 4), ("receivingYards", 45)));

        var result = CoreApiBoxScoreMapper.Map(stats, "wr-1", "det-1", "Detroit Lions", "Amon-Ra St. Brown", "game-4",
            new DateTime(2024, 9, 8, 17, 0, 0, DateTimeKind.Utc), 2024, 1);

        Assert.Equal(0, result.Fumbles);
        Assert.Equal(0, result.FumblesLost);
    }

    [Fact]
    public void Map_PassingStats_JsonBlobUsesTargetKeyNames()
    {
        var stats = BuildStats(BuildCategory("passing",
            ("completions", 28), ("passingAttempts", 40), ("passingYards", 350),
            ("passingTouchdowns", 4), ("interceptions", 1),
            ("yardsPerPassAttempt", 8.75), ("QBRating", 118.4), ("ESPNQBRating", 82.1)));

        var result = CoreApiBoxScoreMapper.Map(stats, "qb-42", "cin-1", "Cincinnati Bengals", "Joe Burrow", "game-5",
            new DateTime(2024, 11, 3, 20, 15, 0, DateTimeKind.Utc), 2024, 9);

        var json = result.Passing!.ToString()!;
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(28, root.GetProperty("completions").GetInt32());
        Assert.Equal(40, root.GetProperty("passingattempts").GetInt32());
        Assert.Equal(350, root.GetProperty("passingyards").GetInt32());
        Assert.Equal(4, root.GetProperty("passingtouchdowns").GetInt32());
        Assert.Equal(1, root.GetProperty("interceptions").GetInt32());
        Assert.Equal(8.75, root.GetProperty("yardsperpassattempt").GetDouble());
        Assert.Equal(118.4, root.GetProperty("qbrating").GetDouble());
        Assert.Equal(82.1, root.GetProperty("adjqbr").GetDouble());
    }

    [Fact]
    public void Map_ReceivingStats_JsonBlobUsesTargetKeyNames()
    {
        var stats = BuildStats(BuildCategory("receiving",
            ("receptions", 6), ("receivingYards", 100), ("yardsPerReception", 16.7),
            ("receivingTouchdowns", 1), ("longReception", 50), ("receivingTargets", 7)));

        var result = CoreApiBoxScoreMapper.Map(stats, "wr-2", "atl-1", "Atlanta Falcons", "Drake London", "game-6",
            new DateTime(2024, 9, 8, 17, 0, 0, DateTimeKind.Utc), 2024, 1);

        var json = result.Receiving!.ToString()!;
        var root = JsonDocument.Parse(json).RootElement;

        Assert.Equal(6, root.GetProperty("receptions").GetInt32());
        Assert.Equal(100, root.GetProperty("receivingyards").GetInt32());
        Assert.Equal(7, root.GetProperty("receivingtargets").GetInt32());
    }

    [Fact]
    public void Map_SetsGameMetadata_FromArguments()
    {
        var gameDate = new DateTime(2024, 11, 3, 20, 15, 0, DateTimeKind.Utc);
        var stats = BuildStats(BuildCategory("rushing", ("rushingAttempts", 25), ("rushingYards", 120)));

        var result = CoreApiBoxScoreMapper.Map(stats, "rb-99", "dal-1", "Dallas Cowboys", "Derrick Henry", "game-xyz",
            gameDate, 2024, 9);

        Assert.Equal("rb-99", result.EspnPlayerId);
        Assert.Equal("dal-1", result.EspnTeamId);
        Assert.Equal("Dallas Cowboys", result.TeamDisplayName);
        Assert.Equal("Derrick Henry", result.Name);
        Assert.Equal("game-xyz", result.EspnGameId);
        Assert.Equal(gameDate, result.GameDate);
        Assert.Equal(2024, result.Season);
        Assert.Equal(9, result.Week);
    }

    [Fact]
    public void Map_EmptyCategory_ReturnsNullBlobNotEmptyObject()
    {
        var stats = BuildStats(BuildCategory("passing", ("totalPoints", 6)));

        var result = CoreApiBoxScoreMapper.Map(stats, "p-1", "kc-1", "Kansas City Chiefs", "Patrick Mahomes", "game-7",
            new DateTime(2024, 9, 8, 17, 0, 0, DateTimeKind.Utc), 2024, 1);

        Assert.Null(result.Passing);
    }
}
