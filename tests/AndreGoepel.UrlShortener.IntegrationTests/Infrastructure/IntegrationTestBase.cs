using AndreGoepel.UrlShortener.Models;
using AndreGoepel.UrlShortener.Services;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Wolverine.Tracking;

namespace AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;

[Collection(IntegrationCollection.Name)]
public abstract class IntegrationTestBase(UrlShortenerAppFixture fixture) : IAsyncLifetime
{
    protected UrlShortenerAppFixture Fixture { get; } = fixture;

    /// <summary>Cookie-aware client that does NOT follow redirects, so 302/410 can be asserted.</summary>
    protected HttpClient Client { get; private set; } = default!;

    public virtual async ValueTask InitializeAsync()
    {
        await Fixture.ResetAsync(TestContext.Current.CancellationToken);
        Client = Fixture.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        // A per-test client IP keeps the rate-limit partition isolated.
        Client.DefaultRequestHeaders.Add(
            TestClientIpStartupFilter.HeaderName,
            $"203.0.113.{Random.Shared.Next(1, 250)}"
        );
    }

    public virtual ValueTask DisposeAsync()
    {
        Client.Dispose();
        return ValueTask.CompletedTask;
    }

    /// <summary>Seeds a link straight through the app's own service — no HTTP, no antiforgery.</summary>
    protected async Task<ShortLink> SeedLinkAsync(
        string target = "https://example.com/destination",
        string? alias = null,
        DateTimeOffset? expiresAtUtc = null,
        long? maxClicks = null,
        bool disabled = false
    )
    {
        await using var scope = Fixture.Services.CreateAsyncScope();
        var links = scope.ServiceProvider.GetRequiredService<ShortLinkService>();

        var result = await links.CreateAsync(
            target,
            alias,
            createdByUserId: null,
            clientIp: "203.0.113.1",
            expiresAtUtc: null, // CreateAsync rejects a past expiry; set it directly below instead.
            maxClicks: maxClicks,
            TestContext.Current.CancellationToken
        );

        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to seed a link: {result.Error}");
        }

        var link = result.Link!;

        if (expiresAtUtc is not null || disabled)
        {
            await using var session = Fixture.Store.LightweightSession();
            link.ExpiresAtUtc = expiresAtUtc;
            link.IsDisabled = disabled;
            session.Store(link);
            await session.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        return link;
    }

    /// <summary>
    /// GETs a redirect and waits for the resulting <c>LinkClicked</c> → <c>LinkClickedHandler</c>
    /// cascade to be fully handled before returning — no sleeps, no polling.
    /// </summary>
    protected Task<ITrackedSession> ClickAndWaitAsync(string slug) =>
        Fixture
            .AppHost.TrackActivity()
            .Timeout(TimeSpan.FromSeconds(30))
            .ExecuteAndWaitAsync(_ => Client.GetAsync($"/s/{slug}"));

    /// <summary>
    /// Bounded poll for anything the Wolverine tracking session cannot see. Never
    /// <c>Task.Delay(2000)</c> followed by a bare assert — this replaces that flake pattern.
    /// </summary>
    protected static async Task<T> EventuallyAsync<T>(
        Func<Task<T?>> probe,
        Func<T, bool> until,
        TimeSpan? timeout = null
    )
        where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(20));
        while (true)
        {
            var value = await probe();
            if (value is not null && until(value))
            {
                return value;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition not reached in time.");
            }

            await Task.Delay(100);
        }
    }
}
