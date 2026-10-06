using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FandNEL.Proxy.Sessions;

namespace FandNEL.Proxy.Servers;

/// <summary>艾尔莉雅（Aely）的校验通道、频道表与校验报文生成，移植自参考实现。</summary>
internal static class AelyProtocol
{
    /// <summary>服务端下发校验质询、代理回发校验结果的通道。</summary>
    internal const string CheckChannel = "jfdhfi";

    /// <summary>旧版品牌通道；Aely 需要双向丢弃并按条件补发频道表。</summary>
    internal const string BrandChannel = "MC|Brand";

    /// <summary>Aely 固定的频道列表，十六进制常量原样取自参考实现。</summary>
    internal static byte[] ChannelList { get; } = Convert.FromHexString(
        "464d4c7c485300464d4c00464d4c7c4d5000464d4c00616e74696d6f64006a666468666900464f52474500436c69656e74487562");

    /// <summary>Aely 的原版启动器模组清单，校验报文必须带上这些条目。</summary>
    private static readonly IReadOnlyDictionary<string, string> DefaultMods = BuildDefaultMods();

    internal static bool IsEnabled(MinecraftConnection connection) =>
        ServerGameIds.IsGame(connection, ServerGameIds.Aely);

    /// <summary>读取 Aely 自定义的 int16 长度前缀 UTF-8 字符串。</summary>
    internal static string ReadPrefixedString(ReadOnlySpan<byte> data)
    {
        if (data.Length < 2)
            throw new InvalidDataException("Aely 载荷缺少字符串长度前缀。");
        var length = (data[0] << 8) | data[1];
        if (length < 0 || length > data.Length - 2)
            throw new InvalidDataException("Aely 字符串长度超出载荷边界。");
        return Encoding.UTF8.GetString(data.Slice(2, length));
    }

    /// <summary>写入 Aely 自定义的 int16 长度前缀 UTF-8 字符串。</summary>
    internal static byte[] WritePrefixedString(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var bytes = Encoding.UTF8.GetBytes(value);
        if (bytes.Length > ushort.MaxValue)
            throw new InvalidDataException("Aely 字符串超出长度上限。");
        var result = new byte[bytes.Length + 2];
        result[0] = (byte)(bytes.Length >> 8);
        result[1] = (byte)bytes.Length;
        bytes.CopyTo(result, 2);
        return result;
    }

    /// <summary>生成服务端期望的校验 JSON；mod 字典不可为空，否则校验会被服务端拒绝。</summary>
    internal static string BuildCheckJson(string name, string uuid, string clientHubCode,
        IReadOnlyDictionary<string, string> mods)
    {
        ArgumentNullException.ThrowIfNull(mods);
        var modsDict = new Dictionary<string, string>(mods.Count + DefaultMods.Count, StringComparer.Ordinal);
        foreach (var pair in mods)
            modsDict[pair.Key] = pair.Value;
        foreach (var pair in DefaultMods)
            modsDict[pair.Key] = pair.Value;
        foreach (var modKey in modsDict.Keys.ToArray())
            modsDict[modKey] = modsDict[modKey].TrimStart('0');

        var hardware = GetHardware(name, uuid);
        var key = Md5Bz(Decrypt(Md5Bz("破解就操死你全家"), clientHubCode) + hardware);
        var plainText = JsonSerializer.Serialize(
            new[] { new Dictionary<string, List<Dictionary<string, string>>> { ["mods"] = [modsDict] } });
        var encrypted = Encrypt(key, plainText);
        return JsonSerializer.Serialize(new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["ModMd5Check"] = encrypted ?? string.Empty,
            ["PlayerHardware"] = hardware
        });
    }

    internal static string GetHardware(string name, string uuid) =>
        Md5Bz($"M-i-r-a-c-l-e:{name}:{uuid}:H-W-I-D");

    private const string Iv = "OYcvVlHbKthIDnfR";

    private static string Encrypt(string key, string plainText)
    {
        try
        {
            var material = Encoding.UTF8.GetBytes(ProcessKey(key));
            var bytes = Encoding.UTF8.GetBytes(plainText);
            var blockSize = 16;
            if (bytes.Length % blockSize != 0)
                Array.Resize(ref bytes, bytes.Length + (blockSize - bytes.Length % blockSize));
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = material;
            aes.IV = Encoding.UTF8.GetBytes(Iv);
            using var transform = aes.CreateEncryptor(aes.Key, aes.IV);
            return Convert.ToBase64String(transform.TransformFinalBlock(bytes, 0, bytes.Length)).Trim();
        }
        catch (Exception)
        {
            return null!;
        }
    }

    private static string? Decrypt(string key, string cipherText)
    {
        try
        {
            var material = Encoding.UTF8.GetBytes(ProcessKey(key));
            var cipher = Convert.FromBase64String(cipherText);
            using var aes = Aes.Create();
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.None;
            aes.Key = material;
            aes.IV = Encoding.UTF8.GetBytes(Iv);
            using var transform = aes.CreateDecryptor(aes.Key, aes.IV);
            var plain = transform.TransformFinalBlock(cipher, 0, cipher.Length);
            var end = plain.Length - 1;
            while (end >= 0 && plain[end] == 0)
                end--;
            var trimmed = new byte[end + 1];
            Array.Copy(plain, trimmed, trimmed.Length);
            return Encoding.UTF8.GetString(trimmed).Trim();
        }
        catch (Exception)
        {
            return null;
        }
    }

    /// <summary>取密钥奇数位，得到 16 字节 AES 密钥材料。</summary>
    private static string ProcessKey(string key)
    {
        var builder = new StringBuilder(key.Length);
        for (var i = 0; i < key.Length; i++)
        {
            if (i % 2 != 0)
                builder.Append(key[i]);
        }
        return builder.ToString();
    }

    private static string Md5Bz(string input)
    {
        var hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        var builder = new StringBuilder(hash.Length * 2);
        foreach (var value in hash)
            builder.Append(value.ToString("x2"));
        return builder.ToString();
    }

    private static IReadOnlyDictionary<string, string> BuildDefaultMods() => new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["lzma-0.0.1.jar"] = "a3e3c3186e41c4a1a3027ba2bb23cdc6",
        ["authlib-1.5.25.jar"] = "c465799db4dbbfb6329b5202c10b7557",
        ["asm-all-5.2.jar"] = "f5ad16c7f0338b541978b0430d51dc83",
        ["text2speech-1.10.3.jar"] = "40b91200489368d4593257b483ec8f7a",
        ["lwjgl-2.9.4-nightly-20150209.jar"] = "6e55ddca0cb6375facfecf1c769b7d77",
        ["akka-actor_2.11-2.3.3.jar"] = "72553b9b2c93cce5b6a0a02d4dce9ac5",
        ["fastutil-7.1.0.jar"] = "35fc1f3aaab7a782873be02319a53828",
        ["vecmath-1.5.2.jar"] = "e5d2b7f46c4800a32f62ce75676a5710",
        ["commons-lang3-3.5.jar"] = "780b5a8b72eebe6d0dbff1c11b5658fa",
        ["scala-continuations-plugin_2.11.1-1.0.2.jar"] = "40e725b6f9da3a52e6adffd94abb8290",
        ["scala-continuations-library_2.11-1.0.2.jar"] = "f2a6b8fb8451839efb670d03dfd79ebd",
        ["maven-artifact-3.5.3.jar"] = "7741ebf29690ee7d9dde9cf4376347fc",
        ["launchwrapper-1.12.jar"] = "a895fe657915d58f55919ceacd30209d",
        ["commons-compress-1.8.1.jar"] = "d862e30ff6b5d78264677dcd6507abb8",
        ["netty-all-4.1.9.Final.jar"] = "2f4a891dffdb1259e4b10f1f090224e2",
        ["1.12.2.jar"] = "7b28b3316ed487c05fa36a6168bc7f54",
        ["lwjgl_util-2.9.4-nightly-20150209.jar"] = "b5c4665943f749a0bba6a6da53a45674",
        ["httpcore-4.3.2.jar"] = "ee3d34dce4a30c7d3002cadf8c9172c1",
        ["jna-4.4.0.jar"] = "34d3537524a6c8c134e840e7be601569",
        ["scala-reflect-2.11.1.jar"] = "8b829b97a4258421b7e5c902b8a08ac",
        ["config-1.2.1.jar"] = "7ca00ee2dc5f594451bd5bf78330caeb",
        ["icu4j-core-mojang-51.2.jar"] = "aec124acf7b3c1c6ed41a6270a4452b8",
        ["soundsystem-20120107.jar"] = "6d9d7d6c163caf74984465694d3566e7",
        ["commons-codec-1.10.jar"] = "353cf6a2bdba09595ccfa073b78c7fcb",
        ["scala-library-2.11.1.jar"] = "6f9208af82f7c2811a6c0de049cfe279",
        ["guava-21.0.jar"] = "ddc91fd850fa6177c91aab5d4e4d1fa6",
        ["realms-1.10.17.jar"] = "1e81d120c56f3872736b1462a01e40c4",
        ["jinput-2.0.5.jar"] = "cc07d371f79dc4ed2239e1101ae06313",
        ["MercuriusUpdater-1.12.2.jar"] = "6eb9e61097bee3103a2fdc42746b76a4",
        ["codecwav-20101023.jar"] = "f6a93b7eb8083e4ced92e7e253657057",
        ["httpclient-4.3.3.jar"] = "88cc3123fce88d61b7c2cdbfc33542c5",
        ["jline-3.5.1.jar"] = "4c20d2879ed2bd75a0771ce29e89f6b0",
        ["oshi-core-1.1.jar"] = "4f992d3ac0aa70a8647460494c95e261",
        ["libraryjavasound-20101123.jar"] = "247b45f9d2f0071ad543c14d0ff31d5c",
        ["log4j-core-2.15.1.jar"] = "a5b3833762d0434e8f7cfbd762e81d2e",
        ["scala-actors-migration_2.11-1.1.0.jar"] = "7f7b169667d14b3092a285badf9487f7",
        ["librarylwjglopenal-20100824.jar"] = "93730cef2e75762c5a1431c6d7a0c78e",
        ["jopt-simple-5.0.3.jar"] = "a5ec84e23df9d7cfb4063bc55f2744c",
        ["scala-parser-combinators_2.11-1.0.1.jar"] = "fe20384e064dd025e144bee2858abf1a",
        ["gson-2.8.0.jar"] = "a42f1f5bfa4e6f123ddcab3de7e0ff81",
        ["scala-xml_2.11-1.0.2.jar"] = "a54ca733ef0ede9c343f1ce9ed0b527c",
        ["trove4j-3.0.3.jar"] = "8fc4d4e0129244f9fd39650c5f30feb2",
        ["4676732490521235354@2@14.jar"] = "452edb2762ce1eb89997539a127deb8a",
        ["javassist-1.12.jar"] = "f234fb70031a459370fa2ee4b0b50287",
        ["scala-compiler-2.11.1.jar"] = "d9fc987043fcf8c25d0f73bc7ca36a47",
        ["jutils-1.0.0.jar"] = "f60976b19661c849c5c87433045a9885",
        ["commons-io-2.5.jar"] = "e2d74794fba570ec2115fb9d5b05dc9b",
        ["codecjorbis-20101023.jar"] = "d622e2ac4368b5a33d540a9e4819e0c",
        ["commons-logging-1.1.3.jar"] = "92eb5aabc1b47287de53d45c086a435c",
        ["forge-1.12.2-14.23.5.2768.jar"] = "b21ac8c1404ee9a03f4f4c99385ac815",
        ["log4j-api-2.15.1.jar"] = "cda77c90e14b18419f935b66e030ca3c",
        ["scala-swing_2.11-1.0.1.jar"] = "5d8c98094f1ae58184ed2e087fca7c44",
        ["patchy-1.1.jar"] = "d7dfcf04d1da01d966d3ca806ca8c22f"
    };
}
