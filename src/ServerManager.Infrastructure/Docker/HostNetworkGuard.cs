using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;

namespace ServerManager.Infrastructure.Docker;

/// <summary>
/// Sunucunun kendi ağıyla veya panelin bağlandığı adresle çakışan bir subnet, sunucuda daha özel bir rota oluşturur
/// ve SSH dönüş trafiğini yeni bridge'e yönlendirerek sunucuya erişimi keser. Docker bu durumu engellemez.
/// </summary>
internal static partial class HostNetworkGuard
{
    public const string ProbeCommand = "ip -o -4 addr show 2>/dev/null; echo \"ssh-client=${SSH_CONNECTION%% *}\"";

    private static readonly string[] DockerManagedInterfacePrefixes = ["docker", "br-", "veth"];

    /// <returns>Çakışma açıklaması; çakışma yoksa veya ağ bilgisi okunamadıysa null.</returns>
    public static string? FindConflict(string subnet, string? probeOutput)
    {
        if (string.IsNullOrWhiteSpace(probeOutput) || !TryParseCidr(subnet, out var target))
            return null;

        foreach (var rawLine in probeOutput.Split('\n'))
        {
            var line = rawLine.Trim();

            var address = AddressRegex().Match(line);
            if (address.Success)
            {
                var name = address.Groups["iface"].Value.Split('@')[0];
                var cidr = address.Groups["cidr"].Value;
                if (!IsDockerManaged(name) && TryParseCidr(cidr, out var network) && Overlaps(target, network))
                    return $"Subnet, sunucunun {name} arayüzündeki {cidr} ağıyla çakışıyor.";
                continue;
            }

            if (line.StartsWith("ssh-client=", StringComparison.Ordinal)
                && IPAddress.TryParse(line["ssh-client=".Length..], out var client)
                && client.AddressFamily == AddressFamily.InterNetwork
                && Contains(target, ToUInt32(client)))
            {
                return $"Subnet, panelin sunucuya bağlandığı {client} adresini kapsıyor.";
            }
        }

        return null;
    }

    private static bool IsDockerManaged(string interfaceName) =>
        DockerManagedInterfacePrefixes.Any(prefix => interfaceName.StartsWith(prefix, StringComparison.Ordinal));

    private static bool TryParseCidr(string value, out (uint Network, int Prefix) cidr)
    {
        cidr = default;
        var parts = value.Split('/');
        if (parts.Length != 2
            || !IPAddress.TryParse(parts[0], out var address)
            || address.AddressFamily != AddressFamily.InterNetwork
            || !int.TryParse(parts[1], out var prefix)
            || prefix is < 0 or > 32)
            return false;

        cidr = (ToUInt32(address) & Mask(prefix), prefix);
        return true;
    }

    private static bool Overlaps((uint Network, int Prefix) a, (uint Network, int Prefix) b)
    {
        var mask = Mask(Math.Min(a.Prefix, b.Prefix));
        return (a.Network & mask) == (b.Network & mask);
    }

    private static bool Contains((uint Network, int Prefix) cidr, uint address) =>
        (address & Mask(cidr.Prefix)) == cidr.Network;

    private static uint Mask(int prefix) => prefix == 0 ? 0 : uint.MaxValue << (32 - prefix);

    private static uint ToUInt32(IPAddress address)
    {
        var bytes = address.GetAddressBytes();
        return ((uint)bytes[0] << 24) | ((uint)bytes[1] << 16) | ((uint)bytes[2] << 8) | bytes[3];
    }

    [GeneratedRegex(@"^\d+:\s+(?<iface>\S+)\s+inet\s+(?<cidr>\d{1,3}(?:\.\d{1,3}){3}/\d{1,2})")]
    private static partial Regex AddressRegex();
}
