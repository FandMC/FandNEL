using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Com4399;

public class Entity4399Device
{
	[JsonPropertyName("device-id")]
	public required string DeviceIdentifier { get; set; }

	[JsonPropertyName("device-id-sm")]
	public required string DeviceIdentifierSm { get; set; }

	[JsonPropertyName("device-udid")]
	public required string DeviceUdid { get; set; }

	[JsonPropertyName("device-state")]
	public string? DeviceState { get; set; }
}
