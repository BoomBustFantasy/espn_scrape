namespace ESPNScrape.Models;

public class ESPNApiResponse<T>
{
    public int Count { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public List<T> Items { get; set; } = new();
    public ESPNApiMeta? Meta { get; set; }
}

// For responses that return references (most list endpoints)
public class ESPNReferenceResponse
{
    public int Count { get; set; }
    public int PageIndex { get; set; }
    public int PageSize { get; set; }
    public int PageCount { get; set; }
    public List<ESPNReference> Items { get; set; } = new();
    public ESPNApiMeta? Meta { get; set; }
}

public class ESPNReference
{
    public string Ref { get; set; } = string.Empty;

    // Helper property to get the actual URL from $ref
    public string GetUrl() => Ref.Replace("$ref", "").Trim('"');
}

// For handling embedded references within objects
public class ESPNEmbeddedReference
{
    public string Ref { get; set; } = string.Empty;
    public string? Id { get; set; }
    public string? Guid { get; set; }

    // Helper property to get the actual URL from $ref
    public string GetUrl() => Ref;

    // Check if this is a reference or actual data
    public bool IsReference => !string.IsNullOrEmpty(Ref);
}

public class ESPNApiMeta
{
    public ESPNApiParameters? Parameters { get; set; }
}

public class ESPNApiParameters
{
    public List<string> Week { get; set; } = new();
    public List<string> Season { get; set; } = new();
    public List<string> Seasontypes { get; set; } = new();
}