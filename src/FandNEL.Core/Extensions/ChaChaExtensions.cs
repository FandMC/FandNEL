using System;
using System.IO.Hashing;
using FandNEL.Core.Connection.ChaCha;

namespace FandNEL.Core.Extensions;

public static class ChaChaExtensions
{
	public static byte[] PackMessage(this ChaChaOfSalsa cipher, byte type, byte[] data)
	{
		if (data.Length > short.MaxValue - 8) throw new System.IO.InvalidDataException("认证载荷超过协议长度限制。");
		byte[] packet = new byte[data.Length + 10];
		Array.Copy(BitConverter.GetBytes((short)(packet.Length - 2)), 0, packet, 0, 2);
		packet[6] = type;
		packet[7] = 136;
		packet[8] = 136;
		packet[9] = 136;
		Array.Copy(data, 0, packet, 10, data.Length);
		Array.Copy(Crc32.Hash(packet.AsSpan(6)), 0, packet, 2, 4);
		cipher.ProcessBytes(packet, 2, packet.Length - 2, packet, 2);
		return packet;
	}

	public static (byte, byte[]) UnpackMessage(this ChaChaOfSalsa cipher, byte[] data)
	{
		if (data.Length < 8) throw new System.IO.InvalidDataException("认证响应长度无效。");
		cipher.ProcessBytes(data, 0, data.Length, data, 0);
		byte[] expectedChecksum = new byte[4];
		Crc32.Hash(data.AsSpan(4, data.Length - 4), expectedChecksum);
		for (int index = 0; index < expectedChecksum.Length; index++)
		{
			if (expectedChecksum[index] != data[index])
			{
				throw new Exception("Unpacking failed");
			}
		}
		byte[] payload = new byte[data.Length - 8];
		Array.Copy(data, 8, payload, 0, payload.Length);
		return (data[4], payload);
	}
}
