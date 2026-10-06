using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using FandNEL.Proxy.Packet.IO;
using FandNEL.Proxy.Protocol;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Servers;

/// <summary>DFDL通道：AES-ECB + GZIP 封装的 BaseMessage。</summary>
internal static class DfdlProtocol
{
    internal const string Channel = "eastlandanticheat";
    internal const string DefaultKey = "97a19f682c416741";
    private const int MaximumDecompressedBytes = 8_388_608;

    internal static bool IsEnabled(MinecraftConnection connection) => ServerGameIds.IsGame(connection, ServerGameIds.Dfdl);

    /// <summary>上行只放行心跳（MessageID == 1），其余反作弊包在代理侧丢弃。</summary>
    internal static ValueTask HandleServerboundAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var state = context.Connection.ServerProtocols;
        BaseMessage message;
        try
        {
            message = BaseMessage.Parse(data);
            try
            {
                _ = Encoding.UTF8.GetString(Decompress(message.Decrypt(state.DfdlClientKey ?? DefaultKey)));
            }
            catch (Exception)
            {
                // 与参考实现一致：解不出内容不改变转发决策，只作为格式校验。
            }
        }
        catch (Exception)
        {
            context.Cancel();
            return ValueTask.CompletedTask;
        }

        if (message.MessageId == 1 && message.MessageType == 0)
            return ValueTask.CompletedTask;
        context.Cancel();
        return ValueTask.CompletedTask;
    }

    /// <summary>下行保存服务端下发的密钥，其余原样转发给客户端。</summary>
    internal static ValueTask HandleClientboundAsync(PacketContext context, byte[] data, CancellationToken cancellationToken)
    {
        _ = cancellationToken;
        var state = context.Connection.ServerProtocols;
        try
        {
            var message = BaseMessage.Parse(data);
            if (message.MessageType == 1)
            {
                if (message.Data is null)
                    throw new InvalidDataException("BaseMessage 缺少数据字段。");
                state.DfdlClientKey = Encoding.UTF8.GetString(BaseMessage.Decrypt(message.Data, DefaultKey));
            }
        }
        catch (Exception)
        {
            context.Cancel();
        }
        return ValueTask.CompletedTask;
    }

    private static byte[] Decompress(byte[] bytes)
    {
        using var input = new MemoryStream(bytes);
        using var gzip = new GZipStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        var buffer = new byte[81_920];
        int read;
        while ((read = gzip.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > MaximumDecompressedBytes)
                throw new InvalidDataException($"GZip 解压数据超过 {MaximumDecompressedBytes} 字节上限");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    /// <summary>[varint 保留][int16 MessageID][int16 长度][数据]。</summary>
    private sealed class BaseMessage
    {
        internal int MessageId { get; private set; }
        internal int MessageType { get; private set; }
        internal byte[]? Data { get; private set; }

        internal static BaseMessage Parse(byte[] bytes)
        {
            var reader = new PacketReader(bytes);
            reader.ReadVarInt();
            var messageId = reader.ReadUnsignedShort();
            var length = reader.ReadUnsignedShort();
            return new BaseMessage
            {
                MessageId = messageId,
                MessageType = GetMessageType(messageId),
                Data = length > 0 ? reader.ReadBytes(length) : null
            };
        }

        internal byte[] Decrypt(string key) => Decrypt(Data ?? throw new InvalidDataException("BaseMessage 缺少数据字段。"), key);

        internal static byte[] Decrypt(byte[] data, string key)
        {
            using var aes = Aes.Create();
            aes.Key = Encoding.UTF8.GetBytes(key);
            aes.Mode = CipherMode.ECB;
            aes.Padding = PaddingMode.PKCS7;
            using var decryptor = aes.CreateDecryptor(aes.Key, aes.IV);
            using var input = new MemoryStream(data);
            using var crypto = new CryptoStream(input, decryptor, CryptoStreamMode.Read);
            using var output = new MemoryStream();
            crypto.CopyTo(output);
            return output.ToArray();
        }

        private static int GetMessageType(int messageId) => messageId switch
        {
            0 => 1,
            >= 2 and <= 17 => messageId,
            _ => 0
        };
    }
}
