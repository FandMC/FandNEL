using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.MPay;

public class EntityDeviceResponse
{
	[JsonPropertyName("device")]
	public EntityDevice EntityDevice { get; set; } = new EntityDevice();

}
