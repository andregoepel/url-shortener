# Project Instructions — url-shortener

## Project Overview

A simple but effective URL shortener. **Public, anonymous link creation** with abuse guardrails;
link **management behind administrator login** (hybrid model). Features: custom aliases, click
analytics, expiration (date / max-clicks), and per-link QR codes.

Host app composing the [`andregoepel/app-foundation`](https://github.com/andregoepel/app-foundation)
NuGet packages (`AndreGoepel.AppFoundation.Hosting` + `AndreGoepel.AppFoundation` +
`AndreGoepel.Marten.Identity.Blazor`). Identity, the management shell, mail, service defaults and
OpenTelemetry come from those packages — **never re-implement what the foundation provides.**
Orchestrated via .NET Aspire. See `README.md` for the route/feature overview.

**Solution projects:**
- `AndreGoepel.UrlShortener` — Blazor host: public create page, admin area, redirect endpoints
- `AndreGoepel.UrlShortener.AppHost` — .NET Aspire host (Postgres + MailHog)
- `AndreGoepel.UrlShortener.Tests` — xUnit v3 unit tests

## Tech Stack
- .NET 10, Blazor (static SSR public page + InteractiveServer admin), .NET Aspire
- Marten + PostgreSQL (documents), Wolverine (durable messaging — async click recording)
- Radzen (admin UI components), QRCoder (QR PNGs)
- xUnit v3

## Commands
- Build: `dotnet build`
- Test: `dotnet test`
- Run: `dotnet run --project src/AndreGoepel.UrlShortener.AppHost` (needs Docker/Podman)
- Format: `dotnet csharpier format .` (run after every change)

## Language
- All GitHub issues, pull requests, and commit messages are written in **English** (the
  working/chat language may differ).

## Git Workflow
- Branches: `feature/`, `bugfix/`, `hotfix/` (docs: `docs/`, CI/chores: `ci/`)
- Commits: `type: description` (feat, fix, refactor, test, docs)
- **Always create a branch before making any file edits.** Never edit files on `main`.
- **Never commit without explicit user confirmation.** Ask before every commit, no exceptions.
- **Never push to `main` or `master`.** All pushes go to a feature/bugfix/hotfix branch only.
- **Never add a `Co-Authored-By` trailer to commits.** Commit messages contain only the description.
- Run tests before committing.

## Build & Packaging
- Formatting is delegated to **CSharpier** (`dotnet csharpier`); `.editorconfig` handles the rest.
- Central Package Management (`Directory.Packages.props`); lock files on, CI restores in `--locked-mode`.
- `global.json` pins the SDK so the implicit Blazor asset package matches the committed
  `packages.lock.json` (no locked-mode drift). The Aspire AppHost opts out of lock files
  (RID-specific packages): it is restored plainly and excluded from the vulnerability gate.

## Code Conventions

### Naming
- Commands: `Create[Entity]Command`, `Update[Entity]Command`
- Queries: `Get[Entity]Query`, `List[Entities]Query`
- Handlers: `[Command/Query/Message]Handler` (e.g. `LinkClickedHandler`)
- DTOs / requests: `[Entity]Dto`, `Create[Entity]Request`

### Quality
- Use async/await for all I/O; always pass `CancellationToken`.
- Prefer `sealed` types; keep visibility as tight as the test project allows (`internal` +
  `InternalsVisibleTo` where a type need not be public).
- Use bare `default` instead of `default(T)` when the type is inferrable.
- Use `#region` / `#endregion` to group sections, not decorative dash comments.

### Patterns
- Primary constructors for DI (e.g. `ShortLinkService`).
- Records for DTOs, messages, and commands (e.g. `LinkClicked`, `CreateResult`).
- Prefer result objects over exceptions for expected failures (`CreateResult`), not exceptions for flow control.
- File-scoped namespaces.

## Blazor

### Folder Structure
- `Components/Pages/` — routed page components (public `Home.razor`)
- `Components/Pages/Admin/` — administrator pages
- `Components/Layout/` — layout components (`PublicLayout`)
- `Components/` — shared components without a route (`ShortenerAdminMenu`)

### Render modes
- The public `/` page is **static SSR** (no `@rendermode`): no Blazor circuit per anonymous
  visitor, and `HttpContext` is available for IP-based rate limiting and antiforgery. `App.razor`
  selects the render mode by request path — public path static, everything else `InteractiveServer`.
- Admin and identity pages use `@rendermode InteractiveServer`.
- **Static-SSR forms:** `EditForm` auto-emits the antiforgery token — do **not** add an explicit
  `<AntiforgeryToken />` too, or the duplicate field is read as `"token,token"` and rejected.

### Component Rules
- Every routed page has `<PageTitle>`.
- Admin pages use `@attribute [Authorize(Roles = "Administrator")]`, not conditionals in code;
  public pages carry no attribute (anonymous).
- Shared `@using` directives belong in `_Imports.razor`; per-file `@using` only for non-global namespaces.
- Radzen components for admin/management UI; plain HTML/CSS for the public page (highly designed).
- Interactive form models: private `sealed class InputModel` inside `@code` (mutable, not a record).
  The static-SSR public form is the exception — it binds a top-level `CreateInput` via
  `[SupplyParameterFromForm]`.
- Implement `IDisposable` / `IAsyncDisposable` on components that subscribe to events; unsubscribe in `Dispose()`.

## Domain Notes (url-shortener-specific)
- **Identity defaults:** no self-service account creation — `ConfigureIdentity` sets
  `EnableUserRegistration = false` (explicit baseline). The first administrator is created via
  `/Setup`; two-factor and passkeys stay enabled. An admin can still toggle these at runtime on the
  Login Features page (the DB record then overrides the baseline).
- **The slug is the Marten document id** (`ShortLink.Id`) — O(1) redirect lookup and free
  uniqueness. New document types auto-register on first use.
- **Redirects** are anonymous minimal-API endpoints under `/s/{slug}` (+ `/s/{slug}/qr`), never
  behind auth. A disabled or expired link returns `410 Gone`.
- **Click recording is async:** the redirect publishes a `LinkClicked` Wolverine message; the
  handler increments `ClickCount` and stores a `LinkClick`, keeping the redirect hot path cheap.
- **Guardrails for public creation:** http/https scheme allowlist + private/loopback target
  rejection (`UrlValidator`), per-IP rate limiting (`CreateRateLimiter`), random base62 slugs
  (non-enumerable), a reserved-alias list, and an admin kill switch (`ShortLink.IsDisabled`).

## Testing
- Scope: domain logic (URL validation, slug generation, expiry evaluation).
- Naming: `[Method]_[Scenario]_[ExpectedResult]`.
- Files: `[Subject]Tests.cs`; the class name stays `[Subject]Tests`.
- Every test uses `// Arrange`, `// Act`, `// Assert` comments (combine as `// Arrange / Act`
  when inseparable; omit `// Arrange` when there is no setup).
