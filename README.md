# url-shortener

A simple but effective URL shortener built on the
[app-foundation](https://github.com/andregoepel/app-foundation) packages (.NET 10, Blazor
Server, Marten/PostgreSQL, Wolverine, .NET Aspire).

## Model

- **Public, anonymous creation** with guardrails (scheme allowlist, private/loopback target
  rejection, per-IP rate limiting, random non-enumerable slugs, reserved-alias list).
- **Hybrid auth:** anyone can create a link; managing links (dashboard, disable/enable, delete)
  is behind the AppFoundation administrator login.
- **Features:** auto slugs, custom aliases, expiration (date or max clicks), click analytics,
  and per-link QR codes.

## How it fits together

| Path | Render | Auth | Purpose |
|---|---|---|---|
| `/` | static SSR | anonymous | Create a short link + see the result/QR |
| `/s/{slug}` | minimal API | anonymous | 302 redirect (410 when disabled/expired); records a click via Wolverine |
| `/s/{slug}/qr` | minimal API | anonymous | PNG QR code for the short link |
| `/admin/links` | interactive | Administrator | Search, disable/enable, delete links |
| `/Setup`, `/Account/*`, `/dashboard` | interactive | AppFoundation | Identity + management shell |

The slug is the Marten document id, so lookups are O(1) and uniqueness is free. Clicks are
recorded off the redirect hot path by a `LinkClicked` Wolverine handler.

## Run it locally

Requires the .NET 10 SDK and a container runtime (Docker/Podman) for PostgreSQL + MailHog.

```bash
dotnet run --project src/AndreGoepel.UrlShortener.AppHost
```

Open the Aspire dashboard URL, start the **web** resource, and open it. The first visit funnels
you to **/Setup** to create the administrator; after that, `/admin/links` manages every link
while `/` stays open for anonymous shortening.

## Test

```bash
dotnet test
```

Three suites; the first two run by the command above, the third needs its own invocation (see
below):

- `AndreGoepel.UrlShortener.Tests` — pure unit tests: URL validation (scheme / private-IP /
  length), slug generation, and expiry. No I/O, no Docker.
- `AndreGoepel.UrlShortener.IntegrationTests` — `WebApplicationFactory` + a real Postgres
  container (Testcontainers). Covers everything anonymous and HTTP-shaped: the public create
  form (including the antiforgery regression guard — see `PublicCreateFlow.Tests.cs`), the
  guardrails, the rate limiter, `/s/{slug}` redirect + click recording, and `/s/{slug}/qr`.
  Needs Docker/Podman running.
- `AndreGoepel.UrlShortener.E2ETests` — Aspire + Playwright (Chromium) against the real AppHost.
  Covers the InteractiveServer `/admin/links` grid (disable/enable/delete over a SignalR
  circuit — no REST surface, so only a real browser can drive it) plus a browser-level smoke of
  the public create flow. Needs Docker/Podman and, once, `playwright.ps1 install chromium`; run
  separately: `dotnet test tests/AndreGoepel.UrlShortener.E2ETests`. See that project's own
  `README.md` for details.

HTTP-shaped assertions go in `IntegrationTests`; anything needing a live Blazor circuit goes in
`E2ETests`.
