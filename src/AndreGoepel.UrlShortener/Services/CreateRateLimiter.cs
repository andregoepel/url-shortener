using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Process-wide, IP-partitioned fixed-window limiter for anonymous public link creation.
/// Kept as an explicit service (rather than the ASP.NET rate-limiter middleware) because the
/// public create page is handled as a static-SSR form post, not a routed minimal-API endpoint.
/// </summary>
public sealed class CreateRateLimiter(IOptions<CreateRateLimiterOptions> options) : IDisposable
{
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<
        string,
        string
    >(key =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = options.Value.PermitLimit,
                Window = options.Value.Window,
                QueueLimit = 0,
            }
        )
    );

    public bool TryAcquire(string partitionKey)
    {
        using var lease = _limiter.AttemptAcquire(partitionKey);
        return lease.IsAcquired;
    }

    public void Dispose() => _limiter.Dispose();
}

/// <summary>
/// Tuning knobs for <see cref="CreateRateLimiter"/>. Bound from configuration
/// (<c>Shortener:RateLimit</c>) so an operator can raise or lower the limit without a
/// redeploy, and so tests can run well above or below the production default.
/// </summary>
public sealed class CreateRateLimiterOptions
{
    /// <summary>Links a single client IP may create per <see cref="Window"/>.</summary>
    public int PermitLimit { get; set; } = 10;

    public TimeSpan Window { get; set; } = TimeSpan.FromMinutes(1);
}
