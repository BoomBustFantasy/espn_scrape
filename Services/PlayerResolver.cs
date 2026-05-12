using System.Text.RegularExpressions;
using ESPNScrape.Models.Supa;

namespace ESPNScrape.Services;

public static class PlayerResolver
{
    /// <summary>
    /// Resolves an ESPN player to a Supabase player ID.
    ///
    /// Resolution order:
    ///   1. ESPN ID fast path — find the first player where EspnPlayerId == espnPlayerId.
    ///   2. Team-scoped name match — filter by Supabase team, fuzzy-match on normalised display name.
    ///   3. Ambiguity guard — if >1 candidate survives name matching, return null.
    ///
    /// Never writes to the database. Same inputs always produce the same output.
    /// </summary>
    /// <param name="allPlayers">The full in-memory player list for the job run.</param>
    /// <param name="espnPlayerId">The ESPN player ID string from the box score.</param>
    /// <param name="espnTeamId">The ESPN team ID string for the player's team.</param>
    /// <param name="displayName">The ESPN display name (e.g. "Patrick Mahomes") used for the name-match fallback.</param>
    /// <returns>The Supabase player Id, or null if unresolvable.</returns>
    public static long? Resolve(
        IReadOnlyList<Player> allPlayers,
        string espnPlayerId,
        string espnTeamId,
        string displayName = "")
    {
        // Step 1 — ESPN ID fast path
        if (!string.IsNullOrEmpty(espnPlayerId))
        {
            var byId = allPlayers.FirstOrDefault(p => p.EspnPlayerId == espnPlayerId);
            if (byId != null)
                return byId.Id;
        }

        // Step 2 — Team-scoped name match
        if (string.IsNullOrEmpty(displayName))
            return null;

        var supabaseTeamId = ESPNTeamMapper.MapEspnIdToSupabaseId(espnTeamId);
        if (!supabaseTeamId.HasValue)
            return null;

        var teamPlayers = allPlayers.Where(p => p.TeamId == supabaseTeamId.Value).ToList();
        if (teamPlayers.Count == 0)
            return null;

        // Normalise the full display name first (strips suffixes, periods, etc.) then split into tokens.
        var normDisplay = NormalizeName(displayName);
        var tokens = normDisplay.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length < 2)
            return null;

        var espnFirstNorm = tokens[0];
        var espnLastNorm = tokens[^1]; // last token after suffix stripping

        // Exact match
        var exactMatches = teamPlayers
            .Where(p =>
                NormalizeName(p.FirstName) == espnFirstNorm &&
                NormalizeName(p.LastName) == espnLastNorm)
            .ToList();

        if (exactMatches.Count == 1)
            return exactMatches[0].Id;
        if (exactMatches.Count > 1)
            return null; // ambiguity guard

        // Prefix match on first name with exact last name
        var fuzzyMatches = teamPlayers
            .Where(p =>
            {
                var dbFirst = NormalizeName(p.FirstName);
                var dbLast = NormalizeName(p.LastName);
                if (dbLast != espnLastNorm)
                    return false;
                return dbFirst.StartsWith(espnFirstNorm, StringComparison.Ordinal)
                    || espnFirstNorm.StartsWith(dbFirst, StringComparison.Ordinal);
            })
            .ToList();

        if (fuzzyMatches.Count == 1)
            return fuzzyMatches[0].Id;

        return null; // ambiguity or no match
    }

    internal static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name))
            return string.Empty;

        var normalized = name.Trim().ToUpperInvariant();
        normalized = normalized.Replace(".", "");
        normalized = Regex.Replace(normalized, @"\s+", " ");
        normalized = Regex.Replace(normalized, @"\s+(JR\.?|SR\.?|III|IV|II)$", string.Empty, RegexOptions.IgnoreCase);

        return normalized.Trim();
    }
}
