using System.Net;
using System.Net.Sockets;

namespace ServerManager.Infrastructure.Monitoring;

/// <summary>
/// Panelden yapılan uptime/SSL bağlantıları bulut metadata servisleri (169.254.169.254) gibi link-local
/// adreslere gidemez; iç ağ ve loopback izinlidir çünkü panel kendi sunucularını izler.
/// </summary>
public static class NetworkTargetGuard
{
    public static bool IsBlocked(IPAddress address)
    {
        if (address.IsIPv4MappedToIPv6)
            address = address.MapToIPv4();

        if (address.Equals(IPAddress.Any) || address.Equals(IPAddress.IPv6Any) || address.Equals(IPAddress.Broadcast))
            return true;

        if (address.AddressFamily == AddressFamily.InterNetworkV6)
            return address.IsIPv6LinkLocal || address.IsIPv6Multicast;

        var bytes = address.GetAddressBytes();
        return bytes[0] == 169 && bytes[1] == 254
            || bytes[0] >= 224
            || bytes[0] == 0;
    }

    public static async Task<Socket> ConnectAsync(string host, int port, CancellationToken cancellationToken)
    {
        var addresses = IPAddress.TryParse(host, out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        var allowed = addresses.Where(a => !IsBlocked(a)).ToList();
        if (allowed.Count == 0)
        {
            if (addresses.Length > 0)
                throw new BlockedNetworkTargetException(host);
            throw new SocketException((int)SocketError.HostNotFound);
        }

        Exception? last = null;
        foreach (var address in allowed)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, port), cancellationToken);
                return socket;
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                last = ex;
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }

        throw last!;
    }
}
