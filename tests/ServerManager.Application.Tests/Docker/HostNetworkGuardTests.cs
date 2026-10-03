using ServerManager.Infrastructure.Docker;

namespace ServerManager.Application.Tests.Docker;

public class HostNetworkGuardTests
{
    private const string DindProbe = """
        1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
        2: docker0    inet 172.17.0.1/16 brd 172.17.255.255 scope global docker0\       valid_lft forever preferred_lft forever
        3: br-ea60976ce88c    inet 172.18.0.1/16 brd 172.18.255.255 scope global br-ea60976ce88c\       valid_lft forever preferred_lft forever
        25: eth0@if26    inet 172.31.0.3/16 brd 172.31.255.255 scope global eth0\       valid_lft forever preferred_lft forever
        ssh-client=192.168.65.1
        """;

    [Theory]
    [InlineData("172.31.0.0/24", "eth0", "172.31.0.3/16")]
    [InlineData("172.0.0.0/8", "eth0", "172.31.0.3/16")]
    [InlineData("127.10.0.0/16", "lo", "127.0.0.1/8")]
    public void Detects_overlap_with_host_interfaces(string subnet, string iface, string cidr)
    {
        var conflict = HostNetworkGuard.FindConflict(subnet, DindProbe);

        Assert.Equal($"Subnet, sunucunun {iface} arayüzündeki {cidr} ağıyla çakışıyor.", conflict);
    }

    [Theory]
    [InlineData("172.30.0.0/16")]
    [InlineData("172.32.0.0/24")]
    [InlineData("10.10.0.0/16")]
    public void Allows_subnets_outside_host_networks(string subnet)
    {
        Assert.Null(HostNetworkGuard.FindConflict(subnet, DindProbe));
    }

    [Fact]
    public void Ignores_docker_managed_interfaces()
    {
        Assert.Null(HostNetworkGuard.FindConflict("172.17.5.0/24", DindProbe));
        Assert.Null(HostNetworkGuard.FindConflict("172.18.0.0/24", DindProbe));
    }

    [Fact]
    public void Detects_subnet_covering_panel_address()
    {
        var conflict = HostNetworkGuard.FindConflict("192.168.64.0/22", DindProbe);

        Assert.Equal("Subnet, panelin sunucuya bağlandığı 192.168.65.1 adresini kapsıyor.", conflict);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("ssh-client=")]
    public void Missing_network_information_does_not_block(string? probe)
    {
        Assert.Null(HostNetworkGuard.FindConflict("172.31.0.0/24", probe));
    }

    [Fact]
    public void Invalid_subnet_is_ignored()
    {
        Assert.Null(HostNetworkGuard.FindConflict("not-a-subnet", DindProbe));
    }
}
