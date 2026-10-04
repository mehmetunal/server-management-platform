using ServerManager.Application.ServerSystem;
using ServerManager.Infrastructure.ServerSystem;

namespace ServerManager.Application.Tests.ServerSystem;

public class ServerSystemParserTests
{
    [Fact]
    public void ParseServices_reads_systemd_units_and_enabled_state()
    {
        const string output = """
            @@ss:systemd
            nginx.service      loaded active   running A high performance web server
            ● backup.service   loaded failed   failed  Nightly backup
            cron.service       loaded inactive dead    Regular background program processing daemon
            dev-sda.device     loaded active   plugged Disk
            @@ss:unitfiles
            nginx.service enabled enabled
            backup.service disabled enabled
            @@ss:end
            """;

        var result = ServerSystemParser.ParseServices(output);

        Assert.Equal(ServiceManagerKind.Systemd, result.Manager);
        Assert.Equal(["backup.service", "cron.service", "nginx.service"], result.Units.Select(u => u.Name));

        var nginx = result.Units.Single(u => u.Name == "nginx.service");
        Assert.True(nginx.IsRunning);
        Assert.True(nginx.Enabled);
        Assert.Equal("A high performance web server", nginx.Description);

        var backup = result.Units.Single(u => u.Name == "backup.service");
        Assert.True(backup.IsFailed);
        Assert.False(backup.Enabled);

        Assert.Null(result.Units.Single(u => u.Name == "cron.service").Enabled);
    }

    [Fact]
    public void ParseServices_reads_openrc_runlevels()
    {
        const string output = """
            @@ss:openrc
            Runlevel: default
             sshd                                                    [  started  ]
             crond                                                   [  stopped  ]
            Dynamic Runlevel: hotplugged
            Runlevel: boot
             sshd                                                    [  started  ]
             networking                                              [  crashed  ]
            @@ss:enabled
                             sshd |      default
                            crond |
                       networking | boot
            @@ss:end
            """;

        var result = ServerSystemParser.ParseServices(output);

        Assert.Equal(ServiceManagerKind.OpenRc, result.Manager);
        Assert.Equal(["crond", "networking", "sshd"], result.Units.Select(u => u.Name));
        Assert.True(result.Units.Single(u => u.Name == "sshd").IsRunning);
        Assert.True(result.Units.Single(u => u.Name == "sshd").Enabled);
        Assert.False(result.Units.Single(u => u.Name == "crond").Enabled);
        Assert.True(result.Units.Single(u => u.Name == "networking").IsFailed);
    }

    [Fact]
    public void ParseServices_without_manager_returns_none()
    {
        var result = ServerSystemParser.ParseServices("@@ss:none\n@@ss:end\n");

        Assert.Equal(ServiceManagerKind.None, result.Manager);
        Assert.Empty(result.Units);
    }

    [Fact]
    public void ParseProcesses_reads_procps_columns_with_spaces_in_command()
    {
        const string output = """
            @@ss:procps
                1     0 root      0.0  0.1  4096 86400 /sbin/init splash
              812     1 www-data 12.5  3.2 65536   120 nginx: worker process
            @@ss:end
            """;

        var result = ServerSystemParser.ParseProcesses(output);

        Assert.True(result.HasCpuUsage);
        Assert.Equal(2, result.TotalCount);
        var worker = result.Processes.Single(p => p.Pid == 812);
        Assert.Equal(1, worker.ParentPid);
        Assert.Equal("www-data", worker.User);
        Assert.Equal(12.5, worker.CpuPercent);
        Assert.Equal(65536, worker.ResidentKilobytes);
        Assert.Equal(120, worker.ElapsedSeconds);
        Assert.Equal("nginx: worker process", worker.Command);
    }

    [Fact]
    public void ParseProcesses_reads_busybox_and_sorts_by_memory()
    {
        const string output = """
            @@ss:busybox
            PID   PPID  USER     RSS  COMMAND
                1     0 root      1m  /sbin/init
               42     1 deploy  2.5m  sshd: deploy [priv]
               77    42 deploy   900  sh
            @@ss:end
            """;

        var result = ServerSystemParser.ParseProcesses(output);

        Assert.False(result.HasCpuUsage);
        Assert.Equal([42, 1, 77], result.Processes.Select(p => p.Pid));
        Assert.Equal(2560, result.Processes[0].ResidentKilobytes);
        Assert.Equal("sshd: deploy [priv]", result.Processes[0].Command);
        Assert.Null(result.Processes[0].CpuPercent);
    }

    [Theory]
    [InlineData("900", 900L)]
    [InlineData("1m", 1024L)]
    [InlineData("1.5g", 1572864L)]
    [InlineData("12k", 12L)]
    [InlineData("x", null)]
    public void ParseSizeKilobytes_understands_busybox_suffixes(string value, long? expected) =>
        Assert.Equal(expected, ServerSystemParser.ParseSizeKilobytes(value));

    [Fact]
    public void ParseLogSources_keeps_only_safe_paths()
    {
        const string output = """
            @@ss:journal
            yes
            @@ss:files
            /var/log/syslog
            /var/log/nginx/access.log
            /var/log/bad name.log
            /etc/passwd
            @@ss:end
            """;

        var (hasJournal, files) = ServerSystemParser.ParseLogSources(output);

        Assert.True(hasJournal);
        Assert.Equal(["/var/log/syslog", "/var/log/nginx/access.log"], files);
    }

    [Fact]
    public void ParseNetwork_merges_links_addresses_and_counters()
    {
        const string output = """
            @@ss:hostname
            web-01
            @@ss:addr
            1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
            2: eth0    inet 10.0.0.5/24 brd 10.0.0.255 scope global eth0\       valid_lft forever preferred_lft forever
            2: eth0    inet6 fe80::1/64 scope link \       valid_lft forever preferred_lft forever
            @@ss:link
            1: lo: <LOOPBACK,UP,LOWER_UP> mtu 65536 qdisc noqueue state UNKNOWN mode DEFAULT group default qlen 1000\    link/loopback 00:00:00:00:00:00 brd 00:00:00:00:00:00
            2: eth0@if7: <BROADCAST,MULTICAST,UP,LOWER_UP> mtu 1500 qdisc noqueue state UP mode DEFAULT group default \    link/ether 02:42:ac:11:00:02 brd ff:ff:ff:ff:ff:ff link-netnsid 0
            @@ss:dev
            Inter-|   Receive                                                |  Transmit
             face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
                lo:    1200      10    0    0    0     0          0         0     1200      10    0    0    0     0       0          0
              eth0: 5000000    4000    0    0    0     0          0         0   250000    2000    0    0    0     0       0          0
            @@ss:route
            default via 10.0.0.1 dev eth0
            10.0.0.0/24 dev eth0 proto kernel scope link src 10.0.0.5
            @@ss:dns
            # generated
            nameserver 1.1.1.1
            nameserver 8.8.8.8
            @@ss:ports
            @@ss:end
            """;

        var result = ServerSystemParser.ParseNetwork(output);

        Assert.Equal("web-01", result.Hostname);
        Assert.Equal(["eth0", "lo"], result.Interfaces.Select(i => i.Name));

        var eth0 = result.Interfaces[0];
        Assert.Equal("UP", eth0.State);
        Assert.Equal("02:42:ac:11:00:02", eth0.MacAddress);
        Assert.Equal(1500, eth0.Mtu);
        Assert.Equal(["10.0.0.5/24", "fe80::1/64"], eth0.Addresses);
        Assert.Equal(5000000, eth0.ReceivedBytes);
        Assert.Equal(250000, eth0.TransmittedBytes);

        Assert.Null(result.Interfaces[1].MacAddress);
        Assert.Equal(2, result.Routes.Count);
        Assert.Equal(["1.1.1.1", "8.8.8.8"], result.DnsServers);
    }

    [Fact]
    public void ParseStorage_reads_typed_df_inodes_and_lsblk()
    {
        const string output = """
            @@ss:df
            Filesystem     Type     1024-blocks     Used Available Capacity Mounted on
            /dev/sda1      ext4        41152736 20576368  18463620      53% /
            proc           proc               0        0         0       -  /proc
            tmpfs          tmpfs        1018512        0   1018512       0% /run/user data
            @@ss:inodes
            Filesystem      Inodes  IUsed   IFree IUse% Mounted on
            /dev/sda1      2621440 262144 2359296   10% /
            @@ss:lsblk
            NAME="sda" TYPE="disk" SIZE="42949672960" MOUNTPOINT="" FSTYPE="" MODEL="QEMU HARDDISK   "
            NAME="sda1" TYPE="part" SIZE="42948624384" MOUNTPOINT="/" FSTYPE="ext4" MODEL=""
            NAME="loop0" TYPE="loop" SIZE="1000" MOUNTPOINT="/snap/x" FSTYPE="squashfs" MODEL=""
            @@ss:end
            """;

        var result = ServerSystemParser.ParseStorage(output);

        Assert.Equal(["/", "/run/user data"], result.FileSystems.Select(f => f.MountPoint));
        var root = result.FileSystems[0];
        Assert.Equal("ext4", root.Type);
        Assert.Equal(41152736, root.SizeKilobytes);
        Assert.Equal(53, root.UsePercent);
        Assert.Equal(10, root.InodeUsePercent);
        Assert.Null(result.FileSystems[1].InodeUsePercent);

        Assert.True(result.BlockDevicesAvailable);
        Assert.Equal(["sda", "sda1"], result.BlockDevices.Select(d => d.Name));
        Assert.Equal("QEMU HARDDISK", result.BlockDevices[0].Model);
        Assert.Null(result.BlockDevices[0].MountPoint);
        Assert.Equal("ext4", result.BlockDevices[1].FileSystem);
    }

    [Fact]
    public void ParseStorage_reads_busybox_df_without_type_column()
    {
        const string output = """
            @@ss:df
            Filesystem           1024-blocks    Used Available Capacity Mounted on
            overlay                 61202244 30601122  27460900  53% /
            shm                        65536        0     65536   0% /dev/shm
            @@ss:inodes
            @@ss:lsblk
            @@ss:end
            """;

        var result = ServerSystemParser.ParseStorage(output);

        Assert.Equal(2, result.FileSystems.Count);
        Assert.All(result.FileSystems, f => Assert.Null(f.Type));
        Assert.Equal(53, result.FileSystems.Single(f => f.MountPoint == "/").UsePercent);
        Assert.False(result.BlockDevicesAvailable);
    }

    [Theory]
    [InlineData("nginx.service", true)]
    [InlineData("getty@tty1.service", true)]
    [InlineData("sshd", true)]
    [InlineData("-help", false)]
    [InlineData("nginx; rm -rf /", false)]
    [InlineData("a b", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidServiceName_rejects_options_and_shell_characters(string? name, bool expected) =>
        Assert.Equal(expected, ServerSystemRules.IsValidServiceName(name));

    [Theory]
    [InlineData("/var/log/syslog", true)]
    [InlineData("/var/log/nginx/error.log", true)]
    [InlineData("/var/log/../../etc/shadow", false)]
    [InlineData("/etc/passwd", false)]
    [InlineData("/var/log/a$(id)", false)]
    [InlineData("/var/logfile", false)]
    public void IsValidLogPath_allows_only_var_log(string path, bool expected) =>
        Assert.Equal(expected, ServerSystemRules.IsValidLogPath(path));

    [Theory]
    [InlineData(0, LogRequest.DefaultLines)]
    [InlineData(1, 10)]
    [InlineData(500, 500)]
    [InlineData(99999, LogRequest.MaxLines)]
    public void NormalizeLines_clamps_to_supported_range(int input, int expected) =>
        Assert.Equal(expected, ServerSystemRules.NormalizeLines(input));

    [Fact]
    public void ApplyFilter_is_case_insensitive()
    {
        var lines = new[] { "Started nginx", "ERROR disk full", "error again" };

        Assert.Equal(["ERROR disk full", "error again"], ServerSystemRules.ApplyFilter(lines, " error "));
        Assert.Equal(lines, ServerSystemRules.ApplyFilter(lines, null));
    }
}
