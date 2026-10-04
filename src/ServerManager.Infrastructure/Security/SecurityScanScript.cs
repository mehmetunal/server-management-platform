namespace ServerManager.Infrastructure.Security;

/// <summary>Yalnızca okuma yapan güvenlik tarama betiği; sistemde hiçbir şeyi değiştirmez.</summary>
internal static class SecurityScanScript
{
    public const string SectionPrefix = "@@sm:";
    public const string EndMarker = "@@sm:end";

    // Tek tırnak kullanılmamalı: betik sh -c '...' içinde çalışır.
    private const string Script = """
        export LC_ALL=C
        PATH=$PATH:/usr/sbin:/sbin:/usr/local/sbin
        TO=""
        command -v timeout >/dev/null 2>&1 && TO="timeout 20"
        echo @@sm:meta
        echo "uid=$(id -u)"
        echo "kernel=$(uname -r)"
        grep -E "^PRETTY_NAME=" /etc/os-release 2>/dev/null | head -n 1
        echo @@sm:sshd_t
        $TO sshd -T 2>/dev/null
        echo @@sm:sshd_config
        for F in /etc/ssh/sshd_config.d/*.conf /etc/ssh/sshd_config; do
          if [ -r "$F" ]; then echo "##file $F"; grep -Ev "^[[:space:]]*(#|$)" "$F" 2>/dev/null; fi
        done
        echo @@sm:ports
        if command -v ss >/dev/null 2>&1; then
          echo tool=ss; ss -H -tulnp 2>/dev/null || ss -tulnp 2>/dev/null
        elif command -v netstat >/dev/null 2>&1; then
          echo tool=netstat; netstat -tulnp 2>/dev/null || netstat -tuln 2>/dev/null
        fi
        echo @@sm:firewall
        if command -v ufw >/dev/null 2>&1; then echo "##tool ufw"; $TO ufw status 2>&1 | head -n 80; fi
        if command -v firewall-cmd >/dev/null 2>&1; then echo "##tool firewalld"; $TO firewall-cmd --state 2>&1 | head -n 3; fi
        if command -v nft >/dev/null 2>&1; then echo "##tool nftables"; $TO nft list ruleset 2>&1 | head -n 400; fi
        if command -v iptables >/dev/null 2>&1; then echo "##tool iptables"; $TO iptables -S 2>&1 | head -n 300; fi
        echo @@sm:auth
        P="Failed (password|publickey|keyboard-interactive)|Invalid user"
        L=""
        SRC=""
        if command -v journalctl >/dev/null 2>&1 && [ -d /run/systemd/system ]; then
          SRC=journal
          L=$($TO journalctl --since "24 hours ago" _COMM=sshd _COMM=sshd-session --no-pager -q -o cat 2>/dev/null | grep -E "$P" | tail -n 50000)
        else
          for F in /var/log/auth.log /var/log/secure /var/log/messages; do
            if [ -r "$F" ]; then SRC=$F; L=$(tail -n 20000 "$F" | grep -E "sshd" | grep -E "$P"); break; fi
          done
        fi
        echo "source=$SRC"
        if [ -n "$SRC" ]; then
          N=$(printf "%s\n" "$L" | grep -c .)
          echo "total=$N"
          printf "%s\n" "$L" | grep -oE "from [0-9a-fA-F:.]+" | sort | uniq -c | sort -rn | head -n 5
        fi
        if command -v fail2ban-client >/dev/null 2>&1; then
          if pgrep -f fail2ban-server >/dev/null 2>&1; then echo fail2ban=active; else echo fail2ban=inactive; fi
        fi
        echo @@sm:docker
        if command -v docker >/dev/null 2>&1 || command -v dockerd >/dev/null 2>&1; then
          echo installed=1
          (ps -eo args 2>/dev/null || ps -o args 2>/dev/null) | grep -E "[d]ockerd" | head -n 3 | sed "s/^/proc=/"
          if [ -r /etc/docker/daemon.json ]; then echo "##daemon.json"; head -c 8000 /etc/docker/daemon.json; echo; echo "##end"; fi
          D=$($TO docker ps --format "{{.Names}}|{{.Ports}}" 2>/dev/null)
          if [ $? -eq 0 ]; then echo access=1; printf "%s\n" "$D" | head -n 200 | sed "s/^/ctr=/"; else echo access=0; fi
        fi
        echo @@sm:updates
        if command -v apt-get >/dev/null 2>&1; then
          echo manager=apt
          U=$($TO apt-get -s -o Debug::NoLocking=1 dist-upgrade 2>/dev/null | grep -E "^Inst ")
          echo "pending=$(printf "%s\n" "$U" | grep -c .)"
          echo "security=$(printf "%s\n" "$U" | grep -ci security)"
          if command -v unattended-upgrade >/dev/null 2>&1; then echo "auto=$(apt-config dump APT::Periodic::Unattended-Upgrade 2>/dev/null)"; else echo auto=none; fi
        elif command -v dnf >/dev/null 2>&1; then
          echo manager=dnf
          echo "pending=$($TO dnf -q -C check-update 2>/dev/null | grep -cE "^[A-Za-z0-9]")"
          echo "security=$($TO dnf -q -C updateinfo list --security 2>/dev/null | grep -c .)"
          if systemctl is-enabled dnf-automatic.timer dnf-automatic-install.timer 2>/dev/null | grep -q "^enabled"; then echo auto=1; else echo auto=0; fi
        elif command -v yum >/dev/null 2>&1; then
          echo manager=yum
          echo "pending=$($TO yum -q -C check-update 2>/dev/null | grep -cE "^[A-Za-z0-9]")"
          echo "security=$($TO yum -q -C updateinfo list security 2>/dev/null | grep -c .)"
        elif command -v apk >/dev/null 2>&1; then
          echo manager=apk
          echo "pending=$($TO apk list -u 2>/dev/null | grep -c .)"
        fi
        if [ -f /var/run/reboot-required ]; then echo reboot=1; fi
        if command -v needs-restarting >/dev/null 2>&1; then $TO needs-restarting -r >/dev/null 2>&1; if [ $? -eq 1 ]; then echo reboot=1; fi; fi
        echo @@sm:disk
        if command -v lsblk >/dev/null 2>&1; then echo checked=1; lsblk -rno NAME,TYPE,FSTYPE 2>/dev/null | grep -E "crypt" | head -n 20; fi
        echo @@sm:passwd
        while IFS=: read -r n x uid gid g home sh; do
          if [ "$uid" -eq 0 ] 2>/dev/null || { [ "$uid" -ge 1000 ] 2>/dev/null && [ "$uid" -lt 65534 ]; }; then echo "$n:$uid:$sh"; fi
        done < /etc/passwd
        echo @@sm:groups
        grep -E "^(sudo|wheel|admin):" /etc/group 2>/dev/null
        echo @@sm:shadow
        if [ -r /etc/shadow ]; then
          echo readable=1
          while IFS=: read -r n p rest; do if [ -z "$p" ]; then echo "empty=$n"; fi; done < /etc/shadow
        fi
        echo @@sm:nopasswd
        if [ -r /etc/sudoers ]; then cat /etc/sudoers /etc/sudoers.d/* 2>/dev/null | grep -Ev "^[[:space:]]*#" | grep -E "NOPASSWD" | head -n 20; fi
        echo @@sm:end
        """;

    public static string Command => "sh -c '" + Script.Replace("\r\n", "\n") + "'";
}
