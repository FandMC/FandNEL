using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace FandNEL.Proxy.Heypixel;

public static class HashExtensions
{
    public static string Sha256Hex(this string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();

    public static string Sha1Hex(this byte[] value) =>
        Convert.ToHexString(SHA1.HashData(value)).ToLowerInvariant();

    public static int JavaHashCode(this string value) =>
        value.Aggregate(
            0,
            static (current, character) => unchecked((current << 5) - current + character));
}
