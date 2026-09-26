using System;
using System.Collections.Generic;
using System.Text;

namespace FandNEL.Core.Utils.Cipher;

public static class XxTeaUtil
{
	private const string Key = "942894570397f6d1c9cca2535ad18a2b";

	private const long Delta = 2654435769L;

	private const string SignaturePrefix = "!x19sign!";

	public static string X19SignEncrypt(this string input)
	{
		return SignaturePrefix + EncryptToHex(input, Key);
	}

	public static string X19SignDecrypt(this string input)
	{
		string hex;
		if (!input.StartsWith(SignaturePrefix))
		{
			hex = input;
		}
		else
		{
			int length = SignaturePrefix.Length;
			hex = input.Substring(length, input.Length - length);
		}

		return DecryptFromHex(hex, Key);
	}

	private static string EncryptToHex(string data, string key)
	{
		long[] blocks = ToLongArray(Encoding.UTF8.GetBytes(data));
		long[] keyBlocks = ToLongArray(Encoding.UTF8.GetBytes(key.PadRight(32, '\0')));
		return ToHexString(EncryptBlocks(blocks, keyBlocks));
	}

	private static string DecryptFromHex(string hex, string key)
	{
		if (string.IsNullOrWhiteSpace(hex))
		{
			return hex;
		}
		long[] blocks = FromHexString(hex);
		long[] keyBlocks = ToLongArray(Encoding.UTF8.GetBytes(key.PadRight(32, '\0')));
		byte[] bytes = ToByteArray(DecryptBlocks(blocks, keyBlocks));
		return Encoding.UTF8.GetString(bytes).TrimEnd('\0');
	}

	private static long[] EncryptBlocks(long[] blocks, long[] key)
	{
		int blockCount = blocks.Length;
		if (blockCount < 1)
		{
			return blocks;
		}

		long sum = 0L;
		long rounds = 6 + 52 / blockCount;
		long previous = blocks[blockCount - 1];
		while (rounds-- > 0)
		{
			sum += Delta;
			long keyIndex = (sum >> 2) & 3;
			for (int index = 0; index < blockCount - 1; index++)
			{
				long next = blocks[index + 1];
				long blockMix = ((previous >> 5) ^ (next << 2)) + ((next >> 3) ^ (previous << 4));
				blockMix ^= (sum ^ next) + (key[(index & 3) ^ keyIndex] ^ previous);
				previous = (blocks[index] += blockMix);
			}

			long first = blocks[0];
			long lastBlockMix = ((previous >> 5) ^ (first << 2)) + ((first >> 3) ^ (previous << 4));
			lastBlockMix ^= (sum ^ first) + (key[((blockCount - 1) & 3) ^ keyIndex] ^ previous);
			blocks[blockCount - 1] += lastBlockMix;
			previous = blocks[blockCount - 1];
		}

		return blocks;
	}

	private static long[] DecryptBlocks(long[] blocks, long[] key)
	{
		int blockCount = blocks.Length;
		if (blockCount < 1)
		{
			return blocks;
		}

		long sum = (6 + 52 / blockCount) * Delta;
		long next = blocks[0];
		while (sum != 0L)
		{
			long keyIndex = (sum >> 2) & 3;
			for (int index = blockCount - 1; index > 0; index--)
			{
				long previous = blocks[index - 1];
				long blockMix = ((previous >> 5) ^ (next << 2)) + ((next >> 3) ^ (previous << 4));
				blockMix ^= (sum ^ next) + (key[(index & 3) ^ keyIndex] ^ previous);
				next = (blocks[index] -= blockMix);
			}

			long previousBlock = blocks[blockCount - 1];
			long firstBlockMix = ((previousBlock >> 5) ^ (next << 2)) + ((next >> 3) ^ (previousBlock << 4));
			firstBlockMix ^= (sum ^ next) + (key[keyIndex] ^ previousBlock);
			blocks[0] -= firstBlockMix;
			next = blocks[0];
			sum -= Delta;
		}

		return blocks;
	}

	private static long[] ToLongArray(byte[] data)
	{
		int blockCount = (data.Length + 7) / 8;
		long[] blocks = new long[blockCount];
		for (int index = 0; index < blockCount; index++)
		{
			blocks[index] = BitConverter.ToInt64(data, (index * 8 <= data.Length - 8) ? (index * 8) : (data.Length - 8));
		}

		return blocks;
	}

	private static byte[] ToByteArray(long[] data)
	{
		List<byte> list = new List<byte>(data.Length * 8);
		foreach (long value in data)
		{
			list.AddRange(BitConverter.GetBytes(value));
		}
		int lastNonZeroIndex = list.Count - 1;
		while (lastNonZeroIndex >= 0 && list[lastNonZeroIndex] == 0)
		{
			lastNonZeroIndex--;
		}

		return list.GetRange(0, lastNonZeroIndex + 1).ToArray();
	}

	private static string ToHexString(long[] data)
	{
		StringBuilder stringBuilder = new StringBuilder(data.Length * 16);
		foreach (long value in data)
		{
			stringBuilder.Append(value.ToString("x16"));
		}
		return stringBuilder.ToString();
	}

	private static long[] FromHexString(string hex)
	{
		int blockCount = hex.Length / 16;
		long[] blocks = new long[blockCount];
		for (int index = 0; index < blockCount; index++)
		{
			blocks[index] = Convert.ToInt64(hex.Substring(index * 16, 16), 16);
		}

		return blocks;
	}
}
