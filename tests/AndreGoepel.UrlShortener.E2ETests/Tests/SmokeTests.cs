namespace AndreGoepel.UrlShortener.E2ETests.Tests;

/// <summary>Fast confidence checks that the harness boots the app and the core happy path works.</summary>
public sealed class SmokeTests(E2EAppFixture fixture) : E2ETestBase<E2EAppFixture>(fixture)
{
    [Fact]
    public async Task PublicHome_ShortenValidUrl_ShowsShortLinkAndQr()
    {
        // Arrange
        await Page.GotoAsync("/");
        await Page.WaitForBlazorAsync();

        // Act
        var slug = await Page.ShortenAsync("https://example.com/e2e-smoke-target");

        // Assert
        await Expect(Page.Locator("a.result-url")).ToContainTextAsync($"/s/{slug}");
        var naturalWidth = await Page.Locator("img.result-qr")
            .EvaluateAsync<int>("img => img.naturalWidth");
        Assert.True(
            naturalWidth > 0,
            "Expected the QR <img> to render a real, non-zero-width PNG."
        );
    }

    [Fact]
    public async Task Redirect_FollowShortLink_LeavesTheAppOrigin()
    {
        // Arrange — a real outbound target, since UrlValidator forbids localhost/private
        // addresses so a fully offline target is impossible. Assert only that the browser left
        // the app (not on example.com's content) per the E2E project's no-network exception.
        await Page.GotoAsync("/");
        await Page.WaitForBlazorAsync();
        var slug = await Page.ShortenAsync("https://example.com/e2e-redirect-target");
        var appOrigin = new Uri(Fixture.AppBaseUrl).Host;

        // Act — the redirect target is a plain (non-Blazor) page, so this deliberately does not
        // call WaitForBlazorAsync afterwards.
        await Page.GotoAsync($"/s/{slug}");
        await Page.WaitForURLAsync(url => new Uri(url).Host != appOrigin);

        // Assert
        Assert.NotEqual(appOrigin, new Uri(Page.Url).Host);
    }

    [Fact]
    public async Task Admin_CanLogIn_AndReachLinksPage()
    {
        // Arrange / Act
        await LoginAsAdminAsync();
        await Page.GotoAsync("/admin/links");
        await Page.WaitForBlazorAsync();

        // Assert
        await Expect(Page.GetByRole(AriaRole.Heading, new() { Name = "Short links" }).First)
            .ToBeVisibleAsync();
    }
}
