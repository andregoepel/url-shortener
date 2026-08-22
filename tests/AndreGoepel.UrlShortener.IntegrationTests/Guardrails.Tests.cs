using System.Net;
using AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;
using AndreGoepel.UrlShortener.Models;
using Marten;
using Microsoft.AspNetCore.Mvc.Testing;

namespace AndreGoepel.UrlShortener.IntegrationTests;

public sealed class GuardrailsTests(UrlShortenerAppFixture fixture) : IntegrationTestBase(fixture)
{
    [Theory]
    [InlineData("javascript:alert(1)", "Only http and https URLs are allowed.")]
    [InlineData("http://127.0.0.1/admin", "local or private addresses")]
    [InlineData("http://192.168.1.1/", "local or private addresses")]
    public async Task Post_RejectedUrl_ShowsTheExpectedError(string url, string expectedFragment)
    {
        // Arrange
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?> { ["Input.Url"] = url }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        var error = resultDoc.QuerySelector("div.form-error");
        Assert.NotNull(error);
        Assert.Contains(expectedFragment, error.TextContent);
    }

    [Fact]
    public async Task Post_UrlTooLong_ShowsTooLongError()
    {
        // Arrange
        var longUrl = "https://example.com/" + new string('a', 2049);
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?> { ["Input.Url"] = longUrl }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        Assert.Contains("too long", resultDoc.QuerySelector("div.form-error")!.TextContent);
    }

    [Theory]
    [InlineData("admin")]
    // "s" is deliberately not covered here: at 1 character it fails the 3-32-length check
    // in AliasPattern before the reserved-word check ever runs, so it cannot exercise this
    // path — see Post_InvalidAliasShape_ShowsLengthError for that check instead.
    [InlineData("setup")]
    [InlineData("_blazor")]
    public async Task Post_ReservedAlias_ShowsReservedError(string alias)
    {
        // Arrange
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["Input.CustomAlias"] = alias,
            }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        Assert.Contains(
            "That alias is reserved.",
            resultDoc.QuerySelector("div.form-error")!.TextContent
        );
    }

    [Theory]
    [InlineData("ab")]
    [InlineData("!!!")]
    public async Task Post_InvalidAliasShape_ShowsLengthError(string alias)
    {
        // Arrange
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["Input.CustomAlias"] = alias,
            }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        Assert.Contains(
            "Alias must be 3–32 characters",
            resultDoc.QuerySelector("div.form-error")!.TextContent
        );
    }

    [Fact]
    public async Task Post_DuplicateAlias_ShowsAlreadyTakenError()
    {
        // Arrange
        await SeedLinkAsync(alias: "taken-alias");
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["Input.CustomAlias"] = "taken-alias",
            }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        Assert.Contains("already taken", resultDoc.QuerySelector("div.form-error")!.TextContent);
    }

    [Fact]
    public async Task Post_MaxClicksZero_ShowsPositiveNumberError()
    {
        // Arrange
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["Input.MaxClicks"] = "0",
            }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        Assert.Contains(
            "Max clicks must be a positive number.",
            resultDoc.QuerySelector("div.form-error")!.TextContent
        );
    }

    [Fact]
    public async Task Post_ExpiresOnYesterday_ShowsFutureDateError()
    {
        // Arrange
        var yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd");
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["Input.ExpiresOn"] = yesterday,
            }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        var resultDoc = await response.ReadHtmlAsync();
        Assert.Contains(
            "expiry date must be in the future",
            resultDoc.QuerySelector("div.form-error")!.TextContent
        );
    }

    [Fact]
    public async Task Post_MoreCreatesThanPermitted_ReturnsTooManyRequests()
    {
        // Arrange — a dedicated fixture instance (own container, own host, own
        // CreateRateLimiter singleton) so the shared window every other test in this
        // collection relies on stays unpolluted.
        await using var limitedFixture = new UrlShortenerAppFixture(permitLimit: 2);
        await limitedFixture.InitializeAsync();
        using var client = limitedFixture.CreateClient(
            new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }
        );
        client.DefaultRequestHeaders.Add(TestClientIpStartupFilter.HeaderName, "203.0.113.99");

        async Task<HttpResponseMessage> CreateOnceAsync()
        {
            var home = await client.GetAsync("/", TestContext.Current.CancellationToken);
            var doc = await home.ReadHtmlAsync();
            var content = doc.BuildPost(
                "form",
                new Dictionary<string, string?>
                {
                    ["Input.Url"] = "https://example.com/destination",
                }
            );
            return await client.PostAsync("/", content, TestContext.Current.CancellationToken);
        }

        // Act — two permitted, then the third trips the limit.
        await CreateOnceAsync();
        await CreateOnceAsync();
        var response = await CreateOnceAsync();

        // Assert
        Assert.Equal(HttpStatusCode.TooManyRequests, response.StatusCode);
        var doc2 = await response.ReadHtmlAsync();
        Assert.Contains(
            "Too many links created from your address",
            doc2.QuerySelector("div.form-error")!.TextContent
        );
    }
}
