using AndreGoepel.UrlShortener.Messaging;
using AndreGoepel.UrlShortener.Services;
using Wolverine;

namespace AndreGoepel.UrlShortener.Endpoints;

/// <summary>
/// Anonymous minimal-API endpoints for resolving short links. Kept under the <c>/s/</c> prefix
/// so single-segment slugs never collide with the app's own routes (<c>/Setup</c>,
/// <c>/Account/*</c>, <c>/admin/*</c>). A bare short domain can be mapped onto <c>/s/</c> at the
/// reverse proxy without any code change.
/// </summary>
public static class RedirectEndpoints
{
    public static void MapShortenerEndpoints(this WebApplication app)
    {
        app.MapGet(
            "/s/{slug}",
            async (
                string slug,
                HttpContext http,
                ShortLinkService links,
                IMessageBus bus,
                CancellationToken ct
            ) =>
            {
                var link = await links.ResolveAsync(slug, ct);
                if (link is null || link.IsDisabled || link.IsExpired(DateTimeOffset.UtcNow))
                {
                    return Results.Content(
                        GonePage,
                        "text/html",
                        statusCode: StatusCodes.Status410Gone
                    );
                }

                // Record the click asynchronously so the redirect stays fast.
                var referer = http.Request.Headers.Referer.ToString();
                var userAgent = http.Request.Headers.UserAgent.ToString();
                await bus.PublishAsync(
                    new LinkClicked(
                        slug,
                        string.IsNullOrEmpty(referer) ? null : referer,
                        string.IsNullOrEmpty(userAgent) ? null : userAgent,
                        DateTimeOffset.UtcNow
                    )
                );

                return Results.Redirect(link.TargetUrl, permanent: false);
            }
        );

        app.MapGet(
            "/s/{slug}/qr",
            async (
                string slug,
                HttpContext http,
                ShortLinkService links,
                QrCodeService qr,
                CancellationToken ct
            ) =>
            {
                // Mirrors the redirect's availability check (#31): a QR code for a link that
                // can no longer be followed would just point print material at a dead link.
                var link = await links.ResolveAsync(slug, ct);
                if (link is null || link.IsDisabled || link.IsExpired(DateTimeOffset.UtcNow))
                {
                    return Results.Content(
                        GonePage,
                        "text/html",
                        statusCode: StatusCodes.Status410Gone
                    );
                }

                var shortUrl = $"{http.Request.Scheme}://{http.Request.Host}/s/{slug}";
                return Results.File(qr.GeneratePng(shortUrl), "image/png");
            }
        );
    }

    private const string GonePage = """
        <!doctype html>
        <html lang="en">
        <head><meta charset="utf-8"><title>Link unavailable</title></head>
        <body style="font-family:system-ui,sans-serif;max-width:32rem;margin:4rem auto;text-align:center">
          <h1>This link is no longer available</h1>
          <p>It may have expired, reached its click limit, or been disabled.</p>
        </body>
        </html>
        """;
}
