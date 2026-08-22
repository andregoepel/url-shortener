using AndreGoepel.Testing.E2E;
using Aspire.Hosting.Testing;

namespace AndreGoepel.UrlShortener.E2ETests.Infrastructure;

#region Fixture

/// <summary>
/// Configures the shared <see cref="AndreGoepel.Testing.E2E.E2EAppFixture"/> for this app: boots the
/// AppHost (Postgres + MailHog + the Blazor web app) with <c>E2E=true</c> so Postgres runs without its
/// persistent volume and on a dynamic port.
/// </summary>
public sealed class E2EAppFixture()
    : AndreGoepel.Testing.E2E.E2EAppFixture(
        new E2EAppFixtureOptions
        {
            CreateAppHostBuilder = args =>
                DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGoepel_UrlShortener_AppHost>(
                    args
                ),
            WebResourceName = "web",
            // The AppFoundation Setup page's submit button reads "Create admin & complete setup";
            // matched on a stable prefix.
            ProvisionAdminButtonText = "Create admin",
            MailHogResourceName = "mailhog",
            // AppHostArguments defaults to ["E2E=true"] — this AppHost needs no secret parameters.
        }
    );

#endregion

#region Collection

[CollectionDefinition(E2ECollectionDefaults.Name)]
public sealed class E2ECollection : ICollectionFixture<E2EAppFixture>;

#endregion
