using AndreGoepel.UrlShortener.Services;

namespace AndreGoepel.UrlShortener.Messaging;

/// <summary>
/// Wolverine handler that persists a click. Discovered via
/// <c>options.ConfigureWolverine = w =&gt; w.Discovery.IncludeAssembly(...)</c> in Program.cs.
/// </summary>
public static class LinkClickedHandler
{
    public static Task Handle(
        LinkClicked message,
        ShortLinkService links,
        CancellationToken cancellationToken
    ) =>
        links.RecordClickAsync(
            message.Slug,
            message.Referer,
            message.UserAgent,
            message.OccurredAtUtc,
            cancellationToken
        );
}
