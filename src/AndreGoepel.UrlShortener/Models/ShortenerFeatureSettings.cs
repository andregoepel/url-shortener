using AndreGoepel.Marten.Configuration;

namespace AndreGoepel.UrlShortener.Models;

/// <summary>
/// Marten document holding the single shortener feature-flag record. When present it takes
/// precedence over the defaults, so an administrator can toggle features at runtime without
/// a redeploy. Registered as a <see cref="SettingsDocument"/> subclass so it shares the
/// foundation's common settings table.
/// </summary>
public sealed class ShortenerFeatureSettings
    : SettingsDocument,
        ISettingsDocument<ShortenerFeatureSettings>
{
    public static string DocumentId => "shortener-feature-settings";

    /// <summary>
    /// Global kill switch for anonymous short-link creation on the public page. Redirects,
    /// QR codes and signed-in creation are unaffected.
    /// </summary>
    public bool AllowPublicCreation { get; set; } = true;
}
