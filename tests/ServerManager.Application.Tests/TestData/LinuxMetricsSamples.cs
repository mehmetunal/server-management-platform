namespace ServerManager.Application.Tests.TestData;

public static class LinuxMetricsSamples
{
    public const string Full = """
        @@T1
        1000.00 3800.00
        @@STAT1
        cpu  1000 0 500 8000 500 0 0 0 0 0
        @@NET1
        Inter-|   Receive                                                |  Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
            lo: 5000 50 0 0 0 0 0 0 5000 50 0 0 0 0 0 0
          eth0: 1000000 1000 0 0 0 0 0 0 500000 800 0 0 0 0 0 0
        veth12ab: 300 3 0 0 0 0 0 0 300 3 0 0 0 0 0 0
        @@T2
        1001.00 3803.80
        @@STAT2
        cpu  1100 0 550 8300 550 0 0 0 0 0
        @@NET2
        Inter-|   Receive                                                |  Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
            lo: 6000 60 0 0 0 0 0 0 6000 60 0 0 0 0 0 0
          eth0: 1102400 1100 0 0 0 0 0 0 551200 850 0 0 0 0 0 0
        veth12ab: 400 4 0 0 0 0 0 0 400 4 0 0 0 0 0 0
          gre0: 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
          sit0: 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0 0
        @@THREADS
        4
        @@CORES
        2
        @@MEMINFO
        MemTotal:        8000000 kB
        MemFree:         1000000 kB
        MemAvailable:    6000000 kB
        Buffers:          200000 kB
        Cached:          2000000 kB
        SwapTotal:       2000000 kB
        SwapFree:        1500000 kB
        @@LOADAVG
        0.52 0.40 0.31 1/234 5678
        @@DF
        Filesystem     1024-blocks     Used Available Capacity Mounted on
        overlay           100000000 45000000  55000000      45% /
        tmpfs                 65536        0     65536       0% /dev
        /dev/sda1         100000000 45000000  55000000      45% /etc/hosts
        /dev/sdb1          50000000 46000000   4000000      92% /data
        /dev/vda1         100000000 45000000  55000000      45% /config
        shm                   65536        0     65536       0% /dev/shm
        @@DFI
        Filesystem       Inodes   IUsed    IFree IUse% Mounted on
        overlay         6000000  600000  5400000   10% /
        /dev/sdb1       3000000 2400000   600000   80% /data
        @@PS
            PID USER     %CPU %MEM COMMAND
           1234 www-data 12.5  3.2 nginx
              1 root      0.5  0.1 systemd
           4321 deploy    0.3  0.0 ps
           2222 mysql     4.0 20.1 mysqld
        @@HOSTNAME
        web-01
        @@KERNEL
        6.8.0-45-generic
        @@ARCH
        x86_64
        @@OSRELEASE
        NAME="Ubuntu"
        VERSION="24.04.1 LTS (Noble Numbat)"
        PRETTY_NAME="Ubuntu 24.04.1 LTS"
        @@TZ
        Europe/Istanbul
        @@TZABBR
        +03
        @@TEMP
        48500
        @@ADDR
        1: lo    inet 127.0.0.1/8 scope host lo\       valid_lft forever preferred_lft forever
        2: eth0    inet 10.0.0.5/24 brd 10.0.0.255 scope global eth0\       valid_lft forever preferred_lft forever
        2: eth0    inet6 fe80::1/64 scope link \       valid_lft forever preferred_lft forever
        @@END
        """;

    public const string BusyBoxMinimal = """
        @@T1
        50.10 40.00
        @@STAT1
        cpu  100 0 100 800 0 0 0 0
        @@NET1
        Inter-|   Receive                                                |  Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
          eth0:1000 10 0 0 0 0 0 0 2000 20 0 0 0 0 0 0
        @@T2
        51.12 41.00
        @@STAT2
        cpu  110 0 110 980 0 0 0 0
        @@NET2
        Inter-|   Receive                                                |  Transmit
         face |bytes    packets errs drop fifo frame compressed multicast|bytes    packets errs drop fifo colls carrier compressed
          eth0:3040 30 0 0 0 0 0 0 4040 40 0 0 0 0 0 0
        @@THREADS
        2
        @@CORES
        0
        @@MEMINFO
        MemTotal:        1000000 kB
        MemFree:          400000 kB
        Buffers:           50000 kB
        Cached:           150000 kB
        SwapTotal:             0 kB
        SwapFree:              0 kB
        @@LOADAVG
        0.00 0.01 0.05 1/50 99
        @@DF
        Filesystem           1024-blocks    Used Available Capacity Mounted on
        overlay                 20000000  5000000  15000000  25% /
        @@DFI
        @@PS
        @@HOSTNAME
        alpine
        @@KERNEL
        6.10.14-linuxkit
        @@ARCH
        aarch64
        @@OSRELEASE
        NAME="Alpine Linux"
        @@TZ
        @@TZABBR
        UTC
        @@TEMP
        @@ADDR
        @@END
        """;
}
