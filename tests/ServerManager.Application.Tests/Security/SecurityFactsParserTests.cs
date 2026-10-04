using ServerManager.Application.Security;
using ServerManager.Application.Tests.TestData;
using ServerManager.Infrastructure.Security;

namespace ServerManager.Application.Tests.Security;

public class SecurityFactsParserTests
{
    [Fact]
    public void Parses_meta_and_prefers_effective_sshd_settings()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RootUbuntu);

        Assert.True(facts.IsRoot);
        Assert.Equal("Ubuntu 24.04.1 LTS", facts.OperatingSystem);
        Assert.Equal("6.8.0-45-generic", facts.Kernel);
        Assert.Equal(SshFacts.EffectiveSource, facts.Ssh.Source);
        Assert.Equal("yes", facts.Ssh.Settings["permitrootlogin"]);
        Assert.Equal("10", facts.Ssh.Settings["maxauthtries"]);
    }

    [Fact]
    public void Falls_back_to_config_file_and_ignores_match_blocks()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RestrictedAlpine);

        Assert.False(facts.IsRoot);
        Assert.Equal(SshFacts.ConfigFileSource, facts.Ssh.Source);
        Assert.Equal("no", facts.Ssh.Settings["passwordauthentication"]);
        Assert.False(facts.Ssh.Settings.ContainsKey("permitrootlogin"));
    }

    [Fact]
    public void Parses_ss_listening_sockets_with_exposure_and_process()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RootUbuntu);

        Assert.True(facts.PortsAvailable);
        Assert.DoesNotContain(facts.Ports, p => p.Address == "10.0.0.5" && p.Port == 22);
        var redis = Assert.Single(facts.Ports, p => p.Port == 6379);
        Assert.Equal(PortExposure.AllInterfaces, redis.Exposure);
        Assert.Equal("redis-server", redis.Process);
        Assert.Equal(PortExposure.Loopback, Assert.Single(facts.Ports, p => p.Port == 3306).Exposure);
        Assert.Equal(PortExposure.PrivateAddress, Assert.Single(facts.Ports, p => p.Port == 5432).Exposure);
        var dns = Assert.Single(facts.Ports, p => p.Port == 53);
        Assert.Equal("127.0.0.53", dns.Address);
        Assert.Equal("udp", dns.Protocol);
        Assert.Equal(2, facts.Ports.Count(p => p.Port == 22));
    }

    [Fact]
    public void Parses_netstat_output()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RestrictedAlpine);

        Assert.True(facts.PortsAvailable);
        Assert.Equal(3, facts.Ports.Count);
        Assert.Contains(facts.Ports, p => p is { Protocol: "tcp", Address: "::", Port: 22, Exposure: PortExposure.AllInterfaces });
        Assert.Contains(facts.Ports, p => p is { Protocol: "udp", Port: 68 });
        Assert.All(facts.Ports, p => Assert.Null(p.Process));
    }

    [Fact]
    public void Reads_netstat_program_names_containing_spaces()
    {
        var (_, ports) = SecurityFactsParser.ParsePorts(
        [
            "tool=netstat",
            "tcp        0      0 0.0.0.0:22              0.0.0.0:*               LISTEN      14/sshd -e [listene",
            "tcp        0      0 0.0.0.0:8080            0.0.0.0:*               LISTEN      344/docker-proxy",
            "udp        0      0 0.0.0.0:68              0.0.0.0:*                           51/dhclient"
        ]);

        Assert.Equal(["sshd", "docker-proxy", "dhclient"], ports.Select(p => p.Process));
    }

    [Fact]
    public void Firewall_is_inactive_when_input_chain_has_no_rules()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RootUbuntu);

        Assert.Equal(["ufw", "nftables", "iptables"], facts.Firewall.Tools.Select(t => t.Name));
        Assert.All(facts.Firewall.Tools, t => Assert.False(t.Active));
        Assert.True(facts.Firewall.StateKnown);
        Assert.False(facts.Firewall.AnyActive);
    }

    [Fact]
    public void Firewall_state_is_unknown_without_root()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RestrictedAlpine);

        var tool = Assert.Single(facts.Firewall.Tools);
        Assert.Null(tool.Active);
        Assert.False(facts.Firewall.StateKnown);
    }

    [Fact]
    public void Detects_active_firewalls()
    {
        var ufw = SecurityFactsParser.ParseFirewall(["##tool ufw", "Status: active", "", "To   Action  From", "22/tcp  ALLOW  Anywhere", "80/tcp  ALLOW  Anywhere"], true);
        var nft = SecurityFactsParser.ParseFirewall(["##tool nftables", "table inet filter {", "chain input {", "type filter hook input priority 0; policy drop;", "}", "}"], true);
        var iptables = SecurityFactsParser.ParseFirewall(["##tool iptables", "-P INPUT ACCEPT", "-A INPUT -j ufw-before-input"], true);

        Assert.True(ufw.AnyActive);
        Assert.Equal(2, ufw.Tools[0].Rules.Count);
        Assert.True(nft.AnyActive);
        Assert.True(iptables.AnyActive);
    }

    [Fact]
    public void Parses_failed_login_summary()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RootUbuntu);

        Assert.Equal("journal", facts.FailedLogins.Source);
        Assert.Equal(742, facts.FailedLogins.Total);
        Assert.Equal(new LoginSourceCount("198.51.100.7", 500), facts.FailedLogins.TopSources[0]);
        Assert.Equal("2001:db8::1", facts.FailedLogins.TopSources[2].Address);
        Assert.Null(facts.FailedLogins.Fail2BanActive);
    }

    [Fact]
    public void Parses_docker_hosts_and_published_ports()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RootUbuntu);

        Assert.True(facts.Docker.Installed);
        Assert.True(facts.Docker.Accessible);
        Assert.Contains("tcp://0.0.0.0:2375", facts.Docker.DaemonHosts);
        Assert.Contains("unix:///var/run/docker.sock", facts.Docker.DaemonHosts);
        Assert.False(facts.Docker.TlsVerify);
        Assert.Equal(2, facts.Docker.PublishedPorts.Count);
        Assert.Contains(facts.Docker.PublishedPorts, p => p is { Container: "web", HostAddress: "0.0.0.0", HostPort: 8080, ContainerPort: 80, Protocol: "tcp" });
        Assert.Contains(facts.Docker.PublishedPorts, p => p is { Container: "db", HostAddress: "127.0.0.1" });
    }

    [Fact]
    public void Parses_updates_disk_and_users()
    {
        var facts = SecurityFactsParser.Parse(SecurityScanSamples.RootUbuntu);

        Assert.Equal("apt", facts.Updates.Manager);
        Assert.Equal(12, facts.Updates.Pending);
        Assert.Equal(3, facts.Updates.Security);
        Assert.True(facts.Updates.RebootRequired);
        Assert.False(facts.Updates.AutoUpdates);
        Assert.True(facts.Disk.Checked);
        Assert.Empty(facts.Disk.EncryptedDevices);
        Assert.Equal(4, facts.Users.Accounts.Count);
        Assert.False(facts.Users.Accounts.Single(a => a.Name == "svc").CanLogin);
        Assert.Equal(["ubuntu", "deploy"], facts.Users.SudoMembers);
        Assert.True(facts.Users.ShadowReadable);
        Assert.Equal(["svc"], facts.Users.EmptyPasswordUsers);
        Assert.Single(facts.Users.NoPasswordSudoRules);
    }

    [Fact]
    public void Detects_luks_devices()
    {
        var disk = SecurityFactsParser.ParseDisk(["checked=1", "sda3 part crypto_LUKS", "dm_crypt-0 crypt ext4"]);

        Assert.Equal(["sda3", "dm_crypt-0"], disk.EncryptedDevices);
    }

    [Theory]
    [InlineData("0.0.0.0", PortExposure.AllInterfaces)]
    [InlineData("*", PortExposure.AllInterfaces)]
    [InlineData("::", PortExposure.AllInterfaces)]
    [InlineData("127.0.0.1", PortExposure.Loopback)]
    [InlineData("::1", PortExposure.Loopback)]
    [InlineData("::ffff:127.0.0.1", PortExposure.Loopback)]
    [InlineData("192.168.1.10", PortExposure.PrivateAddress)]
    [InlineData("172.20.0.2", PortExposure.PrivateAddress)]
    [InlineData("fd00::5", PortExposure.PrivateAddress)]
    [InlineData("203.0.113.10", PortExposure.PublicAddress)]
    [InlineData("2001:db8::10", PortExposure.PublicAddress)]
    public void Classifies_listen_addresses(string address, PortExposure expected) =>
        Assert.Equal(expected, SecurityFactsParser.ClassifyAddress(address));

    [Fact]
    public void Rejects_output_without_meta_section() =>
        Assert.Throws<FormatException>(() => SecurityFactsParser.Parse("sudo: a password is required"));

    [Fact]
    public void Script_contains_no_single_quotes_and_is_wrapped_for_sh()
    {
        var command = SecurityScanScript.Command;

        Assert.StartsWith("sh -c '", command);
        Assert.EndsWith("'", command);
        Assert.DoesNotContain('\'', command[7..^1]);
        Assert.Contains(SecurityScanScript.EndMarker, command);
    }
}
