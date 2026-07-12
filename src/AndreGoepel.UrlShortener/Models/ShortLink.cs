namespace AndreGoepel.UrlShortener.Models;

/// <summary>
/// A shortened link. The document <see cref="Id"/> is the slug itself, so uniqueness and
/// O(1) lookup on redirect come for free from Marten's primary key.
/// </summary>
public class ShortLink
{
    /// <summary>The slug — also the Marten document identity.</summary>
    public string Id { get; set; } = default!;

    public string TargetUrl { get; set; } = default!;

    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <summary>Identity user id when created by a signed-in user; null for anonymous creation.</summary>
    public string? CreatedByUserId { get; set; }

    /// <summary>Truncated SHA-256 of the creator IP for abuse tracing — never the raw address.</summary>
    public string? CreatorIpHash { get; set; }

    public DateTimeOffset? ExpiresAtUtc { get; set; }

    public long? MaxClicks { get; set; }

    public long ClickCount { get; set; }

    public DateTimeOffset? LastAccessedUtc { get; set; }

    public bool IsCustomAlias { get; set; }

    /// <summary>Administrator kill switch — a disabled link resolves to 410 Gone.</summary>
    public bool IsDisabled { get; set; }

    /// <summary>True when the link has passed its expiry date or reached its click limit.</summary>
    public bool IsExpired(DateTimeOffset now) =>
        (ExpiresAtUtc is { } expiry && expiry <= now)
        || (MaxClicks is { } max && ClickCount >= max);
}
