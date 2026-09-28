using System.Text.Json.Serialization;

namespace FandNEL.Proxy.Heypixel;

public class KnownModDescriptor
{
    [JsonPropertyName("mod_name")]
    public string ModName { get; set; } = string.Empty;

    [JsonPropertyName("mod_path")]
    public string ModPath { get; set; } = string.Empty;

    [JsonPropertyName("mod_hash")]
    public string ModHash { get; set; } = string.Empty;

    public override string ToString() => $"Name: {ModName}, Path: {ModPath}, Hash: {ModHash}";
}

public static class KnownModCatalog
{
    private static readonly KnownModDescriptor[] KnownMods =
    [
        new() { ModName = "minecraft", ModPath = @"E:\MCLDownload\Game\.minecraft\libraries\net\minecraft\client\1.20.1-20230612.114412\client-1.20.1-20230612.114412-srg.jar", ModHash = "3c8aa19b710a3a68f721210eb69b74594d13e218" },
        new() { ModName = "saturn", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660717756909@2@8.jar", ModHash = "4c5826a73bdbc052db2fe080c582e59ae8ed227d" },
        new() { ModName = "viaforge", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660735087753@2@8.jar", ModHash = "9df7487be88cc018ab295618c1f88a0a4886608f" },
        new() { ModName = "geckolib", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660633006665@2@8.jar", ModHash = "040e42eb566a764792ba0c77e39a56f020cabb8c" },
        new() { ModName = "heypixel", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660643769709@2@8.jar", ModHash = "e66b8fba3b1a772885244eaf2dacd2e9b241d79f" },
        new() { ModName = "entityculling", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660614912528@2@8.jar", ModHash = "09c17c8794a0e00d2ccb51b8d7c1b812498c5c33" },
        new() { ModName = "mixinextras", ModPath = string.Empty, ModHash = "5c35573a7b76103724799ca1174a69699b8a9aec" },
        new() { ModName = "netease_official", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4681704866889354274@3@0.jar", ModHash = "a9c7c9e3ff02a30f1920b65ee85af9c5e7c25a45" },
        new() { ModName = "waveycapes", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660750230557@2@8.jar", ModHash = "c838d45bd4bb53ed66d66ed3cf3822a7ebd7ba09" },
        new() { ModName = "ferritecore", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660623916601@2@8.jar", ModHash = "417fb6ce8f52abf40bd9d0390371790f9576f8ba" },
        new() { ModName = "embeddium_extra", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660708237753@2@8.jar", ModHash = "bb422d5626bf69841e444a3e545c8e71b6928cf8" },
        new() { ModName = "cloth_config", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660593985565@2@8.jar", ModHash = "c65d07748acc57ceb45d53b3964368b84f34d54f" },
        new() { ModName = "forge", ModPath = @"E:\MCLDownload\Game\.minecraft\libraries\net\minecraftforge\forge\1.20.1-47.3.0\forge-1.20.1-47.3.0-universal.jar", ModHash = "7aeb6f58286b5398dc7dfa1d3db1757e601145c1" },
        new() { ModName = "embeddium", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660604590690@2@8.jar", ModHash = "b772ec4f03fbe9b218e9f28d9441ea0a5724d5cf" },
        new() { ModName = "oculus", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660697310016@2@8.jar", ModHash = "27410903d3af950378776106b76503cfebe7ea3a" },
        new() { ModName = "imblocker", ModPath = @"E:\MCLDownload\Game\.minecraft\mods\4685717660687660960@2@8.jar", ModHash = "65c67fd0c62ad2198902f451bed1f02a5fd15aa5" }
    ];

    public static List<KnownModDescriptor> CreateSessionCopy() =>
        KnownMods.Select(mod => new KnownModDescriptor
        {
            ModName = mod.ModName,
            ModPath = mod.ModPath,
            ModHash = mod.ModHash
        }).ToList();
}
