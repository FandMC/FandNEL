using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using FandNEL.Core.Extensions;

namespace FandNEL.Core.Utils.Cipher;

public static class TokenUtil
{
	private const string TokenSalt = "0eGsBkhl";

	private static readonly Aes Aes;

	static TokenUtil()
	{
		Aes = System.Security.Cryptography.Aes.Create();
		Aes.Mode = CipherMode.CBC;
		Aes.Padding = PaddingMode.Zeros;
		Aes.KeySize = 128;
		Aes.BlockSize = 128;
		Aes.Key = "debbde3548928fab"u8.ToArray();
		Aes.IV = "afd4c5c5a7c456a1"u8.ToArray();
	}

	public static Dictionary<string, string> ComputeHttpRequestToken(string requestPath, string sendBody, string userId, string userToken)
	{
		return ComputeHttpRequestToken(requestPath, Encoding.UTF8.GetBytes(sendBody), userId, userToken);
	}

	private static Dictionary<string, string> ComputeHttpRequestToken(string requestPath, byte[] sendBody, string userId, string userToken)
	{
		requestPath = (requestPath.StartsWith('/') ? requestPath : ("/" + requestPath));
		using MemoryStream memoryStream = new MemoryStream();
		memoryStream.Write(Encoding.UTF8.GetBytes(userToken.EncodeMd5().ToLowerInvariant()));
		memoryStream.Write(sendBody);
		memoryStream.Write(Encoding.UTF8.GetBytes("0eGsBkhl"));
		memoryStream.Write(Encoding.UTF8.GetBytes(requestPath));
		string digest = memoryStream.ToArray().EncodeMd5().ToLowerInvariant();
		string binaryDigest = HexToBinary(digest);
		string rotatedBinaryDigest = binaryDigest.Substring(6, binaryDigest.Length - 6) + binaryDigest.Substring(0, 6);
		byte[] bytes = Encoding.UTF8.GetBytes(digest);
		ProcessBinaryBlock(rotatedBinaryDigest, bytes);
		string token = (Convert.ToBase64String(bytes, 0, 12) + "1").Replace('+', 'm').Replace('/', 'o');
		return new Dictionary<string, string>
		{
			["user-id"] = userId,
			["user-token"] = token
		};
	}

	private static void ProcessBinaryBlock(string secretBin, byte[] httpToken)
	{
		for (int i = 0; i < secretBin.Length / 8; i++)
		{
			ReadOnlySpan<char> binaryByte = secretBin.AsSpan(i * 8, Math.Min(8, secretBin.Length - i * 8));
			byte mask = 0;
			for (int j = 0; j < binaryByte.Length; j++)
			{
				if (binaryByte[7 - j] == '1')
				{
					mask |= (byte)(1 << j);
				}
			}
			httpToken[i] ^= mask;
		}
	}

	private static string HexToBinary(string hexString)
	{
		StringBuilder builder = new StringBuilder();
		foreach (string bits in hexString.Select(hex => Convert.ToString(hex, 2).PadLeft(8, '0')))
		{
			builder.Append(bits);
		}
		return builder.ToString();
	}

	public static string GenerateEncryptToken(string userToken)
	{
		string randomPrefix = RandomUtil.GetRandomString(8, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789").ToUpper();
		string randomSuffix = RandomUtil.GetRandomString(8, "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789").ToUpper();
		string tokenWithPadding = randomPrefix + userToken + randomSuffix;
		byte[] bytes = Encoding.ASCII.GetBytes(tokenWithPadding);
		return Convert.ToHexString(Aes.CreateEncryptor().TransformFinalBlock(bytes, 0, bytes.Length)).ToUpper();
	}
}
