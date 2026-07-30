using AndreGoepel.Marten.Configuration;
using AndreGoepel.UrlShortener.Models;
using Microsoft.Extensions.Caching.Memory;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Resolves the shortener feature flags: the persisted <see cref="ShortenerFeatureSettings"/>
/// record wins; without one the defaults apply (public creation enabled). The hot read path
/// on the public create page is served from <see cref="IMemoryCache"/>; a toggle updates the
/// cache in place so the change takes effect immediately.
/// </summary>
public sealed class ShortenerFeatureService(ISettingsStore store, IMemoryCache cache)
{
    private const string CacheKey = "shortener:allow-public-creation";

    // Safety net for out-of-band database edits; toggles via SetPublicCreationEnabledAsync
    // refresh the cached value immediately regardless.
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);

    public async ValueTask<bool> IsPublicCreationEnabledAsync(CancellationToken ct = default)
    {
        if (cache.TryGetValue(CacheKey, out bool cached))
        {
            return cached;
        }

        var settings = await store.LoadAsync<ShortenerFeatureSettings>(ct);
        var enabled = settings?.AllowPublicCreation ?? true;
        cache.Set(CacheKey, enabled, CacheDuration);
        return enabled;
    }

    public async Task SetPublicCreationEnabledAsync(bool enabled, CancellationToken ct = default)
    {
        await store.SaveAsync(new ShortenerFeatureSettings { AllowPublicCreation = enabled }, ct);
        cache.Set(CacheKey, enabled, CacheDuration);
    }
}
