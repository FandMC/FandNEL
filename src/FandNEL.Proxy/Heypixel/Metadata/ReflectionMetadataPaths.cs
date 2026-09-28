namespace FandNEL.Proxy.Heypixel;

public static class ReflectionMetadataPaths
{
    public static string CacheDirectory { get; } =
        Path.Combine(AppContext.BaseDirectory, "data", "heypixel");
}
