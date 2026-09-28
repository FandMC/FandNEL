using DotNetty.Buffers;
using MessagePack = FandNEL.Proxy.Packet.Heypixel.MessagePackBufferExtensions;

namespace FandNEL.Proxy.Packet.Heypixel;

public sealed record KnownModFingerprint(string Name, string Path, string Hash);
public sealed record EncryptedModFingerprint(string EncryptedPath, string EncryptedSha1);

/// <summary>信息报告的线协议数据；指纹生成、文件扫描和身份获取由业务层负责。</summary>
public sealed record InfoReportData(
    IReadOnlyList<KnownModFingerprint> KnownMods,
    string GameDirectory,
    string JavaHome,
    IReadOnlyList<string> ProcessorInfo,
    IReadOnlyList<string> SystemProductInfo,
    IReadOnlyList<string[]> NetworkAdapterInfo,
    IReadOnlyList<string[]> DiskDriveInfo,
    IReadOnlyList<string> AccountDirectories,
    long UserId,
    IReadOnlyList<EncryptedModFingerprint> ModFiles) : HeypixelReportData
{
    public override HeypixelReportType Type => HeypixelReportType.Info;

    public override void Write(IByteBuffer buffer)
    {
        // 原协议中的数量是条目数，每个条目紧接三个字段，没有嵌套数组头。
        MessagePack.WriteArrayHeader(buffer, KnownMods.Count);
        foreach (var mod in KnownMods)
        {
            MessagePack.WriteString(buffer, mod.Name, postprocessUuid: false);
            MessagePack.WriteString(buffer, mod.Path, postprocessUuid: false);
            MessagePack.WriteString(buffer, mod.Hash, postprocessUuid: false);
        }
        MessagePack.WriteString(buffer, GameDirectory);
        MessagePack.WriteString(buffer, JavaHome);
        WriteStringArray(buffer, ProcessorInfo);
        WriteStringArray(buffer, SystemProductInfo);
        WriteNestedStringArray(buffer, NetworkAdapterInfo);
        WriteNestedStringArray(buffer, DiskDriveInfo);
        WriteStringArray(buffer, AccountDirectories);
        MessagePack.WriteMapHeader(buffer, 1);
        MessagePack.WriteString(buffer, "UserId", postprocessUuid: false);
        MessagePack.WriteInt64(buffer, UserId | 0x80000000L);
        MessagePack.WriteArrayHeader(buffer, ModFiles.Count);
        foreach (var mod in ModFiles)
        {
            MessagePack.WriteString(buffer, mod.EncryptedPath, postprocessUuid: false);
            MessagePack.WriteString(buffer, mod.EncryptedSha1, postprocessUuid: false);
        }
    }

    private static void WriteStringArray(IByteBuffer buffer, IReadOnlyList<string> values)
    {
        MessagePack.WriteArrayHeader(buffer, values.Count);
        foreach (var value in values)
            MessagePack.WriteString(buffer, value, postprocessUuid: false);
    }

    private static void WriteNestedStringArray(IByteBuffer buffer, IReadOnlyList<string[]> values)
    {
        MessagePack.WriteArrayHeader(buffer, values.Count);
        foreach (var value in values) WriteStringArray(buffer, value);
    }
}
