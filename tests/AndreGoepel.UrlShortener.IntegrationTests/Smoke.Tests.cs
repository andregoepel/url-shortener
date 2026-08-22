using System.Net;
using AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;
using AndreGoepel.UrlShortener.Models;
using Marten;

namespace AndreGoepel.UrlShortener.IntegrationTests;

/// <summary>The formalised curl smoke test issue #10 asks for: one walk of the full flow.</summary>
public sealed class SmokeTests(UrlShortenerAppFixture fixture) : IntegrationTestBase(fixture)
{
    [Fact]
    public async Task FullFlow_CreateThenRedirectThenQrThenClickCount_Succeeds()
    {
        // Act — GET /
        var home = await Client.GetAsync("/", TestContext.Current.CancellationToken);
        var doc = await home.ReadHtmlAsync();

        // Act — POST /
        var content = doc.BuildPost(
            "form",
            new Dictionary<string, string?>
            {
                ["Input.Url"] = "https://example.com/smoke-test-target",
            }
        );
        var created = await Client.PostAsync("/", content, TestContext.Current.CancellationToken);
        var resultDoc = await created.ReadHtmlAsync();
        var href = resultDoc.QuerySelector("a.result-url")!.GetAttribute("href")!;
        var slug = href[(href.LastIndexOf('/') + 1)..];

        // Act — GET /s/{slug}/qr
        var qr = await Client.GetAsync($"/s/{slug}/qr", TestContext.Current.CancellationToken);

        // Act — tracked GET /s/{slug}
        await ClickAndWaitAsync(slug);

        // Assert
        Assert.Equal(HttpStatusCode.OK, qr.StatusCode);
        Assert.Equal("image/png", qr.Content.Headers.ContentType?.MediaType);

        await using var session = Fixture.Store.QuerySession();
        var link = await session.LoadAsync<ShortLink>(slug, TestContext.Current.CancellationToken);
        Assert.NotNull(link);
        Assert.Equal("https://example.com/smoke-test-target", link.TargetUrl);
        Assert.Equal(1, link.ClickCount);
    }
}
