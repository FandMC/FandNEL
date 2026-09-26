using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Utils;

public static class JsonConventer
{
	public class SingleOrArrayConverter<T> : JsonConverter<List<T>>
	{
		public override List<T>? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions? options)
		{
			List<T> items = new List<T>();
			if (reader.TokenType != JsonTokenType.StartArray)
			{
				items.Add(DeserializeItem(ref reader, options));
				return items;
			}
			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
			{
				items.Add(DeserializeItem(ref reader, options));
			}
			return items;
		}

		private static T DeserializeItem(ref Utf8JsonReader reader, JsonSerializerOptions? options)
		{
			T? item = JsonSerializer.Deserialize<T>(ref reader, options);
			return item is null ? throw new JsonException("Failed to deserialize list item.") : item;
		}

		public override void Write(Utf8JsonWriter writer, List<T> value, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			foreach (T item in value)
			{
				JsonSerializer.Serialize(writer, item, options);
			}
			writer.WriteEndArray();
		}
	}

	public class StringFromNumberOrStringConverter : JsonConverter<string>
	{
		public override string? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			return reader.TokenType switch
			{
				JsonTokenType.Number => reader.GetInt64().ToString(), 
				JsonTokenType.String => reader.GetString(), 
				_ => throw new JsonException("Unsupported token type for string conversion."), 
			};
		}

		public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
		{
			writer.WriteStringValue(value);
		}
	}
}
