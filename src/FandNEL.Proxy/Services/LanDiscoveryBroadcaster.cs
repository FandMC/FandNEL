using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using FandNEL.Proxy.Models;

namespace FandNEL.Proxy.Services;

/// <summary>按 Minecraft Java LAN 发现格式广播当前代理端口。</summary>
internal sealed class LanDiscoveryBroadcaster : IAsyncDisposable
{
    private static readonly IPAddress MulticastAddress = IPAddress.Parse("224.0.2.60");
    private readonly IReadOnlyList<UdpClient> _clients = CreateClients();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly string _message;
    private readonly Task _loop;

    public LanDiscoveryBroadcaster(ServerTarget target, PlayerRole role, int localPort, string motd)
    {
        _message = $"[MOTD] {motd} -> {target.Host}[/MOTD][AD]{localPort}[/AD]";
        _loop = BroadcastLoopAsync();
    }

    private async Task BroadcastLoopAsync()
    {
        var bytes = Encoding.UTF8.GetBytes(_message);
        try
        {
            while (true)
            {
                foreach (var client in _clients)
                {
                    try
                    {
                        await client.SendAsync(bytes, bytes.Length, new IPEndPoint(MulticastAddress, 4445)).ConfigureAwait(false);
                    }
                    catch (SocketException)
                    {
                        // 单个网络接口暂不可用时，继续向其它接口广播。
                    }
                }
                await Task.Delay(TimeSpan.FromSeconds(2), _lifetime.Token).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _lifetime.CancelAsync().ConfigureAwait(false);
        try { await _loop.ConfigureAwait(false); } catch (OperationCanceledException) { }
        foreach (var client in _clients)
            client.Dispose();
        _lifetime.Dispose();
    }

    private static IReadOnlyList<UdpClient> CreateClients()
    {
        var addresses = NetworkInterface.GetAllNetworkInterfaces()
            .Where(network => network.OperationalStatus == OperationalStatus.Up && network.SupportsMulticast
                && network.NetworkInterfaceType != NetworkInterfaceType.Loopback)
            .SelectMany(network => network.GetIPProperties().UnicastAddresses)
            .Select(address => address.Address)
            .Where(address => address.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(address))
            .Distinct()
            .ToArray();
        var clients = new List<UdpClient>(addresses.Length);
        foreach (var address in addresses)
        {
            try
            {
                var client = new UdpClient(AddressFamily.InterNetwork) { MulticastLoopback = true };
                client.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 1);
                client.Client.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastInterface, address.GetAddressBytes());
                clients.Add(client);
            }
            catch (SocketException)
            {
            }
        }
        if (clients.Count == 0)
            clients.Add(new UdpClient(AddressFamily.InterNetwork));
        return clients;
    }
}
