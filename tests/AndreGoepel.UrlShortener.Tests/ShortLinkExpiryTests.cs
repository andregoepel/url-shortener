using AndreGoepel.UrlShortener.Models;
using Xunit;

namespace AndreGoepel.UrlShortener.Tests;

public class ShortLinkExpiryTests
{
    private static readonly DateTimeOffset Now = new(2026, 7, 5, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Not_expired_when_no_limits_set() => Assert.False(new ShortLink().IsExpired(Now));

    [Fact]
    public void Expired_when_expiry_is_in_the_past() =>
        Assert.True(new ShortLink { ExpiresAtUtc = Now.AddMinutes(-1) }.IsExpired(Now));

    [Fact]
    public void Not_expired_when_expiry_is_in_the_future() =>
        Assert.False(new ShortLink { ExpiresAtUtc = Now.AddMinutes(1) }.IsExpired(Now));

    [Fact]
    public void Expired_when_click_limit_is_reached() =>
        Assert.True(new ShortLink { MaxClicks = 5, ClickCount = 5 }.IsExpired(Now));

    [Fact]
    public void Not_expired_below_click_limit() =>
        Assert.False(new ShortLink { MaxClicks = 5, ClickCount = 4 }.IsExpired(Now));
}
