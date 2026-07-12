using AndreGoepel.AppFoundation;
using AndreGoepel.AppFoundation.Hosting;
using AndreGoepel.Marten.Identity.Blazor.Components.Account;
using AndreGoepel.UrlShortener;
using AndreGoepel.UrlShortener.Components;
using AndreGoepel.UrlShortener.Endpoints;
using AndreGoepel.UrlShortener.Messaging;

var builder = WebApplication.CreateBuilder(args);

// One call wires the data store, identity, messaging, email, data protection, and the
// shared request pipeline (forwarded headers, antiforgery, authn/authz). The connection
// string named "appfoundation-database" is supplied by the Aspire AppHost.
builder.AddAppFoundation(options =>
{
    // Opt this host's Wolverine handlers (e.g. LinkClickedHandler) into discovery. The
    // foundation otherwise only includes its own MailService handler assembly.
    options.ConfigureWolverine = wolverine =>
        wolverine.Discovery.IncludeAssembly(typeof(LinkClicked).Assembly);

    // Public shortener, hybrid model: no self-service account creation. Only the
    // administrator (created once via /Setup) signs in; link management lives behind that
    // login. Set explicitly rather than relying on the foundation default. Two-factor and
    // passkeys stay enabled for the administrator account. This is the baseline default —
    // an administrator can still toggle it at runtime on the Login Features page.
    options.ConfigureIdentity = identity => identity.EnableUserRegistration = false;
});

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddUrlShortener();

// Brand the management shell and contribute the URL-shortener administration menu.
builder.Services.Configure<AppFoundationLayoutOptions>(options =>
{
    options.BrandName = "url.shortener";
    options.Copyright = $"url.shortener © {DateTime.UtcNow:yyyy}";
    options.Menu = typeof(ShortenerAdminMenu);
});

var app = builder.Build();

app.UseAppFoundation();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode()
    .AddAdditionalAssemblies(
        // The AppFoundation management UI (layout, setup, dashboard, admin pages) …
        typeof(AppFoundationLayoutOptions).Assembly,
        // … and the Marten.Identity account pages (login, register, account management).
        typeof(AndreGoepel.Marten.Identity.Blazor.Initialization).Assembly
    );

app.MapAdditionalIdentityEndpoints();

// Anonymous redirect + QR endpoints for short links (never behind auth).
app.MapShortenerEndpoints();

app.Run();
