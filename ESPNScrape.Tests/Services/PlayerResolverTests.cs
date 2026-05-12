using ESPNScrape.Models.Supa;
using ESPNScrape.Services;

namespace ESPNScrape.Tests.Services;

public class PlayerResolverTests
{
    // --------------- helpers ---------------

    private static Player MakePlayer(long id, string firstName, string lastName, long teamId, string? espnPlayerId = null)
        => new()
        {
            Id = id,
            FirstName = firstName,
            LastName = lastName,
            TeamId = teamId,
            EspnPlayerId = espnPlayerId
        };

    // ESPN team ID "12" maps to Supabase team ID 16 (Kansas City Chiefs)
    private const string KcEspnTeamId = "12";
    private const long KcSupabaseTeamId = 16;

    // ESPN team ID "1" maps to Supabase team ID 2 (Atlanta Falcons)
    private const string AtlEspnTeamId = "1";
    private const long AtlSupabaseTeamId = 2;

    // --------------- ESPN ID fast path ---------------

    [Fact]
    public void Resolve_MatchingEspnPlayerId_ReturnsPlayerSupabaseId()
    {
        var players = new List<Player>
        {
            MakePlayer(100, "Patrick", "Mahomes", KcSupabaseTeamId, espnPlayerId: "3139477")
        };

        var result = PlayerResolver.Resolve(players, "3139477", KcEspnTeamId, "Patrick Mahomes");

        Assert.Equal(100L, result);
    }

    [Fact]
    public void Resolve_EspnIdMatchTakesPrecedenceOverNameMatch()
    {
        // Two players: one matched by ESPN ID, another with the same display name
        var players = new List<Player>
        {
            MakePlayer(200, "Patrick", "Mahomes", KcSupabaseTeamId, espnPlayerId: "3139477"),
            MakePlayer(201, "Patrick", "Mahomes", KcSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "3139477", KcEspnTeamId, "Patrick Mahomes");

        Assert.Equal(200L, result);
    }

    // --------------- Name-match fallback ---------------

    [Fact]
    public void Resolve_NoEspnIdMatch_FallsBackToNameAndTeamMatch()
    {
        var players = new List<Player>
        {
            MakePlayer(300, "Travis", "Kelce", KcSupabaseTeamId, espnPlayerId: null)
        };

        // Unknown ESPN ID, but name + team should resolve
        var result = PlayerResolver.Resolve(players, "unknown-id", KcEspnTeamId, "Travis Kelce");

        Assert.Equal(300L, result);
    }

    [Fact]
    public void Resolve_SuffixDifference_StillMatches()
    {
        var players = new List<Player>
        {
            MakePlayer(400, "Calvin", "Ridley", AtlSupabaseTeamId, espnPlayerId: null)
        };

        // ESPN display name includes "Jr." suffix
        var result = PlayerResolver.Resolve(players, "no-id", AtlEspnTeamId, "Calvin Ridley Jr.");

        Assert.Equal(400L, result);
    }

    [Fact]
    public void Resolve_PeriodInName_StillMatches()
    {
        // DB has "St Brown" (no period), ESPN has "Amon-Ra St. Brown"
        // After normalization: ESPN first = "AMON-RA", last = "BROWN"... 
        // Actually, let's test a simpler period-stripping case: "D.J. Moore"
        var players = new List<Player>
        {
            MakePlayer(500, "DJ", "Moore", AtlSupabaseTeamId, espnPlayerId: null)
        };

        // ESPN name has periods stripped during normalization
        var result = PlayerResolver.Resolve(players, "no-id", AtlEspnTeamId, "D.J. Moore");

        Assert.Equal(500L, result);
    }

    [Fact]
    public void Resolve_PrefixFirstNameMatch_ReturnsPlayer()
    {
        // DB has "Pat" but ESPN has "Patrick" — prefix match should work
        var players = new List<Player>
        {
            MakePlayer(600, "Pat", "Mahomes", KcSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", KcEspnTeamId, "Patrick Mahomes");

        Assert.Equal(600L, result);
    }

    // --------------- Ambiguity guard ---------------

    [Fact]
    public void Resolve_TwoPlayersWithSameNormalizedName_ReturnsNull()
    {
        var players = new List<Player>
        {
            MakePlayer(700, "Justin", "Johnson", KcSupabaseTeamId, espnPlayerId: null),
            MakePlayer(701, "Justin", "Johnson", KcSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", KcEspnTeamId, "Justin Johnson");

        Assert.Null(result);
    }

    // --------------- Edge cases ---------------

    [Fact]
    public void Resolve_EmptyPlayerList_ReturnsNull()
    {
        var result = PlayerResolver.Resolve([], "3139477", KcEspnTeamId, "Patrick Mahomes");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_UnknownEspnTeamId_ReturnsNull()
    {
        var players = new List<Player>
        {
            MakePlayer(800, "Unknown", "Player", 999, espnPlayerId: null)
        };

        // "999" is not a valid ESPN team ID in ESPNTeamMapper
        var result = PlayerResolver.Resolve(players, "no-id", "999", "Unknown Player");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_EmptyDisplayName_ReturnsNull()
    {
        var players = new List<Player>
        {
            MakePlayer(900, "Patrick", "Mahomes", KcSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", KcEspnTeamId, "");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_SingleTokenDisplayName_ReturnsNull()
    {
        var players = new List<Player>
        {
            MakePlayer(901, "Mahomes", "", KcSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", KcEspnTeamId, "Mahomes");

        Assert.Null(result);
    }

    [Fact]
    public void Resolve_PlayerOnWrongTeam_DoesNotMatch()
    {
        var players = new List<Player>
        {
            // Player is on Atlanta (team 2), searching with KC ESPN team ID
            MakePlayer(1000, "Patrick", "Mahomes", AtlSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", KcEspnTeamId, "Patrick Mahomes");

        Assert.Null(result);
    }

    // --------------- NormalizeName (internal, tested via Resolve) ---------------

    [Fact]
    public void Resolve_NormalizationStripsJrSuffix()
    {
        var players = new List<Player>
        {
            MakePlayer(1100, "Odell", "Beckham", AtlSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", AtlEspnTeamId, "Odell Beckham Jr");

        Assert.Equal(1100L, result);
    }

    [Fact]
    public void Resolve_NormalizationIsCaseInsensitive()
    {
        var players = new List<Player>
        {
            MakePlayer(1200, "TRAVIS", "KELCE", KcSupabaseTeamId, espnPlayerId: null)
        };

        var result = PlayerResolver.Resolve(players, "no-id", KcEspnTeamId, "travis kelce");

        Assert.Equal(1200L, result);
    }
}
