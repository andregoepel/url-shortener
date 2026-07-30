using AndreGoepel.UrlShortener.Models;
using AndreGoepel.UrlShortener.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AndreGoepel.UrlShortener.Tests;

/// <summary>
/// Covers the public-creation kill switch in <see cref="ShortLinkService.CreateAsync"/>. The
/// gate runs before any Marten session is opened, so the service is constructed without a
/// document store — reaching the store would fail the test.
/// </summary>
public sealed class ShortLinkServiceTests
{
    private const string DisabledMessage = "Public creation is currently disabled.";

    [Fact]
    public async Task CreateAsync_AnonymousWhilePublicCreationDisabled_ReturnsDisabledError()
    {
        // Arrange
        var service = CreateService(publicCreationEnabled: false);

        // Act
        var result = await service.CreateAsync(
            "https://example.com/some/path",
            customAlias: null,
            createdByUserId: null,
            clientIp: "203.0.113.1",
            expiresAtUtc: null,
            maxClicks: null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Success);
        Assert.Equal(DisabledMessage, result.Error);
    }

    [Fact]
    public async Task CreateAsync_SignedInUserWhilePublicCreationDisabled_PassesKillSwitch()
    {
        // Arrange
        var service = CreateService(publicCreationEnabled: false);

        // Act: the invalid URL fails validation, proving the request got past the gate.
        var result = await service.CreateAsync(
            "not a url",
            customAlias: null,
            createdByUserId: "admin-user-id",
            clientIp: null,
            expiresAtUtc: null,
            maxClicks: null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(DisabledMessage, result.Error);
    }

    [Fact]
    public async Task CreateAsync_AnonymousWhilePublicCreationEnabled_PassesKillSwitch()
    {
        // Arrange
        var service = CreateService(publicCreationEnabled: true);

        // Act: the invalid URL fails validation, proving the request got past the gate.
        var result = await service.CreateAsync(
            "not a url",
            customAlias: null,
            createdByUserId: null,
            clientIp: "203.0.113.1",
            expiresAtUtc: null,
            maxClicks: null,
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(result.Success);
        Assert.NotEqual(DisabledMessage, result.Error);
    }

    private static ShortLinkService CreateService(bool publicCreationEnabled)
    {
        var store = new FakeSettingsStore();
        if (!publicCreationEnabled)
        {
            store.Document = new ShortenerFeatureSettings { AllowPublicCreation = false };
        }

        var features = new ShortenerFeatureService(
            store,
            new MemoryCache(new MemoryCacheOptions())
        );
        return new ShortLinkService(
            store: null!,
            new SlugGenerator(),
            new UrlValidator(),
            features
        );
    }
}
