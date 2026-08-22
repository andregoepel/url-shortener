using System.Net;
using AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;
using AndreGoepel.UrlShortener.Models;
using AndreGoepel.UrlShortener.Services;
using Marten;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.UrlShortener.IntegrationTests;

/// <summary>
/// The antiforgery regression guard (#31 upstream — <c>EditForm</c> plus an explicit
/// <c>&lt;AntiforgeryToken /&gt;</c> would emit the hidden field twice, rejected as "A valid
/// antiforgery token was not provided").
/// </summary>
public sealed class PublicCreateFlowTests(UrlShortenerAppFixture fixture)
    : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task Get_HomePage_RendersExactlyOneAntiforgeryToken()
    {
        // Act
        var response = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await response.ReadHtmlAsync();

        // Assert
        Assert.Equal(1, doc.CountAntiforgeryTokens());
    }

    [Fact]
    public async Task Get_HomePage_RendersShortenForm()
    {
        // Act
        var response = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await response.ReadHtmlAsync();

        // Assert
        var handler = doc.QuerySelector("form input[name='_handler']");
        Assert.NotNull(handler);
        Assert.Equal("shorten", handler.GetAttribute("value"));
        Assert.NotNull(doc.QuerySelector("input[name='Input.Url']"));
    }

    [Fact]
    public async Task Post_ValidUrl_CreatesLinkAndRendersResult()
    {
        // Arrange
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?> { ["Input.Url"] = "https://example.com/destination" }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        var resultDoc = await response.ReadHtmlAsync();
        var resultUrl = resultDoc.QuerySelector("a.result-url");
        Assert.NotNull(resultUrl);
        var href = resultUrl.GetAttribute("href")!;
        Assert.Matches(@"/s/[A-Za-z0-9_-]+$", href);
        var slug = href[(href.LastIndexOf('/') + 1)..];
        Assert.Contains($"src=\"/s/{slug}/qr\"", body);

        await using var session = Fixture.Store.QuerySession();
        var link = await session.LoadAsync<ShortLink>(slug, TestContext.Current.CancellationToken);
        Assert.NotNull(link);
        Assert.Equal("https://example.com/destination", link.TargetUrl);
    }

    [Fact]
    public async Task Post_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        // Arrange — proves the guard is actually on: strip the token from the collected fields.
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["__RequestVerificationToken"] = null,
            }
        );

        // Act
        var response = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Post_CustomAlias_UsesThatSlug()
    {
        // Arrange
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/destination",
                ["Input.CustomAlias"] = "my-link",
            }
        );

        // Act
        await Client.PostAsync("/", content, TestContext.Current.CancellationToken);

        // Assert
        await using var session = Fixture.Store.QuerySession();
        var link = await session.LoadAsync<ShortLink>(
            "my-link",
            TestContext.Current.CancellationToken
        );
        Assert.NotNull(link);
    }

    [Fact]
    public async Task Post_WhenPublicCreationDisabled_RendersPausedNotice()
    {
        // Arrange
        await using (var scope = Fixture.Services.CreateAsyncScope())
        {
            var features = scope.ServiceProvider.GetRequiredService<ShortenerFeatureService>();
            await features.SetPublicCreationEnabledAsync(
                false,
                TestContext.Current.CancellationToken
            );
        }

        try
        {
            // Act
            var response = await Client.GetAsync("/", TestContext.Current.CancellationToken);
            var doc = await response.ReadHtmlAsync();

            // Assert
            Assert.Contains("Public creation is currently disabled", doc.Body!.TextContent);
            Assert.Null(doc.QuerySelector("form.shorten-form"));
        }
        finally
        {
            await using var scope = Fixture.Services.CreateAsyncScope();
            var features = scope.ServiceProvider.GetRequiredService<ShortenerFeatureService>();
            await features.SetPublicCreationEnabledAsync(
                true,
                TestContext.Current.CancellationToken
            );
        }
    }
}
