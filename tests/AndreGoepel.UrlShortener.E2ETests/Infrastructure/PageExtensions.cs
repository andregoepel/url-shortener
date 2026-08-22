namespace AndreGoepel.Testing.E2E;

/// <summary>
/// App-specific page helpers layered on top of <see cref="AndreGoepel.Testing.E2E.PageExtensions"/>'s
/// verbatim-identical core: the public create form, the admin Radzen grid, and the QR result —
/// deliberately kept local rather than forked into the shared package (see that package's
/// <c>PageExtensions</c> XML docs).
/// </summary>
public static class UrlShortenerPageExtensions
{
    /// <summary>
    /// Fills and submits the public create form on <c>/</c>, then reads the produced slug out of
    /// <c>a.result-url</c>. Assumes the page is already on <c>/</c>.
    /// </summary>
    public static async Task<string> ShortenAsync(this IPage page, string url, string? alias = null)
    {
        await page.FillFieldAsync("Input.Url", url);
        if (alias is not null)
        {
            await page.Locator("details.options summary").ClickAsync();
            await page.FillFieldAsync("Input.CustomAlias", alias);
        }

        await page.ClickButtonAsync("Shorten");
        await page.WaitForSelectorAsync("a.result-url");

        var resultUrl = await page.Locator("a.result-url").InnerTextAsync();
        return new Uri(resultUrl).Segments[^1];
    }

    /// <summary>The admin grid row for a given slug, identified by its <c>&lt;code&gt;</c> slug cell.</summary>
    public static ILocator RowForSlug(this IPage page, string slug) =>
        page.Locator(
            "tr",
            new PageLocatorOptions { Has = page.Locator($"code:text-is('{slug}')") }
        );

    /// <summary>
    /// Clicks the row's icon-only <c>RadzenButton</c> by its <c>Title</c> attribute (e.g.
    /// <c>"Disable"</c>, <c>"Enable"</c>, <c>"Delete"</c>) — these buttons carry no visible text.
    /// </summary>
    public static Task ClickRowIconAsync(this ILocator row, string title) =>
        row.Locator($"[title='{title}']").ClickAsync();

    /// <summary>Reads the "Clicks" column of the admin grid row for the given slug.</summary>
    public static async Task<long> ReadClickCountAsync(this IPage page, string slug)
    {
        var cells = page.RowForSlug(slug).Locator("td");
        var text = await cells.Nth(2).InnerTextAsync();
        return long.Parse(text.Trim());
    }
}
