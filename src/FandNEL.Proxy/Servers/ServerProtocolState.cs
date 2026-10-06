using System.Collections.Concurrent;
using FandNEL.Proxy.Servers.Germ;

namespace FandNEL.Proxy.Servers;

/// <summary>迁移协议的每连接状态；客户端与服务端两个方向共用，因此使用并发容器。</summary>
internal sealed class ServerProtocolState
{
    /// <summary>客户端品牌不含 forge 时置位，Forge 伪造握手按此分支处理。</summary>
    internal bool IsVanilla { get; set; }

    /// <summary>客户端品牌为 lunarclient 时置位。</summary>
    internal bool LunarClient { get; set; }

    /// <summary>客户端自己发过 FML ClientHello，后续握手原样透传。</summary>
    internal bool IsForgeClient { get; set; }

    /// <summary>服务端 FML ServerHello 的协议版本。</summary>
    internal byte? FmlProtocolVersion { get; set; }

    /// <summary>DFDL 服务端下发的反作弊解密密钥。</summary>
    internal string? DfdlClientKey { get; set; }

    /// <summary>germ 分片组包的累积状态。</summary>
    internal GermFragment? GermFragment { get; set; }

    /// <summary>germ GUI 内容缓存，node → 服务端下发的 GUI 内容。</summary>
    internal ConcurrentDictionary<string, GermGuiResponse> GermGuiCache { get; } = new(StringComparer.Ordinal);

    /// <summary>聊天按钮路径 → 点击时需要发给服务端的 germ 包。</summary>
    internal ConcurrentDictionary<string, IReadOnlyList<byte[]>> GuiClickMap { get; } = new(StringComparer.Ordinal);

    /// <summary>聊天输入框路径 → 输入框组件。</summary>
    internal ConcurrentDictionary<string, GermComponent> GuiActiveInputMap { get; } = new(StringComparer.Ordinal);

    /// <summary>当前已激活的聊天输入框；为 null 时不拦截聊天原文。</summary>
    internal GermComponent? ActiveInputField { get; set; }
}
