using System.Runtime.Serialization;
using System.Text.Json.Serialization;

namespace FandNEL.Core.Entities.WPFLauncher.RentalGame;

public enum EnumServerType
{
	[EnumMember(Value = "docker")]
	Docker,
	[EnumMember(Value = "vmware")]
	Vmware,
	[EnumMember(Value = "docker_guian")]
	[JsonStringEnumMemberName("docker_guian")]
	DockerGuian
}
