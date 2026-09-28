using DotNetty.Buffers;

namespace FandNEL.Proxy.Packet.Heypixel;

public static class PayloadScrambler
{
    private const int ShuffleSeed = 1592639215;

    public static void Write(IByteBuffer buffer, byte[] payload, Uuid128 key)
    {
        int transform = PositiveModulo(key.MostSignificantBits ^ key.LeastSignificantBits, 8);
        switch (transform)
        {
            case 0:
                buffer.WriteBytes(payload);
                break;
            case 1:
                WriteReversed(buffer, payload);
                break;
            case 2:
                WriteEvenThenOdd(buffer, payload);
                break;
            case 3:
                WriteRotated(buffer, payload);
                break;
            case 4:
                WriteXor(buffer, payload);
                break;
            case 5:
                WriteReversedFourByteBlocks(buffer, payload);
                break;
            case 6:
                WriteNibbleSwapped(buffer, payload);
                break;
            case 7:
                WriteBitReversed(buffer, payload);
                break;
            case 8:
                WriteDeterministicallyShuffled(buffer, payload);
                break;
        }
    }

    private static int PositiveModulo(long value, int divisor) =>
        (int)((value % divisor + divisor) % divisor);

    private static void WriteReversed(IByteBuffer buffer, byte[] payload)
    {
        for (int index = payload.Length - 1; index >= 0; index--)
        {
            buffer.WriteByte(payload[index]);
        }
    }

    private static void WriteDeterministicallyShuffled(IByteBuffer buffer, byte[] payload)
    {
        int[] indices = Enumerable.Range(0, payload.Length).ToArray();
        Random random = new(ShuffleSeed);
        for (int index = indices.Length - 1; index > 0; index--)
        {
            int replacementIndex = random.Next(index + 1);
            (indices[index], indices[replacementIndex]) = (indices[replacementIndex], indices[index]);
        }

        foreach (int index in indices)
        {
            buffer.WriteByte(payload[index]);
        }
    }

    private static void WriteEvenThenOdd(IByteBuffer buffer, byte[] payload)
    {
        for (int index = 0; index < payload.Length; index += 2)
        {
            buffer.WriteByte(payload[index]);
        }

        for (int index = 1; index < payload.Length; index += 2)
        {
            buffer.WriteByte(payload[index]);
        }
    }

    private static void WriteRotated(IByteBuffer buffer, byte[] payload)
    {
        if (payload.Length == 0)
        {
            return;
        }

        int offset = (payload.Length % 5 + 1) % payload.Length;
        buffer.WriteBytes(payload, offset, payload.Length - offset);
        buffer.WriteBytes(payload, 0, offset);
    }

    private static void WriteXor(IByteBuffer buffer, byte[] payload)
    {
        foreach (byte item in payload)
        {
            buffer.WriteByte((byte)(item ^ 0xA5));
        }
    }

    private static void WriteReversedFourByteBlocks(IByteBuffer buffer, byte[] payload)
    {
        int blockCount = payload.Length / 4;
        for (int block = blockCount - 1; block >= 0; block--)
        {
            buffer.WriteBytes(payload, block * 4, 4);
        }

        int remainder = payload.Length % 4;
        if (remainder > 0)
        {
            buffer.WriteBytes(payload, blockCount * 4, remainder);
        }
    }

    private static void WriteNibbleSwapped(IByteBuffer buffer, byte[] payload)
    {
        foreach (byte item in payload)
        {
            buffer.WriteByte((byte)((item >> 4) | (item << 4)));
        }
    }

    private static void WriteBitReversed(IByteBuffer buffer, byte[] payload)
    {
        foreach (byte item in payload)
        {
            int value = ((item & 0xCC) >> 2) | ((item & 0x33) << 2);
            value = ((value & 0xAA) >> 1) | ((value & 0x55) << 1);
            buffer.WriteByte((byte)value);
        }
    }
}
