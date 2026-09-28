using System.Security.Cryptography;
using System.Buffers.Binary;
using System.Globalization;

namespace FandNEL.Proxy.Packet.Heypixel;

public class UuidFormatException : Exception
{
    public UuidFormatException(string message)
        : base(message)
    {
    }

    public UuidFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}

public class Uuid128
{
    public long MostSignificantBits { get; }

    public long LeastSignificantBits { get; }

    public Uuid128(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        if (bytes.Length != 16)
        {
            throw new ArgumentException("data must be 16 bytes in length", nameof(bytes));
        }

        MostSignificantBits = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(0, 8));
        LeastSignificantBits = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(8, 8));
    }

    public Uuid128(long mostSignificantBits, long leastSignificantBits)
    {
        MostSignificantBits = mostSignificantBits;
        LeastSignificantBits = leastSignificantBits;
    }

    public Uuid128(string value)
    {
        if (!Guid.TryParseExact(value, "D", out Guid uuid) && !Guid.TryParseExact(value, "N", out uuid))
        {
            throw new UuidFormatException("Invalid UUID format.");
        }

        byte[] bytes = uuid.ToByteArray(bigEndian: true);
        MostSignificantBits = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(0, 8));
        LeastSignificantBits = BinaryPrimitives.ReadInt64BigEndian(bytes.AsSpan(8, 8));
    }

    public string ToEncodedString() =>
        string.Create(CultureInfo.InvariantCulture, $"{LeastSignificantBits}|-|{MostSignificantBits}");

    public static Uuid128 FromEncodedString(string value)
    {
        string[] parts = value.Split("|-|", StringSplitOptions.None);
        if (parts.Length != 2)
        {
            throw new FormatException("Invalid encoded UUID format.");
        }

        long leastSignificantBits = long.Parse(parts[0], CultureInfo.InvariantCulture);
        long mostSignificantBits = long.Parse(parts[1], CultureInfo.InvariantCulture);
        return new Uuid128(mostSignificantBits, leastSignificantBits);
    }

    public static Uuid128 Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new UuidFormatException("UUID string cannot be null or empty");
        }

        string text = value.Trim();
        if (text.Length != 36)
        {
            throw new UuidFormatException($"Invalid UUID format: expected 36 characters, got {text.Length}");
        }

        if (text[8] != '-' || text[13] != '-' || text[18] != '-' || text[23] != '-')
        {
            throw new UuidFormatException("Invalid UUID format: dashes must be at positions 8, 13, 18, and 23");
        }

        return new Uuid128(text);
    }

    public static Uuid128 NewRandom()
    {
        byte[] bytes = new byte[16];
        RandomNumberGenerator.Fill(bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x40);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Uuid128(bytes);
    }

    public override string ToString()
    {
        string hex = unchecked((ulong)MostSignificantBits).ToString("x16")
            + unchecked((ulong)LeastSignificantBits).ToString("x16");
        return $"{hex[..8]}-{hex.Substring(8, 4)}-{hex.Substring(12, 4)}-{hex.Substring(16, 4)}-{hex[20..]}";
    }

}
