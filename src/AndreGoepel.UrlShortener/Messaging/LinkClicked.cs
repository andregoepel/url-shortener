namespace AndreGoepel.UrlShortener.Messaging;

/// <summary>
/// Published on every successful redirect so click recording happens off the hot path.
/// Handled by <see cref="LinkClickedHandler"/> via Wolverine's local durable queue.
/// </summary>
public sealed record LinkClicked(
    string Slug,
    string? Referer,
    string? UserAgent,
    DateTimeOffset OccurredAtUtc
);
