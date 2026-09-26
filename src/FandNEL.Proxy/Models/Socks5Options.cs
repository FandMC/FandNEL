namespace FandNEL.Proxy.Models;

/// <summary>连接远端服务器时使用的 SOCKS5 配置。</summary>
public sealed record Socks5Options(string Host, int Port, string? Username = null, string? Password = null)
{
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Host))
            throw new ArgumentException("SOCKS5 地址不能为空。", nameof(Host));
        if (Port is < 1 or > 65535)
            throw new ArgumentOutOfRangeException(nameof(Port), "SOCKS5 端口必须介于 1 和 65535 之间。");
        if (Username is null && Password is not null)
            throw new ArgumentException("指定 SOCKS5 密码时必须同时指定用户名。");
    }
}
