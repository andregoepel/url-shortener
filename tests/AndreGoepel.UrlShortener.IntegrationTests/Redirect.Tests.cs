using System.Net;
using AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;
using AndreGoepel.UrlShortener.Models;
using Marten;

namespace AndreGoepel.UrlShortener.IntegrationTests;

public sealed class RedirectTests(UrlShortenerAppFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Get_KnownSlug_Returns302ToTarget()
    {
        // Arrange
        var link = await SeedLinkAsync(target: "https://example.com/target-a");

        // Act
        var response = await Client.GetAsync(
            $"/s/{link.Id}",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Found, response.StatusCode);
        Assert.Equal("https://example.com/target-a", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Get_UnknownSlug_Returns410Gone()
    {
        // Act
        var response = await Client.GetAsync(
            "/s/does-not-exist",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Contains("This link is no longer available", body);
    }

    [Fact]
    public async Task Get_DisabledSlug_Returns410Gone()
    {
        // Arrange
        var link = await SeedLinkAsync(disabled: true);

        // Act
        var response = await Client.GetAsync(
            $"/s/{link.Id}",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Get_ExpiredSlug_Returns410Gone()
    {
        // Arrange
        var link = await SeedLinkAsync(expiresAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        var response = await Client.GetAsync(
            $"/s/{link.Id}",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Get_SlugAtMaxClicks_Returns410Gone()
    {
        // Arrange
        var link = await SeedLinkAsync(maxClicks: 1);

        // Act — first click succeeds and is drained, second finds the limit reached.
        await ClickAndWaitAsync(link.Id);
        var response = await Client.GetAsync(
            $"/s/{link.Id}",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Get_KnownSlug_RecordsClickAfterHandlerDrains()
    {
        // Arrange
        var link = await SeedLinkAsync();
        Client.DefaultRequestHeaders.Referrer = new Uri("https://referrer.example/");
        Client.DefaultRequestHeaders.UserAgent.ParseAdd("integration-test-agent");

        // Act
        await ClickAndWaitAsync(link.Id);

        // Assert
        await using var session = Fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<ShortLink>(
            link.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(1, persisted!.ClickCount);
        Assert.NotNull(persisted.LastAccessedUtc);

        var click = Assert.Single(
            await session
                .Query<LinkClick>()
                .Where(c => c.Slug == link.Id)
                .ToListAsync(TestContext.Current.CancellationToken)
        );
        Assert.Equal("https://referrer.example/", click.Referer);
        Assert.Equal("integration-test-agent", click.UserAgent);
    }

    [Fact]
    public async Task Get_KnownSlugTwice_RecordsTwoClicks()
    {
        // Arrange
        var link = await SeedLinkAsync();

        // Act
        await ClickAndWaitAsync(link.Id);
        await ClickAndWaitAsync(link.Id);

        // Assert
        await using var session = Fixture.Store.QuerySession();
        var persisted = await session.LoadAsync<ShortLink>(
            link.Id,
            TestContext.Current.CancellationToken
        );
        Assert.Equal(2, persisted!.ClickCount);

        var clicks = await session
            .Query<LinkClick>()
            .Where(c => c.Slug == link.Id)
            .ToListAsync(TestContext.Current.CancellationToken);
        Assert.Equal(2, clicks.Count);
    }
}
