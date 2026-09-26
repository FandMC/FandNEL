using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Utils;

public class RandomUtil
{
	public static string GetRandomString(int length, string? chars = null)
	{
		if (length <= 0)
		{
			return string.Empty;
		}
		if (string.IsNullOrEmpty(chars))
		{
			chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghizklmnopqrstuvwxyz0123456789";
		}
		StringBuilder stringBuilder = new StringBuilder(length);
		byte[] randomBytes = new byte[length];
		RandomNumberGenerator.Fill(randomBytes);
		for (int i = 0; i < length; i++)
		{
			int index = randomBytes[i] % chars.Length;
			stringBuilder.Append(chars[index]);
		}
		return stringBuilder.ToString();
	}

	public static string GenerateSessionId()
	{
		return "captchaReq" + GetRandomString(16);
	}
}
