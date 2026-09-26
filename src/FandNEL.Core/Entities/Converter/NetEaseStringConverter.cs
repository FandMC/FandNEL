using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Converter;

public class NetEaseStringConverter : JsonConverter<int>
{
	public override int Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
	{
		if (reader.TokenType != JsonTokenType.String)
		{
			return reader.GetInt32();
		}
		return int.Parse(reader.GetString() ?? string.Empty);
	}

	public override void Write(Utf8JsonWriter writer, int value, JsonSerializerOptions options)
	{
		writer.WriteNumberValue(value);
	}
}
