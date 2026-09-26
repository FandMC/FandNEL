using System;
using System.Text;

namespace FandNEL.Core.Utils;

public class StringGenerator
{
	private static readonly Random Random = new Random();

	public static string GenerateHexString(int length)
	{
		byte[] bytes = new byte[length];
		Random.NextBytes(bytes);
		return Convert.ToHexString(bytes);
	}

	public static string GenerateRandomString(int length, bool includeNumbers = true, bool includeUppercase = true, bool includeLowercase = true)
	{
		if (length <= 0)
		{
			throw new ArgumentException("Length must be greater than 0", nameof(length));
		}
		if (!includeNumbers && !includeUppercase && !includeLowercase)
		{
			throw new ArgumentException("Must include at least one character type", "includeNumbers, includeUppercase, includeLowercase");
		}
		StringBuilder stringBuilder = new StringBuilder();
		if (includeNumbers)
		{
			stringBuilder.Append("0123456789");
		}
		if (includeUppercase)
		{
			stringBuilder.Append("ABCDEFGHIJKLMNOPQRSTUVWXYZ");
		}
		if (includeLowercase)
		{
			stringBuilder.Append("abcdefghijklmnopqrstuvwxyz");
		}
		int characterSetLength = stringBuilder.Length;
		StringBuilder result = new StringBuilder(length);
		for (int i = 0; i < length; i++)
		{
			result.Append(stringBuilder[Random.Next(characterSetLength)]);
		}
		return result.ToString();
	}

	public static string GenerateRandomMacAddress(string separator = ":", bool uppercase = true)
	{
		byte[] addressBytes = new byte[6];
		Random.NextBytes(addressBytes);
		addressBytes[0] = (byte)(addressBytes[0] & 0xFEu);
		addressBytes[0] = (byte)(addressBytes[0] | 2u);
		string format = uppercase ? "X2" : "x2";
		string[] values = new string[addressBytes.Length];
		for (int i = 0; i < addressBytes.Length; i++)
		{
			values[i] = addressBytes[i].ToString(format);
		}
		return string.Join(separator, values);
	}
}
