using QRCoder;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Renders a short link's URL as a PNG QR code. Uses QRCoder's <see cref="PngByteQRCode"/>,
/// which is pure managed code (no System.Drawing) so it works cross-platform in a container.
/// </summary>
public sealed class QrCodeService
{
    public byte[] GeneratePng(string text, int pixelsPerModule = 8)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(text, QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(data);
        return png.GetGraphic(pixelsPerModule);
    }
}
