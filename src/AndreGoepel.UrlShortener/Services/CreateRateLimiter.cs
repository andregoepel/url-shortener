using System.Threading.RateLimiting;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Process-wide, IP-partitioned fixed-window limiter for anonymous public link creation.
/// Kept as an explicit service (rather than the ASP.NET rate-limiter middleware) because the
/// public create page is handled as a static-SSR form post, not a routed minimal-API endpoint.
/// </summary>
public sealed class CreateRateLimiter : IDisposable
{
    // 10 links per minute per client IP — generous for humans, hostile to bulk abuse.
    private readonly PartitionedRateLimiter<string> _limiter = PartitionedRateLimiter.Create<
        string,
        string
    >(key =>
        RateLimitPartition.GetFixedWindowLimiter(
            key,
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
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
