using System;
using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Extensions;

public static class MPayExtensions
{
	public static string EncodeMd5(this string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return string.Empty;
		}
		return Encoding.UTF8.GetBytes(input).EncodeMd5();
	}

	public static string EncodeMd5(this byte[] inputBytes)
	{
		return MD5.HashData(inputBytes).EncodeHex();
	}

	public static string EncodeBase64(this string input)
	{
		if (string.IsNullOrEmpty(input))
		{
			return string.Empty;
		}
		return Convert.ToBase64String(Encoding.UTF8.GetBytes(input));
	}

	public static string EncodeHex(this byte[] input)
	{
		return Convert.ToHexString(input).Replace("-", "").ToLower();
	}

	public static byte[] DecodeHex(this string input)
	{
		return Convert.FromHexString(input);
	}

	public static byte[] EncodeAes(this string input, byte[] key)
	{
		using Aes aes = Aes.Create();
		aes.Key = key;
		aes.Mode = CipherMode.ECB;
		aes.Padding = PaddingMode.PKCS7;
		using ICryptoTransform cryptoTransform = aes.CreateEncryptor();
		byte[] bytes = Encoding.UTF8.GetBytes(input);
		return cryptoTransform.TransformFinalBlock(bytes, 0, bytes.Length);
	}
}
