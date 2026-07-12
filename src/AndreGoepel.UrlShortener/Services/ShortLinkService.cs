using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using AndreGoepel.UrlShortener.Models;
using Marten;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Application logic for creating, resolving and managing short links. Opens its own Marten
/// sessions (mirroring the foundation's <c>ImageService</c> pattern) so it is safe to use from
/// Blazor components, minimal-API endpoints, and Wolverine handlers alike.
/// </summary>
public sealed partial class ShortLinkService(
    IDocumentStore store,
    SlugGenerator slugs,
    UrlValidator urlValidator
)
{
    // Slugs that must never be handed out as links because they collide with real app routes.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "s",
        "admin",
        "account",
        "setup",
        "dashboard",
        "health",
        "qr",
        "api",
        "media",
        "css",
        "js",
        "lib",
        "favicon",
        "_blazor",
        "_framework",
        "_content",
    };

    [GeneratedRegex("^[A-Za-z0-9_-]{3,32}$")]
    private static partial Regex AliasPattern();

    public async Task<CreateResult> CreateAsync(
        string? url,
        string? customAlias,
        string? createdByUserId,
        string? clientIp,
        DateTimeOffset? expiresAtUtc,
        long? maxClicks,
        CancellationToken ct = default
    )
    {
        if (!urlValidator.TryValidate(url, out var target, out var urlError))
        {
            return CreateResult.Fail(urlError!);
        }

        if (maxClicks is <= 0)
        {
            return CreateResult.Fail("Max clicks must be a positive number.");
        }

        if (expiresAtUtc is { } expiry && expiry <= DateTimeOffset.UtcNow)
        {
            return CreateResult.Fail("The expiry date must be in the future.");
        }

        await using var session = store.LightweightSession();

        var isCustom = !string.IsNullOrWhiteSpace(customAlias);
        string slug;
        if (isCustom)
        {
            slug = customAlias!.Trim();
            if (!AliasPattern().IsMatch(slug))
            {
                return CreateResult.Fail(
                    "Alias must be 3–32 characters: letters, digits, hyphen or underscore."
                );
            }

            if (Reserved.Contains(slug))
            {
                return CreateResult.Fail("That alias is reserved. Please choose another.");
            }

            if (await session.LoadAsync<ShortLink>(slug, ct) is not null)
            {
                return CreateResult.Fail("That alias is already taken. Please choose another.");
            }
        }
        else
        {
            slug = await GenerateUniqueSlugAsync(session, ct);
        }

        var link = new ShortLink
        {
            Id = slug,
            TargetUrl = target,
            CreatedAtUtc = DateTimeOffset.UtcNow,
            CreatedByUserId = createdByUserId,
            CreatorIpHash = HashIp(clientIp),
            ExpiresAtUtc = expiresAtUtc,
            MaxClicks = maxClicks,
            IsCustomAlias = isCustom,
        };

        session.Store(link);
        await session.SaveChangesAsync(ct);
        return CreateResult.Ok(link);
    }

    public async Task<ShortLink?> ResolveAsync(string slug, CancellationToken ct = default)
    {
        await using var session = store.QuerySession();
        return await session.LoadAsync<ShortLink>(slug, ct);
    }

    /// <summary>
    /// Records a click: increments the aggregate counter on the link and stores a detail
    /// document. Invoked off the redirect hot path by the <c>LinkClicked</c> Wolverine handler.
    /// </summary>
    public async Task RecordClickAsync(
        string slug,
        string? referer,
        string? userAgent,
        DateTimeOffset occurredAtUtc,
        CancellationToken ct = default
    )
    {
        await using var session = store.LightweightSession();
        var link = await session.LoadAsync<ShortLink>(slug, ct);
        if (link is null)
        {
            return;
        }

        link.ClickCount++;
        link.LastAccessedUtc = occurredAtUtc;
        session.Store(link);

        session.Store(
            new LinkClick
            {
                Id = Guid.NewGuid(),
                Slug = slug,
                TimestampUtc = occurredAtUtc,
                Referer = Truncate(referer, 512),
                UserAgent = Truncate(userAgent, 512),
            }
        );

        await session.SaveChangesAsync(ct);
    }

    public async Task<IReadOnlyList<ShortLink>> ListAsync(
        string? search,
        CancellationToken ct = default
    )
    {
        await using var session = store.QuerySession();
        var query = session.Query<ShortLink>();

        if (string.IsNullOrWhiteSpace(search))
        {
            return await query.OrderByDescending(l => l.CreatedAtUtc).Take(500).ToListAsync(ct);
        }

        var term = search.Trim();
        return await query
            .Where(l => l.Id.Contains(term) || l.TargetUrl.Contains(term))
            .OrderByDescending(l => l.CreatedAtUtc)
            .Take(500)
            .ToListAsync(ct);
    }

    public async Task SetDisabledAsync(string slug, bool disabled, CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        var link = await session.LoadAsync<ShortLink>(slug, ct);
        if (link is null)
        {
            return;
        }

        link.IsDisabled = disabled;
        session.Store(link);
        await session.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(string slug, CancellationToken ct = default)
    {
        await using var session = store.LightweightSession();
        session.Delete<ShortLink>(slug);
        await session.SaveChangesAsync(ct);
    }

    private async Task<string> GenerateUniqueSlugAsync(
        IDocumentSession session,
        CancellationToken ct
    )
    {
        for (var attempt = 0; attempt < 8; attempt++)
        {
            var candidate = slugs.Next();
            if (
                !Reserved.Contains(candidate)
                && await session.LoadAsync<ShortLink>(candidate, ct) is null
            )
            {
                return candidate;
            }
        }

        throw new InvalidOperationException("Could not allocate a unique slug after 8 attempts.");
    }

    private static string? HashIp(string? ip)
    {
        if (string.IsNullOrWhiteSpace(ip))
        {
            return null;
        }

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(ip));
        return Convert.ToHexString(hash)[..16];
    }

    private static string? Truncate(string? value, int max) =>
        string.IsNullOrEmpty(value) || value.Length <= max ? value : value[..max];
}
