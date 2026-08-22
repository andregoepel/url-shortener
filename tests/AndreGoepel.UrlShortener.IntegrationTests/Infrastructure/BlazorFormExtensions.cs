using AngleSharp.Html.Dom;
using AngleSharp.Html.Parser;

namespace AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;

/// <summary>
/// Small AngleSharp helpers so tests read the antiforgery token, the Blazor <c>_handler</c>
/// value, and the <c>Input.*</c> field names from the actual rendered markup instead of
/// hard-coding them — the test then keeps working if Blazor changes its naming, and still
/// fails loudly if the *count* of antiforgery tokens changes (the regression this suite exists
/// to guard).
/// </summary>
internal static class BlazorFormExtensions
{
    private static readonly HtmlParser Parser = new();

    public static async Task<IHtmlDocument> ReadHtmlAsync(this HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return await Parser.ParseDocumentAsync(html, TestContext.Current.CancellationToken);
    }

    public static int CountAntiforgeryTokens(this IHtmlDocument doc) =>
        doc.QuerySelectorAll("input[name='__RequestVerificationToken']").Length;

    /// <summary>
    /// Builds a form POST body from every existing <c>input</c>/<c>select</c> in
    /// <paramref name="formSelector"/> — so <c>_handler</c> and the antiforgery token come along
    /// automatically — then layers <paramref name="overrides"/> on top. A <c>null</c> override
    /// value removes the field entirely (e.g. to simulate a missing antiforgery token), rather
    /// than sending it as an empty string.
    /// </summary>
    public static FormUrlEncodedContent BuildPost(
        this IHtmlDocument doc,
        string formSelector,
        IDictionary<string, string?> overrides
    )
    {
        var form =
            doc.QuerySelector(formSelector)
            ?? throw new InvalidOperationException($"No element matched '{formSelector}'.");

        var fields = new Dictionary<string, string>();
        foreach (var input in form.QuerySelectorAll("input"))
        {
            var name = input.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            fields[name] = input.GetAttribute("value") ?? "";
        }

        foreach (var select in form.QuerySelectorAll("select"))
        {
            var name = select.GetAttribute("name");
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            fields[name] = select.QuerySelector("option[selected]")?.GetAttribute("value") ?? "";
        }

        foreach (var (key, value) in overrides)
        {
            if (value is null)
            {
                fields.Remove(key);
            }
            else
            {
                fields[key] = value;
            }
        }

        return new FormUrlEncodedContent(fields);
    }
}
