# AndreGoepel.UrlShortener.E2ETests

End-to-end tests that drive the **real** app — the Blazor InteractiveServer admin surface, the
public static-SSR create page, PostgreSQL, and the Wolverine click path — through a Chromium
browser.

`AndreGoepel.UrlShortener.IntegrationTests` (`WebApplicationFactory` + Testcontainers) owns
everything anonymous and HTTP-shaped: the create-form antiforgery guard, `/s/{slug}` and
`/s/{slug}/qr`, guardrails, the rate limiter, and click recording. This project exists only for
what a `WebApplicationFactory` cannot drive: `/admin/links` is `@rendermode InteractiveServer`
with no REST surface behind its Radzen grid — the disable/enable/delete actions are C# event
handlers over a SignalR circuit, reachable only by a real browser.

## How it works

- **Aspire.Hosting.Testing** boots the `AppHost` (`src/AndreGoepel.UrlShortener.AppHost`) once
  per test run: PostgreSQL, MailHog, and the Blazor web app (Aspire resource `web`). The AppHost
  is started with `E2E=true`, which drops Postgres' persistent volume and fixed host port so
  every run starts from an empty database on a dynamic port. The fixture waits for `web` to
  become healthy, then reads its `https` endpoint.
- **Microsoft.Playwright** (Chromium) drives the browser. Each test gets a fresh
  `IBrowserContext` so cookies never leak between tests.
- The suite runs **serially** inside one xUnit collection because it shares a single app instance
  and database. The first test that needs it provisions the root administrator exactly once via
  the `/Setup` flow (`E2EAppFixture.ProvisionAdminAsync`, idempotent).

## Prerequisites

1. **A container runtime** must be running — Docker **or** Podman. The tests start real
   containers; if none is reachable the fixture fails fast.
2. **.NET 10 SDK** (the version pinned in `global.json`).
3. **Playwright browsers** installed once, after a build:

   ```bash
   # from the repo root:
   pwsh tests/AndreGoepel.UrlShortener.E2ETests/bin/Debug/net10.0/playwright.ps1 install chromium
   ```

### Using Podman instead of Docker

Aspire's orchestrator auto-detects the runtime, but if Docker Desktop's `docker.exe` is on your
PATH (even with its daemon stopped) it may be picked first. Force Podman per-run:

```bash
dotnet test tests/AndreGoepel.UrlShortener.E2ETests --settings tests/AndreGoepel.UrlShortener.E2ETests/podman.runsettings
```

## Running

```bash
# from the repo root
dotnet test tests/AndreGoepel.UrlShortener.E2ETests
```

Watch the browser (debugging locally):

```bash
E2E_HEADED=true dotnet test tests/AndreGoepel.UrlShortener.E2ETests
```

The main `CI` workflow skips these (`--filter "FullyQualifiedName!~E2ETests"`); they run in the
dedicated `E2E` GitHub Actions workflow, which has Docker available.

## Coverage

| Area | Tests |
| --- | --- |
| Smoke | anonymous create → short link + QR render in the browser; following a short link leaves the app origin; admin login reaches `/admin/links` |
| Admin links | a public create is listed for the admin; search filters the grid; disable/enable/delete toggle the row and the redirect's availability; click count updates after a real redirect (bounded reload-and-retry, since the click is recorded by an async Wolverine handler); an anonymous visitor is bounced to login |

## Tuning notes

The admin grid is built with **Radzen** (`RadzenDataGrid`, icon-only `RadzenButton`s with no
visible text — located by their `Title` attribute, not text). The shared page-interaction core
(`WaitForBlazorAsync`, `FillFieldAsync`, `ClickButtonAsync`, `ClickLinkAsync`,
`AssertOnPathAsync`) and the account flows in `E2ETestBase<TFixture>` come from the
`AndreGoepel.Testing.E2E` package; this repo's own selectors (`ShortenAsync`, `RowForSlug`,
`ClickRowIconAsync`, `ReadClickCountAsync`) live in `Infrastructure/PageExtensions.cs`. Verify
`ProvisionAdminButtonText` on the first headed run after an `AndreGoepel.Marten.Identity.Blazor`
upgrade — a wrong string makes `ProvisionAdminAsync` silently no-op the click instead of
provisioning the admin.

The redirect tests navigate to a real `https://example.com/...` target, since `UrlValidator`
forbids `localhost`/private addresses and a fully offline target is therefore impossible; they
assert only that the browser left the app origin, never on example.com's content.

`AdminLinksTests.CreateLinkAsync` always creates its link through a throwaway **anonymous**
browser context, never the admin's own already-authenticated `Page`. This isn't just realism (an
anonymous visitor and a logged-in administrator are never the same session) — it also works
around a confirmed bug in `AndreGoepel.AppFoundation.Hosting`'s shared middleware pipeline, where
`UseAntiforgery()` runs before `UseAuthentication()`/`UseAuthorization()` (Microsoft's documented
order is the reverse). That ordering rejects a POST to this antiforgery-protected static-SSR form
with "A valid antiforgery token was not provided" whenever the request is already authenticated —
reproduced via a real Playwright run (same antiforgery cookie, exactly one token field, 400 only
when logged in). Flagged upstream in `andregoepel/app-foundation`; once fixed there, this
workaround can be removed, but it is harmless either way.

A failed test writes a Playwright trace to `PLAYWRIGHT_TRACE_DIR` (defaults to
`playwright-traces`), uploaded as a CI artifact by `e2e.yml`.
