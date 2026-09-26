using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.NetGame;

public class EntityNetGameServerAddress
{
	[JsonPropertyName("entity_id")]
	public string EntityId { get; set; } = string.Empty;


	[JsonPropertyName("isp_enable")]
	public bool IspEnable { get; set; } = true;


	[JsonPropertyName("ip")]
	public string Ip { get; set; } = string.Empty;


	[JsonPropertyName("port")]
	public int Port { get; set; }

	[JsonPropertyName("game_status")]
	public int GameStatus { get; set; }

	[JsonPropertyName("announcement")]
	public string Announcement { get; set; } = string.Empty;


	[JsonPropertyName("in_whitelist")]
	public bool InWhitelist { get; set; }
}
