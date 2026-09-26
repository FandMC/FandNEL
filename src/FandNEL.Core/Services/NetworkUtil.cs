using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;

namespace FandNEL.Core.Services;

public static class NetworkUtil
{
	public static int GetAvailablePort(int low = 25565, int high = 35565, bool reuseTimeWait = false)
	{
		if (low > high)
		{
			return 0;
		}
		HashSet<int> usedPorts = GetUsedPorts(reuseTimeWait);
		for (int i = low; i <= high; i++)
		{
			if (!usedPorts.Contains(i))
			{
				return i;
			}
		}
		return 0;
	}

	private static HashSet<int> GetUsedPorts(bool reuseTimeWait = true)
	{
		IPGlobalProperties networkProperties = IPGlobalProperties.GetIPGlobalProperties();
		IEnumerable<int> tcpListenerPorts = networkProperties.GetActiveTcpListeners().Select(endpoint => endpoint.Port);
		IEnumerable<int> udpListenerPorts = networkProperties.GetActiveUdpListeners().Select(endpoint => endpoint.Port);
		TcpConnectionInformation[] activeTcpConnections = networkProperties.GetActiveTcpConnections();
		IEnumerable<TcpConnectionInformation> reusableConnections = reuseTimeWait
			? activeTcpConnections.Where(connection => connection.State != TcpState.TimeWait && connection.State != TcpState.CloseWait)
			: activeTcpConnections;
		IEnumerable<int> connectionPorts = reusableConnections.Select(connection => connection.LocalEndPoint.Port);
		HashSet<int> usedPorts = new HashSet<int>();
		foreach (int port in tcpListenerPorts.Concat(udpListenerPorts).Concat(connectionPorts))
		{
			usedPorts.Add(port);
		}

		return usedPorts;
	}
}
