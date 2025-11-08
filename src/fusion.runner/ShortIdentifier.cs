using System.Security.Cryptography;

namespace Fusion.Runner;

public static class ShortIdentifier
{
    private const string Alphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";
    private const int DefaultLength = 8;

    public static string New(int length = DefaultLength)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(length, 4);

        Span<char> buffer = stackalloc char[length];
        Span<byte> randomBytes = stackalloc byte[length];

        RandomNumberGenerator.Fill(randomBytes);

        for (var i = 0; i < length; i++)
        {
            buffer[i] = Alphabet[randomBytes[i] % Alphabet.Length];
        }

        return new string(buffer);
    }
}
