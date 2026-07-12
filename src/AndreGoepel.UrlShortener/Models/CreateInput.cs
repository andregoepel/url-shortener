namespace AndreGoepel.UrlShortener.Models;

/// <summary>Form model bound from the public create page (static SSR form post).</summary>
public class CreateInput
{
    public string? Url { get; set; }

    public string? CustomAlias { get; set; }

    public DateTime? ExpiresOn { get; set; }

    public long? MaxClicks { get; set; }
}
