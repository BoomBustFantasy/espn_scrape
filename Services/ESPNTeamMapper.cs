using ESPNScrape.Models;
using ESPNScrape.Models.Supa;

namespace ESPNScrape.Services;

/// <summary>
/// Provides mapping between ESPN API team data and Supabase database team records
/// </summary>
public static class ESPNTeamMapper
{
    /// <summary>
    /// Maps ESPN team abbreviations to Supabase database abbreviations
    /// ESPN uses different abbreviations for some teams
    /// </summary>
    private static readonly Dictionary<string, string> EspnToSupabaseAbbreviations = new()
    {
        // Most teams match exactly, but these need mapping:
        { "GB", "GNB" },     // Green Bay Packers
        { "KC", "KAN" },     // Kansas City Chiefs  
        { "LV", "LVR" },     // Las Vegas Raiders
        { "NE", "NWE" },     // New England Patriots
        { "NO", "NOR" },     // New Orleans Saints
        { "SF", "SFO" },     // San Francisco 49ers
        { "TB", "TAM" },     // Tampa Bay Buccaneers
        
        // These should match but including for completeness:
        { "ARI", "ARI" },    // Arizona Cardinals
        { "ATL", "ATL" },    // Atlanta Falcons  
        { "BAL", "BAL" },    // Baltimore Ravens
        { "BUF", "BUF" },    // Buffalo Bills
        { "CAR", "CAR" },    // Carolina Panthers
        { "CHI", "CHI" },    // Chicago Bears
        { "CIN", "CIN" },    // Cincinnati Bengals
        { "CLE", "CLE" },    // Cleveland Browns
        { "DAL", "DAL" },    // Dallas Cowboys
        { "DEN", "DEN" },    // Denver Broncos
        { "DET", "DET" },    // Detroit Lions
        { "HOU", "HOU" },    // Houston Texans
        { "IND", "IND" },    // Indianapolis Colts
        { "JAX", "JAX" },    // Jacksonville Jaguars
        { "LAC", "LAC" },    // Los Angeles Chargers
        { "LAR", "LAR" },    // Los Angeles Rams
        { "MIA", "MIA" },    // Miami Dolphins
        { "MIN", "MIN" },    // Minnesota Vikings
        { "NYG", "NYG" },    // New York Giants
        { "NYJ", "NYJ" },    // New York Jets
        { "PHI", "PHI" },    // Philadelphia Eagles
        { "PIT", "PIT" },    // Pittsburgh Steelers
        { "SEA", "SEA" },    // Seattle Seahawks  
        { "TEN", "TEN" },    // Tennessee Titans
        { "WAS", "WAS" }     // Washington Commanders
    };

    /// <summary>
    /// Maps ESPN team IDs to Supabase team IDs
    /// </summary>
    private static readonly Dictionary<string, int> EspnIdToSupabaseId = new()
    {
        // ESPN ID -> Supabase ID mapping
        { "22", 1 },   // Arizona Cardinals
        { "1", 2 },    // Atlanta Falcons
        { "33", 3 },   // Baltimore Ravens
        { "2", 4 },    // Buffalo Bills
        { "29", 5 },   // Carolina Panthers
        { "3", 6 },    // Chicago Bears
        { "4", 7 },    // Cincinnati Bengals
        { "5", 8 },    // Cleveland Browns
        { "6", 9 },    // Dallas Cowboys
        { "7", 10 },   // Denver Broncos
        { "8", 11 },   // Detroit Lions
        { "9", 12 },   // Green Bay Packers
        { "34", 13 },  // Houston Texans
        { "11", 14 },  // Indianapolis Colts
        { "30", 15 },  // Jacksonville Jaguars
        { "12", 16 },  // Kansas City Chiefs
        { "24", 17 },  // Los Angeles Chargers
        { "14", 18 },  // Los Angeles Rams
        { "13", 19 },  // Las Vegas Raiders
        { "15", 20 },  // Miami Dolphins
        { "16", 21 },  // Minnesota Vikings
        { "18", 22 },  // New Orleans Saints
        { "17", 23 },  // New England Patriots
        { "19", 24 },  // New York Giants
        { "20", 25 },  // New York Jets
        { "21", 26 },  // Philadelphia Eagles
        { "23", 27 },  // Pittsburgh Steelers
        { "26", 29 },  // Seattle Seahawks
        { "25", 28 },  // San Francisco 49ers
        { "27", 30 },  // Tampa Bay Buccaneers
        { "10", 31 },  // Tennessee Titans
        { "28", 32 }   // Washington Commanders
    };

    /// <summary>
    /// Converts ESPN team abbreviation to Supabase team abbreviation
    /// </summary>
    public static string MapEspnAbbreviationToSupabase(string espnAbbreviation)
    {
        return EspnToSupabaseAbbreviations.GetValueOrDefault(espnAbbreviation.ToUpper(), espnAbbreviation.ToUpper());
    }

    /// <summary>
    /// Converts ESPN team ID to Supabase team ID
    /// </summary>
    public static int? MapEspnIdToSupabaseId(string espnTeamId)
    {
        return EspnIdToSupabaseId.GetValueOrDefault(espnTeamId);
    }

    /// <summary>
    /// Maps ESPN Team model to Supabase Team model
    /// </summary>
    public static Models.Supa.Team? MapEspnTeamToSupabase(Models.Team espnTeam)
    {
        var supabaseTeamId = MapEspnIdToSupabaseId(espnTeam.Id);
        if (!supabaseTeamId.HasValue)
            return null;

        return new Models.Supa.Team
        {
            Id = supabaseTeamId.Value,
            Abbreviation = MapEspnAbbreviationToSupabase(espnTeam.Abbreviation),
            FullName = espnTeam.DisplayName,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };
    }

    /// <summary>
    /// Gets Supabase team ID from ESPN team abbreviation used in PlayerStats
    /// </summary>
    public static int? GetSupabaseTeamIdFromFullName(string teamFullName)
    {
        // Direct lookup based on your database full_name values
        var teamMappings = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            { "Arizona Cardinals", 1 },
            { "Atlanta Falcons", 2 },
            { "Baltimore Ravens", 3 },
            { "Buffalo Bills", 4 },
            { "Carolina Panthers", 5 },
            { "Chicago Bears", 6 },
            { "Cincinnati Bengals", 7 },
            { "Cleveland Browns", 8 },
            { "Dallas Cowboys", 9 },
            { "Denver Broncos", 10 },
            { "Detroit Lions", 11 },
            { "Green Bay Packers", 12 },
            { "Houston Texans", 13 },
            { "Indianapolis Colts", 14 },
            { "Jacksonville Jaguars", 15 },
            { "Kansas City Chiefs", 16 },
            { "Los Angeles Chargers", 17 },
            { "Los Angeles Rams", 18 },
            { "Las Vegas Raiders", 19 },
            { "Miami Dolphins", 20 },
            { "Minnesota Vikings", 21 },
            { "New Orleans Saints", 22 },
            { "New England Patriots", 23 },
            { "New York Giants", 24 },
            { "New York Jets", 25 },
            { "Philadelphia Eagles", 26 },
            { "Pittsburgh Steelers", 27 },
            { "San Francisco 49ers", 28 },
            { "Seattle Seahawks", 29 },
            { "Tampa Bay Buccaneers", 30 },
            { "Tennessee Titans", 31 },
            { "Washington Commanders", 32 }
        };

        return teamMappings.GetValueOrDefault(teamFullName);
    }

    /// <summary>
    /// Gets all team mappings for reference
    /// </summary>
    public static Dictionary<string, (int SupabaseId, string SupabaseAbbr, string FullName)> GetAllTeamMappings()
    {
        return new Dictionary<string, (int, string, string)>
        {
            { "22", (1, "ARI", "Arizona Cardinals") },
            { "1", (2, "ATL", "Atlanta Falcons") },
            { "33", (3, "BAL", "Baltimore Ravens") },
            { "2", (4, "BUF", "Buffalo Bills") },
            { "29", (5, "CAR", "Carolina Panthers") },
            { "3", (6, "CHI", "Chicago Bears") },
            { "4", (7, "CIN", "Cincinnati Bengals") },
            { "5", (8, "CLE", "Cleveland Browns") },
            { "6", (9, "DAL", "Dallas Cowboys") },
            { "7", (10, "DEN", "Denver Broncos") },
            { "8", (11, "DET", "Detroit Lions") },
            { "9", (12, "GNB", "Green Bay Packers") },
            { "34", (13, "HOU", "Houston Texans") },
            { "11", (14, "IND", "Indianapolis Colts") },
            { "30", (15, "JAX", "Jacksonville Jaguars") },
            { "12", (16, "KAN", "Kansas City Chiefs") },
            { "24", (17, "LAC", "Los Angeles Chargers") },
            { "14", (18, "LAR", "Los Angeles Rams") },
            { "13", (19, "LVR", "Las Vegas Raiders") },
            { "15", (20, "MIA", "Miami Dolphins") },
            { "16", (21, "MIN", "Minnesota Vikings") },
            { "18", (22, "NOR", "New Orleans Saints") },
            { "17", (23, "NWE", "New England Patriots") },
            { "19", (24, "NYG", "New York Giants") },
            { "20", (25, "NYJ", "New York Jets") },
            { "21", (26, "PHI", "Philadelphia Eagles") },
            { "23", (27, "PIT", "Pittsburgh Steelers") },
            { "26", (29, "SEA", "Seattle Seahawks") },
            { "25", (28, "SFO", "San Francisco 49ers") },
            { "27", (30, "TAM", "Tampa Bay Buccaneers") },
            { "10", (31, "TEN", "Tennessee Titans") },
            { "28", (32, "WAS", "Washington Commanders") }
        };
    }
}