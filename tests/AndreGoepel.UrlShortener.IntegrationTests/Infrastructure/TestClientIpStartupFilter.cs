using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;

namespace AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;

/// <summary>
/// <c>TestServer</c> leaves every request's <c>Connection.RemoteIpAddress</c> null, which would
/// bucket every test under the single "unknown" rate-limit partition and exhaust the create
/// limit after ~10 requests for the whole suite. Lets a test stamp its own client IP via a
/// header instead.
/// </summary>
internal sealed class TestClientIpStartupFilter : IStartupFilter
{
    public const string HeaderName = "X-Test-Client-Ip";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) =>
        app =>
        {
            app.Use(
                (context, continuation) =>
                {
                    if (
                        context.Request.Headers.TryGetValue(HeaderName, out var value)
                        && IPAddress.TryParse(value.ToString(), out var ip)
                    )
                    {
                        context.Connection.RemoteIpAddress = ip;
                    }

                    return continuation();
                }
            );
            next(app);
        };
}
