namespace FandNEL.Proxy.Models;

/// <summary>远端 Minecraft 服务器的连接目标。</summary>
public sealed record ServerTarget(string Host, int Port)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
        {
            throw new ArgumentException("服务器地址不能为空。", nameof(Host));
        }

        if (Port is < 1 or > 65535)
        {
            throw new ArgumentOutOfRangeException(nameof(Port), "服务器端口必须介于 1 和 65535 之间。");
        }
    }
}
