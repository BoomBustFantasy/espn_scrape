namespace ESPNScrape.Configuration;

public class SupabaseSettings
{
    public const string SectionName = "Supabase";

    public string Url { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string ServiceRoleKey { get; set; } = string.Empty;
    public string ProjectId { get; set; } = string.Empty;
}
