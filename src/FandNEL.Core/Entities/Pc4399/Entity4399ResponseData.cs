using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.Pc4399;

public class Entity4399ResponseData
{
	[JsonPropertyName("ops")]
	public List<Entity4399OpsItem> Ops { get; set; } = new List<Entity4399OpsItem>();


	[JsonPropertyName("username")]
	public string Username { get; set; } = string.Empty;


	[JsonPropertyName("login_tip")]
	public string LoginTip { get; set; } = string.Empty;


	[JsonPropertyName("sdk_login_data")]
	public string SdkLoginData { get; set; } = string.Empty;

}
