using AndreGoepel.Marten.Configuration;

namespace AndreGoepel.UrlShortener.Tests;

/// <summary>In-memory <see cref="ISettingsStore"/> holding a single settings document.</summary>
internal sealed class FakeSettingsStore : ISettingsStore
{
    public SettingsDocument? Document { get; set; }

    public int LoadCount { get; private set; }

    public Task<T?> LoadAsync<T>(CancellationToken cancellationToken = default)
        where T : SettingsDocument, ISettingsDocument<T>
    {
        LoadCount++;
        return Task.FromResult(Document as T);
    }

    public Task SaveAsync<T>(T settings, CancellationToken cancellationToken = default)
        where T : SettingsDocument, ISettingsDocument<T>
    {
        settings.Id = T.DocumentId;
        Document = settings;
        return Task.CompletedTask;
    }
}
