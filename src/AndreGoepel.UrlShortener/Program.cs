using AndreGoepel.AppFoundation;
using AndreGoepel.AppFoundation.Hosting;
using AndreGoepel.Marten.Identity.Blazor.Components.Account;
using AndreGoepel.UrlShortener;
using AndreGoepel.UrlShortener.Components;
using AndreGoepel.UrlShortener.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// One call wires the data store, identity, messaging, email, data protection, and the
// shared request pipeline (forwarded headers, antiforgery, authn/authz). The connection
// string named "appfoundation-database" is supplied by the Aspire AppHost. Wolverine
// discovers this host's handlers (e.g. LinkClickedHandler) automatically from the entry
// assembly, so no extra discovery wiring is required.
builder.AddAppFoundation();

builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddUrlShortener();

// Brand the management shell and contribute the URL-shortener administration menu.
builder.Services.Configure<AppFoundationLayoutOptions>(options =>
{
    options.BrandName = "url.shortener";
    options.Copyright = $"url.shortener © {DateTime.UtcNow:yyyy}";
    options.AdminMenu = typeof(ShortenerAdminMenu);
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
