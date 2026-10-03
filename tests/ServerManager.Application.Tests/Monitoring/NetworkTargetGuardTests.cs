using System.Net;
using ServerManager.Infrastructure.Monitoring;

namespace ServerManager.Application.Tests.Monitoring;

public class NetworkTargetGuardTests
{
    [Theory]
    [InlineData("169.254.169.254")]
    [InlineData("0.0.0.0")]
    [InlineData("0.1.2.3")]
    [InlineData("255.255.255.255")]
    [InlineData("224.0.0.1")]
    [InlineData("::")]
    [InlineData("fe80::1")]
    [InlineData("ff02::1")]
    [InlineData("::ffff:169.254.169.254")]
    public void Blocks_metadata_link_local_and_unspecified(string address)
    {
        Assert.True(NetworkTargetGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Theory]
    [InlineData("127.0.0.1")]
    [InlineData("10.0.0.5")]
    [InlineData("192.168.1.10")]
    [InlineData("93.184.216.34")]
    [InlineData("::1")]
    [InlineData("2606:2800:220:1:248:1893:25c8:1946")]
    public void Allows_loopback_private_and_public(string address)
    {
        Assert.False(NetworkTargetGuard.IsBlocked(IPAddress.Parse(address)));
    }

    [Fact]
    public async Task Connect_refuses_blocked_literal_address()
    {
        await Assert.ThrowsAsync<BlockedNetworkTargetException>(() =>
            NetworkTargetGuard.ConnectAsync("169.254.169.254", 80, TestContext.Current.CancellationToken));
    }
}
