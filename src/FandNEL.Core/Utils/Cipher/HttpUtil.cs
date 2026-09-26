using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Core.Utils.Cipher;

public static class HttpUtil
{
	private const string SKeys = "MK6mipwmOUedplb6,OtEylfId6dyhrfdn,VNbhn5mvUaQaeOo9,bIEoQGQYjKd02U0J,fuaJrPwaH2cfXXLP,LEkdyiroouKQ4XN1,jM1h27H4UROu427W,DhReQada7gZybTDk,ZGXfpSTYUvcdKqdY,AZwKf7MWZrJpGR5W,amuvbcHw38TcSyPU,SI4QotspbjhyFdT0,VP4dhjKnDGlSJtbB,UXDZx4KhZywQ2tcn,NIK73ZNvNqzva4kd,WeiW7qU766Q1YQZI";

	private static Aes Aes
	{
		get
		{
			Aes aes = System.Security.Cryptography.Aes.Create();
			aes.Padding = PaddingMode.None;
			return aes;
		}
	}

	private static byte[][] HttpKeys => (from skey in "MK6mipwmOUedplb6,OtEylfId6dyhrfdn,VNbhn5mvUaQaeOo9,bIEoQGQYjKd02U0J,fuaJrPwaH2cfXXLP,LEkdyiroouKQ4XN1,jM1h27H4UROu427W,DhReQada7gZybTDk,ZGXfpSTYUvcdKqdY,AZwKf7MWZrJpGR5W,amuvbcHw38TcSyPU,SI4QotspbjhyFdT0,VP4dhjKnDGlSJtbB,UXDZx4KhZywQ2tcn,NIK73ZNvNqzva4kd,WeiW7qU766Q1YQZI".Split(',')
		select Encoding.GetEncoding("us-ascii").GetBytes(skey)).ToArray();

	public static byte[] HttpEncrypt(byte[] bodyIn)
	{
		byte[] paddedBody = new byte[(int)Math.Ceiling((double)(bodyIn.Length + 16) / 16.0) * 16];
		Array.Copy(bodyIn, paddedBody, bodyIn.Length);
		byte[] initializationVector = Encoding.ASCII.GetBytes(StringGenerator.GenerateRandomString(16, includeNumbers: false));
		for (int index = 0; index < initializationVector.Length; index++)
		{
			paddedBody[index + bodyIn.Length] = initializationVector[index];
		}

		byte flags = (byte)((uint)(Random.Shared.Next(0, HttpKeys.Length - 1) << 4) | 2u);
		byte[] encryptedBody = Aes.CreateEncryptor(HttpKeys[(flags >> 4) & 0xF], initializationVector).TransformFinalBlock(paddedBody, 0, paddedBody.Length);
		byte[] payload = new byte[16 + encryptedBody.Length + 1];
		Array.Copy(initializationVector, payload, 16);
		Array.Copy(encryptedBody, 0, payload, 16, encryptedBody.Length);
		payload[^1] = flags;
		return payload;
	}

	public static byte[]? HttpDecrypt(byte[] body)
	{
		if (body.Length < 18)
		{
			return null;
		}
		byte[] encryptedBody = body.Skip(16).Take(body.Length - 1 - 16).ToArray();
		byte[] decryptedBody = Aes.CreateDecryptor(HttpKeys[(body[^1] >> 4) & 0xF], body.Take(16).ToArray()).TransformFinalBlock(encryptedBody, 0, encryptedBody.Length);
		int nonZeroPaddingBytes = 0;
		int contentEndIndex = decryptedBody.Length - 1;
		while (nonZeroPaddingBytes < 16)
		{
			if (decryptedBody[contentEndIndex--] != 0)
			{
				nonZeroPaddingBytes++;
			}
		}

		return decryptedBody.Take(contentEndIndex + 1).ToArray();
	}
}
