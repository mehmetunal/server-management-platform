namespace ServerManager.Application.Tests.TestData;

public static class SecurityScanSamples
{
    public const string RootUbuntu = """
        @@sm:meta
        uid=0
        kernel=6.8.0-45-generic
        PRETTY_NAME="Ubuntu 24.04.1 LTS"
        @@sm:sshd_t
        port 22
        permitrootlogin yes
        passwordauthentication yes
        permitemptypasswords no
        maxauthtries 10
        x11forwarding yes
        pubkeyauthentication yes
        @@sm:sshd_config
        ##file /etc/ssh/sshd_config
        PermitRootLogin no
        @@sm:ports
        tool=ss
        tcp   LISTEN 0      4096         0.0.0.0:22         0.0.0.0:*    users:(("sshd",pid=812,fd=3))
        tcp   LISTEN 0      4096            [::]:22            [::]:*    users:(("sshd",pid=812,fd=4))
        tcp   LISTEN 0      511          0.0.0.0:80         0.0.0.0:*    users:(("nginx",pid=900,fd=6))
        tcp   LISTEN 0      151        127.0.0.1:3306       0.0.0.0:*    users:(("mysqld",pid=1000,fd=21))
        tcp   LISTEN 0      511          0.0.0.0:6379       0.0.0.0:*    users:(("redis-server",pid=1100,fd=6))
        tcp   LISTEN 0      4096      10.0.0.5:5432         0.0.0.0:*    users:(("postgres",pid=1200,fd=5))
        udp   UNCONN 0      0      127.0.0.53%lo:53         0.0.0.0:*    users:(("systemd-resolve",pid=600,fd=13))
        tcp   ESTAB  0      0          10.0.0.5:22      203.0.113.9:51515 users:(("sshd",pid=2000,fd=4))
        @@sm:firewall
        ##tool ufw
        Status: inactive
        ##tool nftables
        table ip filter {
        	chain INPUT {
        		type filter hook input priority filter; policy accept;
        	}
        	chain DOCKER {
        		iifname != "docker0" tcp dport 8080 accept
        	}
        }
        ##tool iptables
        -P INPUT ACCEPT
        -P FORWARD DROP
        -P OUTPUT ACCEPT
        -A FORWARD -j DOCKER-USER
        @@sm:auth
        source=journal
        total=742
              500 from 198.51.100.7
              200 from 203.0.113.50
               42 from 2001:db8::1
        @@sm:docker
        installed=1
        proc=/usr/bin/dockerd -H fd:// -H tcp://0.0.0.0:2375 --containerd=/run/containerd/containerd.sock
        ##daemon.json
        {"log-driver":"json-file","hosts":["unix:///var/run/docker.sock"]}
        ##end
        access=1
        ctr=web|0.0.0.0:8080->80/tcp, [::]:8080->80/tcp
        ctr=db|127.0.0.1:5433->5432/tcp
        ctr=worker|
        @@sm:updates
        manager=apt
        pending=12
        security=3
        auto=APT::Periodic::Unattended-Upgrade "0";
        reboot=1
        @@sm:disk
        checked=1
        @@sm:passwd
        root:0:/bin/bash
        toor:0:/bin/sh
        ubuntu:1000:/bin/bash
        svc:1001:/usr/sbin/nologin
        @@sm:groups
        sudo:x:27:ubuntu,deploy
        @@sm:shadow
        readable=1
        empty=svc
        @@sm:nopasswd
        ubuntu ALL=(ALL) NOPASSWD:ALL
        @@sm:end
        """;

    public const string RestrictedAlpine = """
        @@sm:meta
        uid=1000
        kernel=6.6.32-0-lts
        PRETTY_NAME="Alpine Linux v3.20"
        @@sm:sshd_t
        @@sm:sshd_config
        ##file /etc/ssh/sshd_config
        AllowTcpForwarding no
        PasswordAuthentication no
        Match User backup
            PasswordAuthentication yes
        @@sm:ports
        tool=netstat
        Active Internet connections (only servers)
        Proto Recv-Q Send-Q Local Address           Foreign Address         State       PID/Program name
        tcp        0      0 0.0.0.0:22              0.0.0.0:*               LISTEN      -
        tcp        0      0 :::22                   :::*                    LISTEN      -
        udp        0      0 0.0.0.0:68              0.0.0.0:*                           -
        @@sm:firewall
        ##tool iptables
        iptables v1.8.10 (nf_tables): Could not fetch rule set generation id: Permission denied (you must be root)
        @@sm:auth
        source=
        @@sm:docker
        @@sm:updates
        manager=apk
        pending=0
        @@sm:disk
        @@sm:passwd
        root:0:/bin/ash
        deploy:1000:/bin/ash
        @@sm:groups
        wheel:x:10:root
        @@sm:shadow
        @@sm:nopasswd
        @@sm:end
        """;
}
