namespace ESPNScrape.Models;

public class League
{
    public string Id { get; set; } = string.Empty;
    public string Uid { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Abbreviation { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsTournament { get; set; }
    public List<Link> Links { get; set; } = new();
}