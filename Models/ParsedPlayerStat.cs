namespace ESPNScrape.Models;

public record ParsedPlayerStat
{
    public string EspnPlayerId { get; set; } = string.Empty;
    public string EspnTeamId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string TeamDisplayName { get; set; } = string.Empty;
    public string EspnGameId { get; set; } = string.Empty;
    public DateTime GameDate { get; set; }
    public int Season { get; set; }
    public int Week { get; set; }
    public object? Passing { get; set; }
    public object? Rushing { get; set; }
    public object? Receiving { get; set; }
    public int Fumbles { get; set; }
    public int FumblesLost { get; set; }
}
