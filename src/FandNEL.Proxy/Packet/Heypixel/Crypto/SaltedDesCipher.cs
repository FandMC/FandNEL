using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Proxy.Packet.Heypixel;

public class SaltedDesCipher
{
    private const int SaltLength = 8;
    private const int DerivationRounds = 1000;
    private readonly string _password;

    public SaltedDesCipher(string password)
    {
        _password = password;
    }

    public string DecryptString(string encryptedValue)
    {
        byte[] plaintext = Decrypt(Convert.FromBase64String(encryptedValue));
        return Encoding.UTF8.GetString(plaintext);
    }

    public string EncryptString(string value) =>
        Convert.ToBase64String(Encrypt(Encoding.UTF8.GetBytes(value)));

    public byte[] Encrypt(byte[] plaintext)
    {
        if (string.IsNullOrEmpty(_password))
        {
            throw new InvalidOperationException("Heypixel DES password has not been initialized.");
        }

        byte[] salt = RandomNumberGenerator.GetBytes(SaltLength);
        (byte[] key, byte[] iv) = DeriveKeyAndIv(_password, salt, DerivationRounds);

        using DES des = DES.Create();
        des.Key = key;
        des.IV = iv;
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.PKCS7;

        using ICryptoTransform encryptor = des.CreateEncryptor();
        using MemoryStream output = new();
        using (CryptoStream cryptoStream = new(output, encryptor, CryptoStreamMode.Write, leaveOpen: true))
        {
            cryptoStream.Write(plaintext, 0, plaintext.Length);
            cryptoStream.FlushFinalBlock();
        }

        byte[] ciphertext = output.ToArray();
        byte[] result = new byte[salt.Length + ciphertext.Length];
        Buffer.BlockCopy(salt, 0, result, 0, salt.Length);
        Buffer.BlockCopy(ciphertext, 0, result, salt.Length, ciphertext.Length);
        return result;
    }

    public byte[] Decrypt(byte[] encryptedValue)
    {
        if (string.IsNullOrEmpty(_password))
        {
            throw new InvalidOperationException("Heypixel DES password has not been initialized.");
        }

        if (encryptedValue.Length <= SaltLength || (encryptedValue.Length - SaltLength) % 8 != 0)
        {
            throw new ArgumentException("Heypixel DES ciphertext requires an 8-byte salt and complete blocks.", nameof(encryptedValue));
        }

        byte[] salt = encryptedValue[..SaltLength];
        byte[] ciphertext = encryptedValue[SaltLength..];
        (byte[] key, byte[] iv) = DeriveKeyAndIv(_password, salt, DerivationRounds);

        using DES des = DES.Create();
        des.Key = key;
        des.IV = iv;
        des.Mode = CipherMode.CBC;
        des.Padding = PaddingMode.PKCS7;

        using ICryptoTransform decryptor = des.CreateDecryptor();
        using MemoryStream input = new(ciphertext);
        using CryptoStream cryptoStream = new(input, decryptor, CryptoStreamMode.Read);
        using MemoryStream output = new();
        cryptoStream.CopyTo(output);
        return output.ToArray();
    }

    private static (byte[] Key, byte[] Iv) DeriveKeyAndIv(string password, byte[] salt, int rounds)
    {
        byte[] passwordBytes = Encoding.UTF8.GetBytes(password);
        byte[] seed = new byte[passwordBytes.Length + salt.Length];
        Buffer.BlockCopy(passwordBytes, 0, seed, 0, passwordBytes.Length);
        Buffer.BlockCopy(salt, 0, seed, passwordBytes.Length, salt.Length);

        byte[] digest = MD5.HashData(seed);
        for (int round = 1; round < rounds; round++)
        {
            digest = MD5.HashData(digest);
        }

        if (digest.Length < 16)
        {
            byte[] expandedDigest = new byte[16];
            Buffer.BlockCopy(digest, 0, expandedDigest, 0, digest.Length);
            byte[] nextDigest = digest;
            for (int offset = digest.Length; offset < expandedDigest.Length;)
            {
                nextDigest = MD5.HashData(nextDigest);
                int copyLength = Math.Min(nextDigest.Length, expandedDigest.Length - offset);
                Buffer.BlockCopy(nextDigest, 0, expandedDigest, offset, copyLength);
                offset += copyLength;
            }

            digest = expandedDigest;
        }

        return (digest[..8], digest[8..16]);
    }
}
