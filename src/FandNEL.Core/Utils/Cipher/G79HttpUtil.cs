using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Utils.Cipher;

public static class G79HttpUtil
{
	private const string StartTypeTail = "abcdefghijklmnopqrstuvwxyz";
	private const string RandomCharacters = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

	private static readonly byte[][] G79Keys =
	{
		Convert.FromHexString("60F1E0D1FD635362430747215CF1C2FF"),
		Convert.FromHexString("EA5B62D27D0338374852C4B9469D7AC6"),
		Convert.FromHexString("17238D55501C5F020B155FB3303591E6"),
		Convert.FromHexString("8C5CEAE0F443E006A050266F73ADD5B0"),
		Convert.FromHexString("1C02CE22FB22F0E72060217418F351F3"),
		Convert.FromHexString("9A01773FEBB0CFE0EBDBF37F4D23C27F"),
		Convert.FromHexString("43F32300BF252CC320E2572ACE766367"),
		Convert.FromHexString("07F161011B3101F1ED0301735631E734"),
		Convert.FromHexString("0454E7707A5F37565601E100406060AF"),
		Convert.FromHexString("647554BAD3100C43C16660F002CC10F3"),
		Convert.FromHexString("E157213170F842382032564265B0B043"),
		Convert.FromHexString("914FC59311B04151393EF6896A847636"),
		Convert.FromHexString("0710C0205D224237025323265C145FA1"),
		Convert.FromHexString("054E6F01165267025C3111F562A921E9"),
		Convert.FromHexString("722D1789E792E2CA0D5322211FD0F5AE"),
		Convert.FromHexString("91F7C751FCF671F34943430772341799")
	};

	public static byte[] Encrypt(byte[] body)
	{
		int paddedLength = (body.Length + 31) / 16 * 16;
		byte[] paddedBody = new byte[paddedLength];
		Array.Copy(body, paddedBody, body.Length);
		byte[] randomPadding = RandomAscii(16);
		Array.Copy(randomPadding, 0, paddedBody, body.Length, Math.Min(16, paddedLength - body.Length));

		int keyIndex = RandomNumberGenerator.GetInt32(G79Keys.Length);
		byte marker = (byte)((keyIndex << 4) | 0x0C);
		byte[] initializationVector = RandomAscii(16);

		using Aes aes = Aes.Create();
		aes.Padding = PaddingMode.None;
		aes.Mode = CipherMode.CBC;
		byte[] encryptedBody = aes.CreateEncryptor(G79Keys[keyIndex], initializationVector)
			.TransformFinalBlock(paddedBody, 0, paddedBody.Length);

		byte[] result = new byte[initializationVector.Length + encryptedBody.Length + 1];
		Array.Copy(initializationVector, result, initializationVector.Length);
		Array.Copy(encryptedBody, 0, result, initializationVector.Length, encryptedBody.Length);
		result[^1] = marker;
		return result;
	}

	public static byte[]? Decrypt(byte[] body)
	{
		if (body.Length < 18)
		{
			return null;
		}

		int keyIndex = (body[^1] >> 4) & 0x0F;
		byte[] initializationVector = body[..16];
		byte[] encryptedBody = body[16..^1];

		using Aes aes = Aes.Create();
		aes.Padding = PaddingMode.None;
		aes.Mode = CipherMode.CBC;
		byte[] decryptedBody = aes.CreateDecryptor(G79Keys[keyIndex], initializationVector)
			.TransformFinalBlock(encryptedBody, 0, encryptedBody.Length);

		int contentEnd = decryptedBody.Length - 1;
		while (contentEnd >= 0 && decryptedBody[contentEnd] == 0)
		{
			contentEnd--;
		}
		return contentEnd < 0 ? Array.Empty<byte>() : decryptedBody[..(contentEnd + 1)];
	}

	public static string DecryptStartType(string encryptedHex)
	{
		byte[] body = Convert.FromHexString(encryptedHex);
		byte[] decrypted = DecryptRaw(body);
		int end = decrypted.Length;
		while (end > 0 && decrypted[end - 1] == 0)
		{
			end--;
		}
		if (end < 16)
		{
			throw new InvalidOperationException("StartType payload is missing its random suffix.");
		}

		string withTail = Encoding.UTF8.GetString(decrypted, 0, end - 16);
		if (!withTail.EndsWith(StartTypeTail, StringComparison.Ordinal))
		{
			throw new InvalidOperationException("StartType payload is missing its fixed tail.");
		}
		return withTail[..^StartTypeTail.Length];
	}

	public static string EncryptStartType(string value)
	{
		byte[] body = Encoding.UTF8.GetBytes(value + StartTypeTail);
		int paddedLength = (body.Length + 31) / 16 * 16;
		byte[] paddedBody = new byte[paddedLength];
		Array.Copy(body, paddedBody, body.Length);
		byte[] randomSuffix = RandomAscii(16);
		Array.Copy(randomSuffix, 0, paddedBody, body.Length, randomSuffix.Length);

		int keyIndex = RandomNumberGenerator.GetInt32(G79Keys.Length);
		byte marker = (byte)((keyIndex << 4) | 0x04);
		byte[] initializationVector = RandomAscii(16);
		using Aes aes = Aes.Create();
		aes.Padding = PaddingMode.None;
		aes.Mode = CipherMode.CBC;
		byte[] encryptedBody = aes.CreateEncryptor(G79Keys[keyIndex], initializationVector)
			.TransformFinalBlock(paddedBody, 0, paddedBody.Length);

		byte[] result = new byte[initializationVector.Length + encryptedBody.Length + 1];
		Array.Copy(initializationVector, result, initializationVector.Length);
		Array.Copy(encryptedBody, 0, result, initializationVector.Length, encryptedBody.Length);
		result[^1] = marker;
		return Convert.ToHexStringLower(result);
	}

	private static byte[] DecryptRaw(byte[] body)
	{
		if (body.Length < 18 || (body.Length - 17) % 16 != 0)
		{
			throw new ArgumentException("Invalid encrypted G79 payload.", nameof(body));
		}
		int keyIndex = (body[^1] >> 4) & 0x0F;
		using Aes aes = Aes.Create();
		aes.Padding = PaddingMode.None;
		aes.Mode = CipherMode.CBC;
		return aes.CreateDecryptor(G79Keys[keyIndex], body[..16])
			.TransformFinalBlock(body, 16, body.Length - 17);
	}

	public static byte[]? ExtractJson(byte[] data)
	{
		int depth = 0;
		for (int index = 0; index < data.Length; index++)
		{
			if (data[index] == (byte)'{')
			{
				depth++;
			}
			else if (data[index] == (byte)'}')
			{
				depth--;
			}

			if (depth == 0 && index > 0)
			{
				return data[..(index + 1)];
			}
		}
		return data;
	}

	public static string PeAuthSign(string value, int signProfile, int transformRounds)
	{
		byte[] roundKeyBytes =
		{
			98, 37, 30, 246, 64, 179, 64, 192, 81, 90,
			94, 38, 170, 199, 182, 233, 68, 234, 190, 164,
			169, 207, 222, 75, 96, 75, 187, 246, 112, 188,
			191, 190, 195, 89, 91, 101, 146, 204, 12, 143,
			125, 244, 239, 255, 209, 93, 132, 133, 198, 126,
			155, 40, 250, 39, 161, 234, 133, 48, 239, 212,
			5, 29, 136, 4, 230, 205, 225, 33, 214, 7,
			55, 195, 135, 13, 213, 244, 237, 20, 90, 69
		};
		byte[] rotations =
		{
			1, 6, 10, 13, 2, 5, 9, 14, 4, 7,
			11, 3, 3, 8, 11, 5, 1, 7, 11, 14
		};

		if (signProfile < 0 || transformRounds <= 0 || 16 * signProfile + 16 > roundKeyBytes.Length || 4 * signProfile + 4 > rotations.Length)
		{
			throw new ArgumentException("invalid sp/tr parameters");
		}
		if (value.Length % 4 != 0)
		{
			value += new string('0', 4 - value.Length % 4);
		}

		byte[] input = Encoding.ASCII.GetBytes(value);
		List<uint> words = new List<uint>();
		for (int index = 0; index < input.Length; index += 4)
		{
			words.Add((uint)((input[index] << 24) | (input[index + 1] << 16) | (input[index + 2] << 8) | input[index + 3]));
		}
		while (words.Count % 64 != 0)
		{
			words.Add(2882398599u);
		}

		uint[] roundKeys = new uint[4];
		for (int index = 0; index < roundKeys.Length; index++)
		{
			int offset = 16 * signProfile + index * 4;
			roundKeys[index] = (uint)(roundKeyBytes[offset]
				| (roundKeyBytes[offset + 1] << 8)
				| (roundKeyBytes[offset + 2] << 16)
				| (roundKeyBytes[offset + 3] << 24));
		}
		byte[] profileRotations =
		{
			rotations[4 * signProfile],
			rotations[4 * signProfile + 1],
			rotations[4 * signProfile + 2],
			rotations[4 * signProfile + 3]
		};

		uint stateA = 1732584193u;
		uint stateB = 4023233417u;
		uint stateC = 2562383102u;
		uint stateD = 271733878u;
		for (int round = 0; round < transformRounds; round++)
		{
			for (int index = 0; index < words.Count; index += 4)
			{
				long stepA = stateA + (long)((stateB & stateC) | (~stateB & stateD));
				stateA = stateB + unchecked((uint)(int)((stepA + words[index] + roundKeys[0]) << profileRotations[0]));

				long stepB = stateA + (long)((stateB & stateD) | (~stateD & stateC));
				stateB += unchecked((uint)(int)((stepB + words[index] + roundKeys[1]) << profileRotations[1]));

				long stepC = stateA + (long)(stateB ^ stateC ^ stateD);
				stateC = stateB + unchecked((uint)(int)((stepC + words[index] + roundKeys[2]) << profileRotations[2]));

				long stepD = stateC ^ (stateB | stateD);
				stateD = stateB + unchecked((uint)(int)((stateA + stepD + words[index] + roundKeys[3]) << profileRotations[3]));
			}
		}

		byte[] signature =
		{
			(byte)stateA, (byte)(stateA >> 8), (byte)(stateA >> 16), (byte)(stateA >> 24),
			(byte)stateB, (byte)(stateB >> 8), (byte)(stateB >> 16), (byte)(stateB >> 24),
			(byte)stateC, (byte)(stateC >> 8), (byte)(stateC >> 16), (byte)(stateC >> 24),
			(byte)stateD, (byte)(stateD >> 8), (byte)(stateD >> 16), (byte)(stateD >> 24)
		};
		return Convert.ToBase64String(signature);
	}

	public static string ComputeDynamicToken(string path, string body, string userToken)
	{
		string normalizedPath = path.StartsWith('/') ? path : "/" + path;
		string tokenHash = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(userToken)));
		string digest = Convert.ToHexStringLower(MD5.HashData(Encoding.UTF8.GetBytes(tokenHash + body + "0eGsBkhl" + normalizedPath)));

		StringBuilder bits = new StringBuilder(256);
		foreach (byte value in Encoding.ASCII.GetBytes(digest))
		{
			bits.Append(Convert.ToString(value, 2).PadLeft(8, '0'));
		}
		string bitString = bits.ToString();
		string rotatedBits = bitString[6..] + bitString[..6];
		byte[] digestBytes = Encoding.ASCII.GetBytes(digest);
		for (int index = 0; index < digestBytes.Length; index++)
		{
			string byteBits = rotatedBits.Substring(index * 8, 8);
			byte mask = 0;
			for (int bit = 0; bit < 8; bit++)
			{
				if (byteBits[7 - bit] == '1')
				{
					mask |= (byte)(1 << bit);
				}
			}
			digestBytes[index] ^= mask;
		}

		string token = Convert.ToBase64String(digestBytes.AsSpan(0, 12)) + "1";
		return token.Replace('+', 'm').Replace('/', 'o');
	}

	private static byte[] RandomAscii(int count)
	{
		byte[] bytes = new byte[count];
		for (int index = 0; index < bytes.Length; index++)
		{
			bytes[index] = (byte)RandomCharacters[RandomNumberGenerator.GetInt32(RandomCharacters.Length)];
		}
		return bytes;
	}
}
