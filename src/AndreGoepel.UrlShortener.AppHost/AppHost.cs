using AndreGoepel.AppFoundation.Aspire;

var builder = DistributedApplication.CreateBuilder(args);

// The E2E suite starts this AppHost with E2E=true so each run gets a clean, throwaway
// database on a dynamic port — never the developer's persistent local data.
var isE2E = string.Equals(builder.Configuration["E2E"], "true", StringComparison.OrdinalIgnoreCase);

// The database resource name is the connection-string name the foundation reads by default
// (AppFoundationOptions.DatabaseConnectionName == "appfoundation-database").
var (_, database) = builder.AddStandardPostgres(
    isE2E,
    serverName: "postgres",
    databaseResourceName: "appfoundation-database",
    databaseName: "urlshortener"
);

// Captures development email locally so the admin setup / password-reset flows work
// without a real mail account.
var mailhog = builder.AddStandardMailHog();

builder
    .AddProject<Projects.AndreGoepel_UrlShortener>("web")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(mailhog)
    // Point the identity EmailSender at MailHog so email keeps working after a database reset
    // with no manual setup. MailHog needs no credentials, but the settings are required.
    .WithEnvironment("EmailSender__SenderName", "url.shortener Dev")
    .WithEnvironment("EmailSender__SenderEmail", "dev@urlshortener.local")
    .WithEnvironment("EmailSender__Server", () => mailhog.GetEndpoint("smtp").Host)
    .WithEnvironment("EmailSender__Port", () => mailhog.GetEndpoint("smtp").Port.ToString())
    .WithEnvironment("EmailSender__UseSsl", "false")
    .WithEnvironment("EmailSender__Username", "dev")
    .WithEnvironment("EmailSender__Password", "dev");

builder.Build().Run();
