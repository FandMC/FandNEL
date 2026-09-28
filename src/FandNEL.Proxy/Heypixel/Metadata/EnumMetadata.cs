using System.Text.Json.Serialization;

namespace FandNEL.Proxy.Heypixel;

public class EnumMetadata
{
    [JsonPropertyName("enumName")]
    public string EnumName { get; set; } = string.Empty;

    [JsonPropertyName("fields")]
    public Dictionary<string, EnumFieldMetadata> Fields { get; set; } = [];
}

public class EnumFieldMetadata
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    [JsonPropertyName("hashCode")]
    public int HashCode { get; set; }
}
