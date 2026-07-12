using System.Text.RegularExpressions;
using AndreGoepel.UrlShortener.Services;
using Xunit;

namespace AndreGoepel.UrlShortener.Tests;

public class SlugGeneratorTests
{
    [Fact]
    public void Generates_slug_of_configured_length_and_base62_charset()
    {
        var generator = new SlugGenerator();
        for (var i = 0; i < 1000; i++)
        {
            var slug = generator.Next();
            Assert.Equal(7, slug.Length);
            Assert.Matches(new Regex("^[A-Za-z0-9]+$"), slug);
        }
    }

    [Fact]
    public void Respects_a_custom_length()
    {
        var generator = new SlugGenerator { Length = 12 };
        Assert.Equal(12, generator.Next().Length);
    }

    [Fact]
    public void Produces_effectively_distinct_values()
    {
        var generator = new SlugGenerator();
        var seen = new HashSet<string>();
        for (var i = 0; i < 1000; i++)
        {
            seen.Add(generator.Next());
        }

        // Collisions across 62^7 space are astronomically unlikely at this sample size.
        Assert.True(seen.Count > 995);
    }
}
