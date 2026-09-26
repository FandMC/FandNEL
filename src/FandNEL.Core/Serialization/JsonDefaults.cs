using System.Text.Json;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Serialization;

public static class JsonDefaults
{
    public static JsonSerializerOptions Create()
    {
        return new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false
        };
    }
}
