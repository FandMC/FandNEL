namespace FandNEL.Proxy.Servers.Germ;

/// <summary>germ 组件类型；本次只迁移 isDefault 路径，恒为按钮。</summary>
internal enum GermComponentType
{
    Button
}

/// <summary>germ 按钮：聊天里的一条可点击条目及其点击时要发给服务端的包序列。</summary>
internal sealed class GermComponent
{
    internal GermComponentType Type { get; init; }
    internal string GuiUuid { get; init; } = string.Empty;
    internal string ButtonName { get; init; } = string.Empty;
    internal string? ButtonTips { get; init; }
    internal string ButtonPath { get; init; } = string.Empty;
    internal List<byte[]> ClickAction { get; init; } = [];
}

/// <summary>服务端下发的 germ GUI 内容。</summary>
internal sealed record GermGuiResponse(string Top, string Node, string Content);

/// <summary>germ -1 分片组包的累积状态。</summary>
internal sealed class GermFragment
{
    private const int MaximumWrappedLength = 8_388_608;

    internal bool Start { get; set; }
    internal int FullLength { get; set; }
    internal bool End { get; set; }
    internal byte[]? FullData { get; set; }
    internal int CurrentIndex { get; set; }
    internal bool FinalData { get; set; }

    /// <summary>把一个分片写入缓冲区；返回组装完成的完整数据，未完成时返回 null。</summary>
    internal byte[]? Append(byte[] fragment)
    {
        if (Start)
        {
            if (FullLength < 0 || FullLength > MaximumWrappedLength)
                throw new InvalidDataException($"Germ 分片总长度 {FullLength} 超出允许范围");
            FullData = new byte[FullLength];
            CurrentIndex = 0;
            FinalData = false;
        }
        else if (End)
        {
            FinalData = true;
        }

        if (FullData is null || CurrentIndex < 0 || fragment.Length > FullData.Length - CurrentIndex)
            throw new InvalidDataException("Germ 分片数据超过声明的总长度");
        Array.Copy(fragment, 0, FullData, CurrentIndex, fragment.Length);
        CurrentIndex += fragment.Length;
        return FinalData ? FullData : null;
    }
}
