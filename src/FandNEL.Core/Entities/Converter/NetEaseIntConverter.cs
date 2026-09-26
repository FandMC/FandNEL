using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Converter;

public class NetEaseIntConverter : JsonConverter<string>
{
	public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		string? value;
		if (reader.TokenType != JsonTokenType.Number)
		{
			value = reader.GetString();
			if (value == null)
			{
				return string.Empty;
			}
		}
		else
		{
			value = reader.GetInt32().ToString();
		}
		return value;
	}

	public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
	{
		writer.WriteStringValue(value);
	}
}
