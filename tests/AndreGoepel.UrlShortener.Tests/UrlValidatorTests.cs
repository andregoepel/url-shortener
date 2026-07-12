using AndreGoepel.UrlShortener.Services;
using Xunit;

namespace AndreGoepel.UrlShortener.Tests;

public class UrlValidatorTests
{
    private readonly UrlValidator _validator = new();

    [Theory]
    [InlineData("https://example.com/path?q=1")]
    [InlineData("http://example.com")]
    [InlineData("https://sub.example.co.uk/a/b/c")]
    public void Accepts_public_http_and_https(string url)
    {
        Assert.True(_validator.TryValidate(url, out var normalized, out var error));
        Assert.Null(error);
        Assert.StartsWith("http", normalized);
    }

    [Theory]
    [InlineData("javascript:alert(1)")]
    [InlineData("data:text/html,<script>alert(1)</script>")]
    [InlineData("ftp://example.com/file")]
    [InlineData("file:///etc/passwd")]
    [InlineData("mailto:someone@example.com")]
    [InlineData("not a url")]
    [InlineData("")]
    [InlineData("   ")]
    public void Rejects_disallowed_schemes_and_garbage(string? url)
    {
        Assert.False(_validator.TryValidate(url, out _, out var error));
        Assert.NotNull(error);
    }

    [Theory]
    [InlineData("http://127.0.0.1/admin")]
    [InlineData("http://localhost:8080")]
    [InlineData("https://foo.localhost")]
    [InlineData("http://10.0.0.5")]
    [InlineData("http://172.16.4.9")]
    [InlineData("http://192.168.1.1")]
    [InlineData("http://169.254.169.254/latest/meta-data")]
    [InlineData("http://[::1]/")]
    public void Rejects_loopback_and_private_targets(string url)
    {
        Assert.False(_validator.TryValidate(url, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void Rejects_urls_over_the_length_limit()
    {
        var url = "https://example.com/" + new string('a', UrlValidator.MaxUrlLength);
        Assert.False(_validator.TryValidate(url, out _, out _));
    }

    [Fact]
    public void Accepts_a_public_ip_literal()
    {
        Assert.True(_validator.TryValidate("http://93.184.216.34/", out _, out _));
    }
}
