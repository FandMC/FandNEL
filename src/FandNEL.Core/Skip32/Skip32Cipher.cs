using System;
using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Skip32;

public class Skip32Cipher
{
	private const int KeySize = 10;

	private static readonly byte[] FTable = new byte[256]
	{
		163, 215, 9, 131, 248, 72, 246, 244, 179, 33,
		21, 120, 153, 177, 175, 249, 231, 45, 77, 138,
		206, 76, 202, 46, 82, 149, 217, 30, 78, 56,
		68, 40, 10, 223, 2, 160, 23, 241, 96, 104,
		18, 183, 122, 195, 233, 250, 61, 83, 150, 132,
		107, 186, 242, 99, 154, 25, 124, 174, 229, 245,
		247, 22, 106, 162, 57, 182, 123, 15, 193, 147,
		129, 27, 238, 180, 26, 234, 208, 145, 47, 184,
		85, 185, 218, 133, 63, 65, 191, 224, 90, 88,
		128, 95, 102, 11, 216, 144, 53, 213, 192, 167,
		51, 6, 101, 105, 69, 0, 148, 86, 109, 152,
		155, 118, 151, 252, 178, 194, 176, 254, 219, 32,
		225, 235, 214, 228, 221, 71, 74, 29, 66, 237,
		158, 110, 73, 60, 205, 67, 39, 210, 7, 212,
		222, 199, 103, 24, 137, 203, 48, 31, 141, 198,
		143, 170, 200, 116, 220, 201, 93, 92, 49, 164,
		112, 136, 97, 44, 159, 13, 43, 135, 80, 130,
		84, 100, 38, 125, 3, 64, 52, 75, 28, 115,
		209, 196, 253, 59, 204, 251, 127, 171, 230, 62,
		91, 165, 173, 4, 35, 156, 20, 81, 34, 240,
		41, 121, 113, 126, 255, 140, 14, 226, 12, 239,
		188, 114, 117, 111, 55, 161, 236, 211, 142, 98,
		139, 134, 16, 232, 8, 119, 17, 190, 146, 79,
		36, 197, 50, 54, 157, 207, 243, 166, 187, 172,
		94, 108, 169, 19, 87, 37, 181, 227, 189, 168,
		58, 1, 5, 89, 42, 70
	};

	private readonly byte[] _key;

	public Skip32Cipher(byte[] key)
	{
		if (key.Length != KeySize)
		{
			throw new ArgumentOutOfRangeException(nameof(key), $"Key must be {KeySize} bytes.");
		}
		_key = key;
	}

	private static int G(byte[] key, int round, int word)
	{
		int highByte = word >> 8;
		int lowByte = word & 0xFF;
		int first = FTable[lowByte ^ (key[4 * round % KeySize] & 0xFF)] ^ highByte;
		int second = FTable[first ^ (key[(4 * round + 1) % KeySize] & 0xFF)] ^ lowByte;
		int third = FTable[second ^ (key[(4 * round + 2) % KeySize] & 0xFF)] ^ first;
		int fourth = FTable[third ^ (key[(4 * round + 3) % KeySize] & 0xFF)] ^ second;
		return (third << 8) + fourth;
	}

	private void Skip32(int[] buffer, bool encrypt)
	{
		int roundIncrement;
		int round;
		if (encrypt)
		{
			roundIncrement = 1;
			round = 0;
		}
		else
		{
			roundIncrement = -1;
			round = 23;
		}

		int left = (buffer[0] << 8) + buffer[1];
		int right = (buffer[2] << 8) + buffer[3];
		for (int cycle = 0; cycle < 12; cycle++)
		{
			right ^= G(_key, round, left) ^ round;
			round += roundIncrement;
			left ^= G(_key, round, right) ^ round;
			round += roundIncrement;
		}

		buffer[0] = right >> 8;
		buffer[1] = right & 0xFF;
		buffer[2] = left >> 8;
		buffer[3] = left & 0xFF;
	}

	public uint Encrypt(uint value)
	{
		return (uint)Encrypt((int)value);
	}

	public int Encrypt(int value)
	{
		int[] bytes = new int[4]
		{
			(value >> 24) & 0xFF,
			(value >> 16) & 0xFF,
			(value >> 8) & 0xFF,
			value & 0xFF
		};
		Skip32(bytes, encrypt: true);
		return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
	}

	public uint Decrypt(uint value)
	{
		return (uint)Decrypt((int)value);
	}

	public int Decrypt(int value)
	{
		int[] bytes = new int[4]
		{
			(value >> 24) & 0xFF,
			(value >> 16) & 0xFF,
			(value >> 8) & 0xFF,
			value & 0xFF
		};
		Skip32(bytes, encrypt: false);
		return (bytes[0] << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
	}

	public string GenerateRoleUuid(string roleName, uint userId)
	{
		byte[] uuidBytes = MD5.HashData(Encoding.UTF8.GetBytes(roleName));
		byte[] bytes = BitConverter.GetBytes(Encrypt(userId));
		Buffer.BlockCopy(bytes, 0, uuidBytes, 12, bytes.Length);
		uuidBytes[6] = (byte)((uuidBytes[6] & 0xFu) | 0x40u);
		uuidBytes[8] = (byte)((uuidBytes[8] & 0x3Fu) | 0x80u);
		return Convert.ToHexStringLower(uuidBytes);
	}

	public uint ComputeUserIdFromUuid(string uuid)
	{
		uuid = uuid.Replace("-", "");
		if (uuid.Length != 32)
		{
			return 0u;
		}
		uint value = BitConverter.ToUInt32(Convert.FromHexString(uuid), 12);
		return Decrypt(value);
	}
}
