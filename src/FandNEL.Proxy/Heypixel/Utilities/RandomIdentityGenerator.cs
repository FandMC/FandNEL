using System.Text;

namespace FandNEL.Proxy.Heypixel;

public static class RandomIdentityGenerator
{
    private const string AlphaNumericCharacters = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private const string Digits = "0123456789";

    public static string GenerateHex(Random random, int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        random.NextBytes(bytes);
        return Convert.ToHexString(bytes);
    }

    public static string GenerateAlphaNumeric(Random random, int length) =>
        GenerateCharacters(random, length, AlphaNumericCharacters);

    public static string GenerateDigits(Random random, int length) =>
        GenerateCharacters(random, length, Digits);

    public static string GenerateMacAddress(Random random, int byteCount)
    {
        byte[] bytes = new byte[byteCount];
        random.NextBytes(bytes);
        bytes[0] &= 0xFE;
        return string.Join(':', bytes.Select(item => item.ToString("x2")));
    }

    public static string GeneratePrivateIpv4Address(Random random)
    {
        (int first, int second) = random.Next(0, 3) switch
        {
            0 => (10, random.Next(0, 256)),
            1 => (172, random.Next(16, 32)),
            _ => (192, 168)
        };
        return $"{first}.{second}.{random.Next(0, 256)}.{random.Next(1, 255)}";
    }

    public static string GenerateIpv6InterfaceIdentifier(Random random)
    {
        StringBuilder result = new(32);
        for (int group = 0; group < 4; group++)
        {
            byte[] bytes = new byte[2];
            random.NextBytes(bytes);
            bytes[0] &= 0xFE;
            if (result.Length > 0)
            {
                result.Append(':');
            }

            foreach (byte item in bytes)
            {
                result.Append(item.ToString("x2"));
            }
        }

        return result.ToString();
    }

    private static string GenerateCharacters(Random random, int length, string alphabet)
    {
        char[] characters = new char[length];
        for (int index = 0; index < characters.Length; index++)
        {
            characters[index] = alphabet[random.Next(alphabet.Length)];
        }

        return new string(characters);
    }
}
