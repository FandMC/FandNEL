using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Proxy.Packet.Heypixel;

public static class UserTokenCipher
{
    private const string Alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
    private static readonly byte[] Key = "debbde3548928fab"u8.ToArray();
    private static readonly byte[] Iv = "afd4c5c5a7c456a1"u8.ToArray();

    public static string Encrypt(string token)
    {
        string paddedToken = GenerateRandomText(new Random(), 8).ToUpper()
            + token
            + GenerateRandomText(new Random(), 8).ToUpper();
        byte[] plaintext = Encoding.ASCII.GetBytes(paddedToken);

        using Aes aes = CreateCipher();
        using ICryptoTransform encryptor = aes.CreateEncryptor();
        byte[] ciphertext = encryptor.TransformFinalBlock(plaintext, 0, plaintext.Length);
        return Convert.ToHexString(ciphertext);
    }

    public static string Decrypt(string encryptedToken)
    {
        try
        {
            byte[] ciphertext = Convert.FromHexString(encryptedToken);
            using Aes aes = CreateCipher();
            using ICryptoTransform decryptor = aes.CreateDecryptor();
            byte[] plaintext = decryptor.TransformFinalBlock(ciphertext, 0, ciphertext.Length);
            string paddedToken = Encoding.ASCII.GetString(plaintext).TrimEnd('\0');
            return paddedToken.Length > 16 ? paddedToken[8..^8] : string.Empty;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException("Cannot decrypt token", exception);
        }
    }

    private static Aes CreateCipher()
    {
        Aes aes = Aes.Create();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.Zeros;
        aes.KeySize = 128;
        aes.BlockSize = 128;
        aes.Key = Key;
        aes.IV = Iv;
        return aes;
    }

    private static string GenerateRandomText(Random random, int length) =>
        new(Enumerable.Range(0, length).Select(_ => Alphabet[random.Next(Alphabet.Length)]).ToArray());
}
