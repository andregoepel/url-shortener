using AndreGoepel.Marten.Configuration;
using AndreGoepel.UrlShortener.Models;
using AndreGoepel.UrlShortener.Services;
using Marten;

namespace AndreGoepel.UrlShortener;

/// <summary>
/// Registers this host's own services on top of the AppFoundation seam. Mirrors the
/// <c>AddWebsite()</c> convention used by the reference consumer.
/// </summary>
public static class Initialization
{
    public static IServiceCollection AddUrlShortener(this IServiceCollection services)
    {
        services.AddScoped<ShortLinkService>();
        services.AddSingleton<SlugGenerator>();
        services.AddSingleton<UrlValidator>();
        services.AddSingleton<QrCodeService>();
        // A process-wide, IP-partitioned limiter guarding anonymous public link creation.
        services.AddOptions<CreateRateLimiterOptions>().BindConfiguration("Shortener:RateLimit");
        services.AddSingleton<CreateRateLimiter>();

        // Admin-toggleable feature flags (public-creation kill switch), persisted via the
        // foundation's settings store. The document joins the shared settings table the same
        // way the foundation registers its own settings types.
        services.AddSingleton<ShortenerFeatureService>();
        services.ConfigureMarten(marten => marten.AddSettingsDocument<ShortenerFeatureSettings>());
        return services;
    }
}
