namespace AndreGoepel.UrlShortener.E2ETests.Tests;

/// <summary>
/// The InteractiveServer admin surface (<c>/admin/links</c>) — disable/enable/delete are C# event
/// handlers over a SignalR circuit with no REST surface, so only a real browser can drive them.
/// This is the reason this project exists; everything HTTP-shaped lives in
/// <c>AndreGoepel.UrlShortener.IntegrationTests</c> instead.
/// </summary>
public sealed class AdminLinksTests(E2EAppFixture fixture) : E2ETestBase<E2EAppFixture>(fixture)
{
    /// <summary>
    /// Creates a link through a throwaway anonymous browser context, never the admin's own
    /// authenticated <see cref="E2ETestBase{TFixture}.Page"/>. This matches how the two roles
    /// actually meet in production — an anonymous visitor and a logged-in administrator are never
    /// the same session — and also sidesteps a confirmed AppFoundation bug where the antiforgery
    /// middleware runs before authentication, which rejects this form's POST for an already
    /// signed-in user (see the E2ETests README's Tuning notes).
    /// </summary>
    private async Task<string> CreateLinkAsync(string url, string? alias = null)
    {
        await using var anonymousContext = await Fixture.NewContextAsync();
        var anonymousPage = await anonymousContext.NewPageAsync();
        await anonymousPage.GotoAsync("/");
        await anonymousPage.WaitForBlazorAsync();
        return await anonymousPage.ShortenAsync(url, alias);
    }

    private async Task GotoAdminLinksAsync()
    {
        await Page.GotoAsync("/admin/links");
        await Page.WaitForBlazorAsync();
    }

    [Fact]
    public async Task AdminLinks_AfterPublicCreate_ListsTheNewLink()
    {
        // Arrange
        var slug = await CreateLinkAsync("https://example.com/admin-list-target");

        // Act
        await LoginAsAdminAsync();
        await GotoAdminLinksAsync();

        // Assert
        var row = Page.RowForSlug(slug);
        await Expect(row).ToContainTextAsync("https://example.com/admin-list-target");
    }

    [Fact]
    public async Task AdminLinks_Search_FiltersToMatchingSlug()
    {
        // Arrange
        await LoginAsAdminAsync();
        var firstSlug = await CreateLinkAsync("https://example.com/search-target-one");
        var secondSlug = await CreateLinkAsync("https://example.com/search-target-two");
        await GotoAdminLinksAsync();

        // Act — the RadzenTextBox binds via @bind-Value, not an EditForm field, so it renders no
        // "name" attribute; locate it by its placeholder instead of FillFieldAsync.
        await Page.GetByPlaceholder("Search slug or URL").FillAsync(firstSlug);
        await Page.ClickButtonAsync("Search");

        // Assert
        await Expect(Page.RowForSlug(firstSlug)).ToBeVisibleAsync();
        await Expect(Page.RowForSlug(secondSlug)).Not.ToBeVisibleAsync();
    }

    [Fact]
    public async Task AdminLinks_Disable_MarksRowDisabledAndRedirectReturns410()
    {
        // Arrange
        await LoginAsAdminAsync();
        var slug = await CreateLinkAsync("https://example.com/disable-target");
        await GotoAdminLinksAsync();

        // Act
        await Page.RowForSlug(slug).ClickRowIconAsync("Disable");

        // Assert
        await Expect(Page.RowForSlug(slug)).ToContainTextAsync("Disabled");
        var response = await Page.GotoAsync($"/s/{slug}");
        Assert.NotNull(response);
        Assert.Equal(410, response.Status);
    }

    [Fact]
    public async Task AdminLinks_Enable_RestoresTheRedirect()
    {
        // Arrange
        await LoginAsAdminAsync();
        var slug = await CreateLinkAsync("https://example.com/enable-target");
        await GotoAdminLinksAsync();
        await Page.RowForSlug(slug).ClickRowIconAsync("Disable");
        await Expect(Page.RowForSlug(slug)).ToContainTextAsync("Disabled");

        // Act
        await Page.RowForSlug(slug).ClickRowIconAsync("Enable");

        // Assert — active again, so the redirect target is reachable once more (not a
        // Blazor page; the extension method's Blazor wait is deliberately skipped).
        await Expect(Page.RowForSlug(slug)).ToContainTextAsync("Active");
        var appOrigin = new Uri(Fixture.AppBaseUrl).Host;
        await Page.GotoAsync($"/s/{slug}");
        await Page.WaitForURLAsync(url => new Uri(url).Host != appOrigin);
        Assert.NotEqual(appOrigin, new Uri(Page.Url).Host);
    }

    [Fact]
    public async Task AdminLinks_Delete_RemovesRowAndRedirectReturns410()
    {
        // Arrange
        await LoginAsAdminAsync();
        var slug = await CreateLinkAsync("https://example.com/delete-target");
        await GotoAdminLinksAsync();

        // Act
        await Page.RowForSlug(slug).ClickRowIconAsync("Delete");

        // Assert
        await Expect(Page.RowForSlug(slug)).Not.ToBeVisibleAsync();
        var response = await Page.GotoAsync($"/s/{slug}");
        Assert.NotNull(response);
        Assert.Equal(410, response.Status);
    }

    [Fact]
    public async Task AdminLinks_AfterRedirect_ClickCountBecomesOne()
    {
        // Arrange
        await LoginAsAdminAsync();
        var slug = await CreateLinkAsync("https://example.com/click-count-target");

        // Act — the click is recorded by an async Wolverine handler, so the grid needs a reload
        // loop rather than a single read; bounded so a genuine failure still fails fast.
        await using var visitorContext = await Fixture.NewContextAsync();
        var visitorPage = await visitorContext.NewPageAsync();
        await visitorPage.GotoAsync($"/s/{slug}");

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await GotoAdminLinksAsync();
            if (await Page.ReadClickCountAsync(slug) == 1)
            {
                return;
            }

            await Task.Delay(500, TestContext.Current.CancellationToken);
        }

        // Assert
        Assert.Fail($"Click count for /{slug} never reached 1.");
    }

    [Fact]
    public async Task AdminLinks_AnonymousVisitor_RedirectsToLogin()
    {
        // Act
        await Page.GotoAsync("/admin/links");
        await Page.WaitForBlazorAsync();

        // Assert
        await Page.AssertOnPathAsync("Account/Login");
    }
}
