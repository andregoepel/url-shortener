using AndreGoepel.UrlShortener.Models;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>Outcome of a create attempt: the new link on success, or a user-facing error.</summary>
public sealed record CreateResult(bool Success, string? Error, ShortLink? Link)
{
    public static CreateResult Ok(ShortLink link) => new(true, null, link);

    public static CreateResult Fail(string error) => new(false, error, null);
}
