namespace AndreGoepel.UrlShortener.Models;

/// <summary>
/// A single recorded click on a short link, written asynchronously off the redirect hot
/// path (via the <c>LinkClicked</c> Wolverine message) for per-click analytics detail. The
/// aggregate count lives on <see cref="ShortLink.ClickCount"/>.
/// </summary>
public class LinkClick
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = default!;

    public DateTimeOffset TimestampUtc { get; set; }

    public string? Referer { get; set; }

    public string? UserAgent { get; set; }
}
