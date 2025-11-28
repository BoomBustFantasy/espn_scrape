namespace ESPNScrape.Models;

public class OffensivePlayerStats
{
    public string PlayerName { get; set; } = string.Empty;
    public string Position { get; set; } = string.Empty;
    public string Team { get; set; } = string.Empty;

    // Passing stats
    public int? Completions { get; set; }
    public int? Attempts { get; set; }
    public double? CompletionPercentage { get; set; }
    public int? PassingYards { get; set; }
    public double? YardsPerAttempt { get; set; }
    public int? PassingTouchdowns { get; set; }
    public int? Interceptions { get; set; }
    public double? QBRating { get; set; }

    // Rushing stats
    public int? Carries { get; set; }
    public int? RushingYards { get; set; }
    public double? YardsPerCarry { get; set; }
    public int? RushingTouchdowns { get; set; }
    public int? LongestRush { get; set; }

    // Receiving stats
    public int? Receptions { get; set; }
    public int? ReceivingYards { get; set; }
    public double? YardsPerReception { get; set; }
    public int? ReceivingTouchdowns { get; set; }
    public int? LongestReception { get; set; }
    public int? Targets { get; set; }

    public static OffensivePlayerStats ParseFromCategory(PlayerStats playerStat, PlayerStatCategory category)
    {
        var stats = new OffensivePlayerStats
        {
            PlayerName = playerStat.Athlete.DisplayName,
            Position = playerStat.Athlete.Position?.Abbreviation ?? "",
            Team = "" // Can be derived from team reference if needed
        };

        // Parse stats based on category and labels
        for (int i = 0; i < Math.Min(category.Labels.Count, playerStat.Stats.Count); i++)
        {
            var label = category.Labels[i].ToLower();
            var value = playerStat.Stats[i];

            // Handle completion/attempts format like "12/20" or "12-20"
            if (label.Contains("comp") && label.Contains("att"))
            {
                var parts = value.Split(new[] { '/', '-' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    if (int.TryParse(parts[0], out var comp)) stats.Completions = comp;
                    if (int.TryParse(parts[1], out var att)) stats.Attempts = att;
                    if (stats.Completions.HasValue && stats.Attempts.HasValue && stats.Attempts > 0)
                    {
                        stats.CompletionPercentage = Math.Round((double)stats.Completions.Value / stats.Attempts.Value * 100, 1);
                    }
                }
            }
            else
            {
                // Handle individual stats
                switch (label)
                {
                    case "yds" when category.Name.ToLower() == "passing":
                        if (int.TryParse(value.Replace(",", ""), out var passYds)) stats.PassingYards = passYds;
                        break;
                    case "yds" when category.Name.ToLower() == "rushing":
                        if (int.TryParse(value.Replace(",", ""), out var rushYds)) stats.RushingYards = rushYds;
                        break;
                    case "yds" when category.Name.ToLower() == "receiving":
                        if (int.TryParse(value.Replace(",", ""), out var recYds)) stats.ReceivingYards = recYds;
                        break;
                    case "avg":
                        if (double.TryParse(value, out var avg))
                        {
                            switch (category.Name.ToLower())
                            {
                                case "passing": stats.YardsPerAttempt = avg; break;
                                case "rushing": stats.YardsPerCarry = avg; break;
                                case "receiving": stats.YardsPerReception = avg; break;
                            }
                        }
                        break;
                    case "td":
                        if (int.TryParse(value, out var td))
                        {
                            switch (category.Name.ToLower())
                            {
                                case "passing": stats.PassingTouchdowns = td; break;
                                case "rushing": stats.RushingTouchdowns = td; break;
                                case "receiving": stats.ReceivingTouchdowns = td; break;
                            }
                        }
                        break;
                    case "int" when category.Name.ToLower() == "passing":
                        if (int.TryParse(value, out var ints)) stats.Interceptions = ints;
                        break;
                    case "car" when category.Name.ToLower() == "rushing":
                        if (int.TryParse(value, out var carries)) stats.Carries = carries;
                        break;
                    case "rec" when category.Name.ToLower() == "receiving":
                        if (int.TryParse(value, out var rec)) stats.Receptions = rec;
                        break;
                    case "tgts" when category.Name.ToLower() == "receiving":
                        if (int.TryParse(value, out var tgts)) stats.Targets = tgts;
                        break;
                    case "long":
                        if (int.TryParse(value, out var lng))
                        {
                            switch (category.Name.ToLower())
                            {
                                case "rushing": stats.LongestRush = lng; break;
                                case "receiving": stats.LongestReception = lng; break;
                            }
                        }
                        break;
                    case "rtg" when category.Name.ToLower() == "passing":
                        if (double.TryParse(value, out var rtg)) stats.QBRating = rtg;
                        break;
                }
            }
        }

        return stats;
    }
}