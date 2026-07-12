using AndreGoepel.UrlShortener.Services;

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
        services.AddSingleton<CreateRateLimiter>();
        return services;
    }
}
