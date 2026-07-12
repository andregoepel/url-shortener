using System.Security.Cryptography;

namespace AndreGoepel.UrlShortener.Services;

/// <summary>
/// Generates random base62 slugs. Random (not sequential) so short links can't be
/// enumerated by guessing the next id.
/// </summary>
public sealed class SlugGenerator
{
    private const string Alphabet =
        "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";

    public int Length { get; init; } = 7;

    public string Next()
    {
        Span<byte> bytes = stackalloc byte[Length];
        RandomNumberGenerator.Fill(bytes);

        Span<char> chars = stackalloc char[Length];
        for (var i = 0; i < Length; i++)
        {
            chars[i] = Alphabet[bytes[i] % Alphabet.Length];
        }

        return new string(chars);
    }
}
