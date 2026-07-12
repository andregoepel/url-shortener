var builder = DistributedApplication.CreateBuilder(args);

// A PostgreSQL container with a persistent volume so setup, accounts, and links survive
// restarts. The database resource name is the connection-string name the foundation reads by
// default (AppFoundationOptions.DatabaseConnectionName == "appfoundation-database").
var postgres = builder.AddPostgres("postgres").WithDataVolume();
var database = postgres.AddDatabase("appfoundation-database", "urlshortener");

// MailHog captures development email locally (SMTP on 1025, web UI on 8025) so the admin
// setup / password-reset flows work without a real mail account.
var mailhog = builder
    .AddContainer("mailhog", "mailhog/mailhog", "v1.0.1")
    .WithEndpoint(name: "smtp", port: 1025, targetPort: 1025)
    .WithHttpEndpoint(name: "http", port: 8025, targetPort: 8025);

builder
    .AddProject<Projects.AndreGoepel_UrlShortener>("web")
    .WithReference(database)
    .WaitFor(database)
    .WaitFor(mailhog)
    // Point the identity EmailSender at MailHog so email keeps working after a database reset
    // with no manual setup. MailHog needs no credentials, but the settings are required.
    .WithEnvironment("EmailSender__SenderName", "url.shortener Dev")
    .WithEnvironment("EmailSender__SenderEmail", "dev@urlshortener.local")
    .WithEnvironment("EmailSender__Server", "localhost")
    .WithEnvironment("EmailSender__Port", "1025")
    .WithEnvironment("EmailSender__UseSsl", "false")
    .WithEnvironment("EmailSender__Username", "dev")
    .WithEnvironment("EmailSender__Password", "dev");

builder.Build().Run();
