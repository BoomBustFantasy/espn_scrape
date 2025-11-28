namespace ESPNScrape.Models;

public class Venue
{
    public string Id { get; set; } = string.Empty;
    public string Guid { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public Address? Address { get; set; }
    public bool Grass { get; set; }
    public bool Indoor { get; set; }
    public List<Image> Images { get; set; } = new();
}

public class Address
{
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
}

public class Image
{
    public string Href { get; set; } = string.Empty;
    public int Width { get; set; }
    public int Height { get; set; }
    public string Alt { get; set; } = string.Empty;
    public List<string> Rel { get; set; } = new();
}