using System.Text.Json;
using ESPNScrape.Models;
using ESPNScrape.Services;

namespace ESPNScrape.Tests.Services;

public class BoxScoreParserTests
{
    // --------------- helpers ---------------

    private static GameSummary BuildSummary(params TeamPlayerStats[] teams)
        => new()
        {
            Id = "game-1",
            Date = new DateTime(2025, 10, 20, 20, 0, 0, DateTimeKind.Utc),
            BoxScore = new GameBoxScore { Players = [.. teams] }
        };

    private static TeamPlayerStats BuildTeamStats(string teamId, string teamDisplayName, params PlayerStatCategory[] categories)
        => new()
        {
            Team = new TeamBasicInfo { Id = teamId, DisplayName = teamDisplayName },
            Statistics = [.. categories]
        };

    private static PlayerStatCategory BuildCategory(string name, string[] keys, params (string playerId, string playerName, string[] stats)[] players)
        => new()
        {
            Name = name,
            Keys = [.. keys],
            Labels = [.. keys],
            Athletes = players.Select(p => new PlayerStats
            {
                Athlete = new PlayerInfo { Id = p.playerId, DisplayName = p.playerName },
                Stats = [.. p.stats]
            }).ToList()
        };

    // --------------- tests ---------------

    [Fact]
    public void Parse_NullSummary_ReturnsEmpty()
    {
        var result = BoxScoreParser.Parse(null!, 2025, 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_NullBoxScore_ReturnsEmpty()
    {
        var summary = new GameSummary { Id = "g1", BoxScore = null };

        var result = BoxScoreParser.Parse(summary, 2025, 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_EmptyPlayers_ReturnsEmpty()
    {
        var summary = new GameSummary
        {
            Id = "g1",
            BoxScore = new GameBoxScore { Players = [] }
        };

        var result = BoxScoreParser.Parse(summary, 2025, 1);

        Assert.Empty(result);
    }

    [Fact]
    public void Parse_PlayerWithOnlyPassingStats_ReturnsOneRecordWithPassingOnly()
    {
        var category = BuildCategory("passing",
            ["completions/passingAttempts", "passingyards", "td"],
            ("qb-1", "Patrick Mahomes", ["25/35", "312", "3"]));

        var summary = BuildSummary(BuildTeamStats("kc-1", "Kansas City Chiefs", category));

        var result = BoxScoreParser.Parse(summary, 2025, 1);

        Assert.Single(result);
        var stat = result[0];
        Assert.Equal("qb-1", stat.EspnPlayerId);
        Assert.NotNull(stat.Passing);
        Assert.Null(stat.Rushing);
        Assert.Null(stat.Receiving);
    }

    [Fact]
    public void Parse_PlayerInPassingAndRushingCategories_ReturnsSingleMergedRecord()
    {
        var passing = BuildCategory("passing",
            ["completions/passingAttempts", "passingyards"],
            ("qb-1", "Lamar Jackson", ["22/30", "250"]));

        var rushing = BuildCategory("rushing",
            ["car", "yds"],
            ("qb-1", "Lamar Jackson", ["8", "72"]));

        var summary = BuildSummary(BuildTeamStats("bal-1", "Baltimore Ravens", passing, rushing));

        var result = BoxScoreParser.Parse(summary, 2025, 5);

        Assert.Single(result);
        var stat = result[0];
        Assert.Equal("qb-1", stat.EspnPlayerId);
        Assert.NotNull(stat.Passing);
        Assert.NotNull(stat.Rushing);
        Assert.Null(stat.Receiving);
        Assert.Equal(2025, stat.Season);
        Assert.Equal(5, stat.Week);
    }

    [Fact]
    public void Parse_TwoTeams_EachPlayerCarriesCorrectEspnTeamId()
    {
        var homeCategory = BuildCategory("rushing",
            ["car", "yds"],
            ("rb-home", "Home RB", ["20", "100"]));

        var awayCategory = BuildCategory("rushing",
            ["car", "yds"],
            ("rb-away", "Away RB", ["15", "80"]));

        var summary = BuildSummary(
            BuildTeamStats("home-team-1", "Home Team", homeCategory),
            BuildTeamStats("away-team-2", "Away Team", awayCategory));

        var result = BoxScoreParser.Parse(summary, 2025, 3);

        Assert.Equal(2, result.Count);

        var homePlayer = result.Single(r => r.EspnPlayerId == "rb-home");
        Assert.Equal("home-team-1", homePlayer.EspnTeamId);
        Assert.Equal("Home Team", homePlayer.TeamDisplayName);

        var awayPlayer = result.Single(r => r.EspnPlayerId == "rb-away");
        Assert.Equal("away-team-2", awayPlayer.EspnTeamId);
        Assert.Equal("Away Team", awayPlayer.TeamDisplayName);
    }

    [Fact]
    public void Parse_FumbleCategory_SetsFumblesAndFumblesLostOnCorrectPlayer()
    {
        var rushing = BuildCategory("rushing",
            ["car", "yds"],
            ("rb-1", "Nick Chubb", ["22", "110"]));

        var fumbles = BuildCategory("fumbles",
            ["fum", "lost"],
            ("rb-1", "Nick Chubb", ["2", "1"]));

        var summary = BuildSummary(BuildTeamStats("cle-1", "Cleveland Browns", rushing, fumbles));

        var result = BoxScoreParser.Parse(summary, 2025, 7);

        Assert.Single(result);
        var stat = result[0];
        Assert.Equal(2, stat.Fumbles);
        Assert.Equal(1, stat.FumblesLost);
    }

    [Fact]
    public void Parse_PassingStats_JsonBlobContainsExpectedFields()
    {
        var category = BuildCategory("passing",
            ["completions/passingAttempts", "passingyards", "td", "int"],
            ("qb-42", "Joe Burrow", ["28/40", "350", "4", "1"]));

        var summary = BuildSummary(BuildTeamStats("cin-1", "Cincinnati Bengals", category));

        var result = BoxScoreParser.Parse(summary, 2025, 9);

        Assert.Single(result);
        var stat = result[0];
        Assert.NotNull(stat.Passing);

        var json = stat.Passing!.ToString()!;
        var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        Assert.Equal(28, root.GetProperty("completions").GetInt32());
        Assert.Equal(40, root.GetProperty("passingattempts").GetInt32());
        Assert.Equal(350, root.GetProperty("passingyards").GetInt32());
        Assert.Equal(4, root.GetProperty("passingtouchdowns").GetInt32());
        Assert.Equal(1, root.GetProperty("interceptions").GetInt32());
    }

    [Fact]
    public void Parse_SetsGameMetadata_FromSummaryAndArguments()
    {
        var gameDate = new DateTime(2025, 11, 3, 20, 15, 0, DateTimeKind.Utc);
        var category = BuildCategory("rushing",
            ["car", "yds"],
            ("rb-99", "Derrick Henry", ["25", "120"]));

        var summary = new GameSummary
        {
            Id = "game-xyz",
            Date = gameDate,
            BoxScore = new GameBoxScore
            {
                Players = [BuildTeamStats("dal-1", "Dallas Cowboys", category)]
            }
        };

        var result = BoxScoreParser.Parse(summary, 2025, 9);

        Assert.Single(result);
        var stat = result[0];
        Assert.Equal("game-xyz", stat.EspnGameId);
        Assert.Equal(gameDate, stat.GameDate);
        Assert.Equal(2025, stat.Season);
        Assert.Equal(9, stat.Week);
    }
}
