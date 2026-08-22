using System.Net;
using AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;

namespace AndreGoepel.UrlShortener.IntegrationTests;

/// <summary>
/// The QR endpoint mirrors the redirect's availability check (upstream #31): missing,
/// disabled, and expired links all answer 410 Gone, the same as <c>/s/{slug}</c> itself —
/// refusing to mint a code for a link that cannot be followed.
/// </summary>
public sealed class QrCodeEndpointTests(UrlShortenerAppFixture fixture)
    : IntegrationTestBase(fixture)
{
    private static readonly byte[] PngMagicBytes = [0x89, 0x50, 0x4E, 0x47];

    [Fact]
    public async Task Get_QrForKnownSlug_ReturnsPng()
    {
        // Arrange
        var link = await SeedLinkAsync();

        // Act
        var response = await Client.GetAsync(
            $"/s/{link.Id}/qr",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
        var bytes = await response.Content.ReadAsByteArrayAsync(
            TestContext.Current.CancellationToken
        );
        Assert.True(bytes.Length > 0);
        Assert.Equal(PngMagicBytes, bytes[..4]);
    }

    [Fact]
    public async Task Get_QrForUnknownSlug_Returns410Gone()
    {
        // Act
        var response = await Client.GetAsync(
            "/s/does-not-exist/qr",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Get_QrForDisabledSlug_Returns410Gone()
    {
        // Arrange
        var link = await SeedLinkAsync(disabled: true);

        // Act
        var response = await Client.GetAsync(
            $"/s/{link.Id}/qr",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }

    [Fact]
    public async Task Get_QrForExpiredSlug_Returns410Gone()
    {
        // Arrange
        var link = await SeedLinkAsync(expiresAtUtc: DateTimeOffset.UtcNow.AddDays(-1));

        // Act
        var response = await Client.GetAsync(
            $"/s/{link.Id}/qr",
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.Equal(HttpStatusCode.Gone, response.StatusCode);
    }
}
