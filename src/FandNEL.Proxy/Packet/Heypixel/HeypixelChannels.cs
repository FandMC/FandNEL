namespace FandNEL.Proxy.Packet.Heypixel;

/// <summary>Heypixel 及其兼容插件使用的自定义频道。</summary>
public static class HeypixelChannels
{
    public const string Event = "heypixel:s2cevent";
    public const string Skin = "heypixel:sync_skins";
    public const string Form = "floodgate:form";
    public static IReadOnlyList<string> Required { get; } = Array.AsReadOnly(new[]
    {
        "worldedit:cui", "fml:loginwrapper", "forge:tier_sorting", "storemod:buy",
        "floodgate:custom", "floodgate:packet", Event, "report:areport",
        "plugin:guild", "fml:play", "floodgate:netease", "floodgate:transfer",
        "fml:handshake", "heypixel:onlinestats", "forge:split", Form,
        "geckolib:main", "floodgate:skin"
    });
}
