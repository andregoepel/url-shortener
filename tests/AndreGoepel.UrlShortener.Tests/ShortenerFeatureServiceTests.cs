using AndreGoepel.UrlShortener.Models;
using AndreGoepel.UrlShortener.Services;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace AndreGoepel.UrlShortener.Tests;

public sealed class ShortenerFeatureServiceTests : IDisposable
{
    private readonly FakeSettingsStore _store = new();
    private readonly MemoryCache _cache = new(new MemoryCacheOptions());
    private readonly ShortenerFeatureService _service;

    public ShortenerFeatureServiceTests() => _service = new ShortenerFeatureService(_store, _cache);

    public void Dispose() => _cache.Dispose();

    [Fact]
    public async Task IsPublicCreationEnabledAsync_NoSavedSettings_DefaultsToTrue()
    {
        // Act
        var enabled = await _service.IsPublicCreationEnabledAsync(
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.True(enabled);
    }

    [Fact]
    public async Task IsPublicCreationEnabledAsync_SavedDisabled_ReturnsFalse()
    {
        // Arrange
        _store.Document = new ShortenerFeatureSettings { AllowPublicCreation = false };

        // Act
        var enabled = await _service.IsPublicCreationEnabledAsync(
            TestContext.Current.CancellationToken
        );

        // Assert
        Assert.False(enabled);
    }

    [Fact]
    public async Task IsPublicCreationEnabledAsync_SecondCall_IsServedFromCache()
    {
        // Arrange
        await _service.IsPublicCreationEnabledAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.IsPublicCreationEnabledAsync(TestContext.Current.CancellationToken);

        // Assert
        Assert.Equal(1, _store.LoadCount);
    }

    [Fact]
    public async Task SetPublicCreationEnabledAsync_AfterCachedRead_TakesEffectImmediately()
    {
        // Arrange
        await _service.IsPublicCreationEnabledAsync(TestContext.Current.CancellationToken);

        // Act
        await _service.SetPublicCreationEnabledAsync(false, TestContext.Current.CancellationToken);

        // Assert
        Assert.False(
            await _service.IsPublicCreationEnabledAsync(TestContext.Current.CancellationToken)
        );
        var saved = Assert.IsType<ShortenerFeatureSettings>(_store.Document);
        Assert.False(saved.AllowPublicCreation);
    }

    [Fact]
    public async Task SetPublicCreationEnabledAsync_ReEnabled_PersistsAndServesTrue()
    {
        // Arrange
        _store.Document = new ShortenerFeatureSettings { AllowPublicCreation = false };

        // Act
        await _service.SetPublicCreationEnabledAsync(true, TestContext.Current.CancellationToken);

        // Assert
        Assert.True(
            await _service.IsPublicCreationEnabledAsync(TestContext.Current.CancellationToken)
        );
        var saved = Assert.IsType<ShortenerFeatureSettings>(_store.Document);
        Assert.True(saved.AllowPublicCreation);
    }
}
