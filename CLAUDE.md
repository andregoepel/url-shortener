# CLAUDE.md — url-shortener

A URL shortener consuming the **app-foundation** NuGet packages (.NET 10 / Blazor Server /
Marten / Wolverine / Aspire). See `README.md` for the feature and route overview.

## Layout

- `src/AndreGoepel.UrlShortener` — the Blazor Server host (Web SDK).
  - `Program.cs` — `AddAppFoundation` (+ `ConfigureWolverine` to discover this host's handlers),
    `AddUrlShortener`, layout branding, `MapShortenerEndpoints`.
  - `Components/` — `App.razor` (static SSR for `/`, interactive elsewhere), `Routes.razor`,
    public `Pages/Home.razor`, `Pages/Admin/Links.razor`, `ShortenerAdminMenu.razor`.
  - `Services/` — `ShortLinkService` (Marten sessions), `SlugGenerator`, `UrlValidator`,
    `QrCodeService`, `CreateRateLimiter`.
  - `Endpoints/RedirectEndpoints.cs` — anonymous `/s/{slug}` + `/s/{slug}/qr`.
  - `Messaging/` — `LinkClicked` + handler (async click recording).
- `src/AndreGoepel.UrlShortener.AppHost` — Aspire orchestrator (Postgres + MailHog + web).
- `tests/AndreGoepel.UrlShortener.Tests` — xUnit v3 unit tests.

## Conventions

- Formatting is delegated to **CSharpier** (`dotnet csharpier`); `.editorconfig` handles the rest.
- Central Package Management (`Directory.Packages.props`); lock files on (`--locked-mode` in CI).
- The slug is the Marten document id. New document types auto-register on first use.
- Public pages are anonymous (no `[Authorize]`); admin pages use
  `[Authorize(Roles = "Administrator")]`. Keep the public `/` page static SSR so anonymous
  visitors don't each open a Blazor circuit and IP-based rate limiting can read `HttpContext`.

## Build & test

```bash
dotnet build
dotnet test
dotnet run --project src/AndreGoepel.UrlShortener.AppHost   # needs Docker/Podman
```
