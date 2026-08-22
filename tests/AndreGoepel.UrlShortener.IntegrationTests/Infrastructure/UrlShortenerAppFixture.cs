using AndreGoepel.Marten.Testing;
using AndreGoepel.UrlShortener.Models;
using Marten;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Testcontainers.PostgreSql;

namespace AndreGoepel.UrlShortener.IntegrationTests.Infrastructure;

/// <summary>
/// The one collection fixture: one Postgres container + one booted app, shared by every test
/// in the assembly. Owns its own <see cref="PostgreSqlContainer"/> rather than composing
/// <c>AndreGoepel.Marten.Testing</c>'s <c>MartenFixture</c> — that fixture doesn't expose its
/// container's connection string, so it cannot point a second consumer (this app's own Marten,
/// booted by <see cref="WebApplicationFactory{TEntryPoint}"/>) at the same database. Still
/// depends on the package purely for <see cref="MartenFixture.PostgresImage"/>, so the pinned
/// Postgres digest stays in lockstep with every other repo in the ecosystem.
/// </summary>
public sealed class UrlShortenerAppFixture : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly int _permitLimit;
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder(
        MartenFixture.PostgresImage
    ).Build();

    /// <summary>Used by xUnit's collection-fixture activator, which needs a zero-arg constructor.</summary>
    public UrlShortenerAppFixture()
        : this(permitLimit: 1000) { }

    /// <summary>
    /// Used by a test that needs a low create-rate-limiter permit count: it constructs its own
    /// dedicated instance rather than reconfiguring the shared collection fixture — see the note
    /// on <see cref="ConfigureWebHost"/>.
    /// </summary>
    internal UrlShortenerAppFixture(int permitLimit)
    {
        _permitLimit = permitLimit;
    }

    /// <summary>The app's own host — used to drive Wolverine's tracking session.</summary>
    public IHost AppHost { get; private set; } = default!;

    /// <summary>The app's own Marten store, so assertions read exactly what the app wrote.</summary>
    public IDocumentStore Store => AppHost.Services.GetRequiredService<IDocumentStore>();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync(TestContext.Current.CancellationToken);
        _ = Server; // forces host creation (and thus CreateHost below)
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        AppHost = base.CreateHost(builder);
        return AppHost;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // Development: AutoCreate.All (schema built on first use), no HSTS / security-header
        // middleware, and EnsureKeyRingProtected does not throw over the unencrypted key ring.
        builder.UseEnvironment("Development");
        builder.UseSetting(
            "ConnectionStrings:appfoundation-database",
            _postgres.GetConnectionString()
        );
        // Raised well above production default (10/min) so the bulk of the suite never trips
        // it. The one test that asserts the limit spins up its own dedicated instance of this
        // fixture with a low permitLimit instead of reconfiguring this shared one: WithWebHostBuilder
        // would call back into this instance's overridden CreateHost (which reassigns AppHost on
        // the shared object), so disposing the derived factory would tear down this fixture's
        // host out from under every other test in the collection.
        builder.UseSetting("Shortener:RateLimit:PermitLimit", _permitLimit.ToString());

        // TestServer leaves Connection.RemoteIpAddress null, so Home.razor would bucket every
        // request under the single "unknown" rate-limit partition. This filter lets a test pick
        // its own client IP via the X-Test-Client-Ip header.
        builder.ConfigureServices(services =>
            services.AddSingleton<IStartupFilter, TestClientIpStartupFilter>()
        );
    }

    /// <summary>Wipes link + click documents between tests without dropping the schema.</summary>
    public async Task ResetAsync(CancellationToken ct = default)
    {
        await using var session = Store.LightweightSession();
        session.DeleteWhere<ShortLink>(_ => true);
        session.DeleteWhere<LinkClick>(_ => true);
        await session.SaveChangesAsync(ct);
    }

    public new async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class IntegrationCollection : ICollectionFixture<UrlShortenerAppFixture>
{
    public const string Name = "Integration";
}
