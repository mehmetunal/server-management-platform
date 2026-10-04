using ServerManager.Domain.Enums;

namespace ServerManager.Application.Security;

/// <summary>Toplanan bilgilerden bulgu ve puan üretir. Yalnızca tespit eder; düzeltme komutları öneri olarak döner.</summary>
public static class SecurityAnalyzer
{
    public const string SshCategory = "SSH";
    public const string NetworkCategory = "Ağ ve portlar";
    public const string FirewallCategory = "Güvenlik duvarı";
    public const string DockerCategory = "Docker";
    public const string UpdatesCategory = "Güncellemeler";
    public const string UsersCategory = "Kullanıcılar";
    public const string DiskCategory = "Disk";

    public const int FailedLoginWarningThreshold = 200;
    public const int CriticalPenalty = 25;
    public const int WarningPenalty = 10;

    private const string ReloadSsh = "(sudo systemctl reload ssh 2>/dev/null || sudo systemctl reload sshd 2>/dev/null || sudo rc-service sshd reload)";

    public static SecurityReport Analyze(SecurityFacts facts)
    {
        var findings = new List<SecurityFinding>();
        AddSsh(facts, findings);
        AddFailedLogins(facts, findings);
        var riskyExposed = AddPorts(facts, findings);
        AddFirewall(facts, findings, riskyExposed);
        AddDocker(facts, findings);
        AddUpdates(facts, findings);
        AddUsers(facts, findings);
        AddDisk(facts, findings);

        var critical = findings.Count(f => f.Status == SecurityCheckStatus.Critical);
        var warning = findings.Count(f => f.Status == SecurityCheckStatus.Warning);
        return new SecurityReport
        {
            Score = Score(critical, warning),
            CriticalCount = critical,
            WarningCount = warning,
            Findings = findings,
            Facts = facts
        };
    }

    public static int Score(int critical, int warning) =>
        Math.Clamp(100 - (critical * CriticalPenalty) - (warning * WarningPenalty), 0, 100);

    private static void AddSsh(SecurityFacts facts, List<SecurityFinding> findings)
    {
        var ssh = facts.Ssh;
        if (ssh.Source is null)
        {
            findings.Add(new("ssh.config", SshCategory, "SSH ayarları", SecurityCheckStatus.Unknown,
                "SSH sunucu ayarları okunamadı (sshd -T root ister, /etc/ssh/sshd_config de okunamadı).",
                "Taramayı sudo yetkili bir kullanıcıyla çalıştırın."));
            return;
        }

        var note = ssh.Source == SshFacts.ConfigFileSource
            ? " Ayarlar sshd_config dosyasından okundu; Match blokları hesaba katılmadı."
            : string.Empty;

        // Değer yoksa OpenSSH varsayılanı geçerlidir.
        string Setting(string key, string fallback) =>
            ssh.Settings.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value) ? value.Trim().ToLowerInvariant() : fallback;

        var passwordAuth = Setting("passwordauthentication", "yes") == "yes";
        var rootLogin = Setting("permitrootlogin", "prohibit-password");

        if (rootLogin == "yes")
        {
            findings.Add(new("ssh.root_login", SshCategory, "Root ile SSH girişi açık",
                passwordAuth ? SecurityCheckStatus.Critical : SecurityCheckStatus.Warning,
                "PermitRootLogin yes: root kullanıcısı SSH ile doğrudan girebilir" + (passwordAuth ? " ve parola ile giriş de açık." : ".") + note,
                "Yönetim için sudo yetkili ayrı bir kullanıcı kullanın; root girişini kapatın veya yalnızca anahtara izin verin. /etc/ssh/sshd_config.d altındaki dosyaları da kontrol edin.",
                "sudo sed -i -E \"s/^#?PermitRootLogin.*/PermitRootLogin prohibit-password/\" /etc/ssh/sshd_config && " + ReloadSsh));
        }
        else
        {
            findings.Add(new("ssh.root_login", SshCategory, "Root ile SSH girişi", SecurityCheckStatus.Pass,
                (rootLogin == "no" ? "Root ile SSH girişi kapalı." : "Root yalnızca SSH anahtarıyla girebilir.") + note));
        }

        findings.Add(passwordAuth
            ? new("ssh.password_auth", SshCategory, "Parola ile SSH girişi açık", SecurityCheckStatus.Warning,
                "PasswordAuthentication yes: hesaplar kaba kuvvet (parola deneme) saldırılarına açık." + note,
                "Tüm kullanıcıların SSH anahtarıyla girebildiğini doğruladıktan sonra parola girişini kapatın.",
                "sudo sed -i -E \"s/^#?PasswordAuthentication.*/PasswordAuthentication no/\" /etc/ssh/sshd_config && " + ReloadSsh)
            : new("ssh.password_auth", SshCategory, "Parola ile SSH girişi", SecurityCheckStatus.Pass,
                "Parola ile giriş kapalı; yalnızca anahtar kabul ediliyor." + note));

        findings.Add(Setting("permitemptypasswords", "no") == "yes"
            ? new("ssh.empty_passwords", SshCategory, "Boş parolayla SSH girişi açık", SecurityCheckStatus.Critical,
                "PermitEmptyPasswords yes: parolası boş hesaplar SSH ile girebilir." + note,
                "PermitEmptyPasswords no yapın.",
                "sudo sed -i -E \"s/^#?PermitEmptyPasswords.*/PermitEmptyPasswords no/\" /etc/ssh/sshd_config && " + ReloadSsh)
            : new("ssh.empty_passwords", SshCategory, "Boş parolayla SSH girişi", SecurityCheckStatus.Pass, "Boş parolalı hesaplar SSH ile giremez."));

        if (int.TryParse(Setting("maxauthtries", "6"), out var maxTries) && maxTries > 6)
        {
            findings.Add(new("ssh.max_auth_tries", SshCategory, "Bağlantı başına deneme sayısı yüksek", SecurityCheckStatus.Info,
                $"MaxAuthTries {maxTries}: tek bağlantıda {maxTries} parola denenebilir.",
                "MaxAuthTries değerini 3-6 arasında tutun."));
        }

        if (Setting("x11forwarding", "no") == "yes")
        {
            findings.Add(new("ssh.x11", SshCategory, "X11 yönlendirme açık", SecurityCheckStatus.Info,
                "X11Forwarding yes. Sunucuda grafik uygulama çalıştırmıyorsanız gerekmez.",
                "X11Forwarding no yapın."));
        }
    }

    private static void AddFailedLogins(SecurityFacts facts, List<SecurityFinding> findings)
    {
        var logins = facts.FailedLogins;
        var unreadable = logins.Source is null || logins.Total is null || (!facts.IsRoot && logins.Total == 0);
        if (unreadable)
        {
            findings.Add(new("ssh.failed_logins", SshCategory, "Başarısız SSH girişleri", SecurityCheckStatus.Unknown,
                "SSH giriş kayıtları okunamadı (journal veya /var/log/auth.log için root ya da adm grubu gerekir)."));
            return;
        }

        var period = logins.Source == "journal" ? "Son 24 saatte" : "Log dosyasının son kayıtlarında";
        var top = logins.TopSources.Count == 0
            ? string.Empty
            : " En çok deneyen adresler: " + string.Join(", ", logins.TopSources.Take(5).Select(s => $"{s.Address} ({s.Count})")) + ".";
        var fail2Ban = logins.Fail2BanActive switch
        {
            true => " fail2ban etkin.",
            false => " fail2ban kurulu ama çalışmıyor.",
            _ => string.Empty
        };

        if (logins.Total == 0)
        {
            findings.Add(new("ssh.failed_logins", SshCategory, "Başarısız SSH girişleri", SecurityCheckStatus.Pass, $"{period} başarısız SSH girişi yok.{fail2Ban}"));
            return;
        }

        if (logins.Total >= FailedLoginWarningThreshold && logins.Fail2BanActive != true)
        {
            findings.Add(new("ssh.failed_logins", SshCategory, "Yoğun SSH giriş denemesi", SecurityCheckStatus.Warning,
                $"{period} {logins.Total} başarısız SSH girişi.{top}{fail2Ban}",
                "fail2ban ile tekrar eden adresleri engelleyin ve parola girişini kapatın.",
                InstallCommand(facts.Updates.Manager, "fail2ban")));
            return;
        }

        findings.Add(new("ssh.failed_logins", SshCategory, "Başarısız SSH girişleri", SecurityCheckStatus.Info,
            $"{period} {logins.Total} başarısız SSH girişi.{top}{fail2Ban}"));
    }

    /// <returns>Kritik seviyede riskli bir servis tüm arayüzlerde veya genel bir adreste dinleniyorsa true.</returns>
    private static bool AddPorts(SecurityFacts facts, List<SecurityFinding> findings)
    {
        if (!facts.PortsAvailable)
        {
            findings.Add(new("ports.listening", NetworkCategory, "Dinlenen portlar", SecurityCheckStatus.Unknown,
                "Dinlenen portlar okunamadı (ss veya netstat bulunamadı)."));
            return false;
        }

        var reachable = facts.Ports.Where(p => p.IsReachableFromNetwork).ToList();
        var riskyExposed = false;
        var flagged = 0;
        var ufwPresent = facts.Firewall.Tools.Any(t => t.Name == "ufw");

        foreach (var group in reachable.GroupBy(p => (p.Port, p.Protocol)).OrderBy(g => g.Key.Port))
        {
            var known = PortCatalog.Find(group.Key.Port);
            if (known?.Risk is not { } risk)
                continue;

            var widest = group.OrderBy(p => p.Exposure == PortExposure.PrivateAddress ? 1 : 0).First();
            var status = widest.Exposure == PortExposure.PrivateAddress ? Downgrade(risk) : risk;
            if (status == SecurityCheckStatus.Critical)
                riskyExposed = true;

            var where = string.Join(", ", group.Select(p => $"{FormatAddress(p.Address)}:{p.Port}").Distinct());
            var process = group.Select(p => p.Process).FirstOrDefault(p => !string.IsNullOrEmpty(p));
            findings.Add(new($"ports.{group.Key.Port}.{group.Key.Protocol}", NetworkCategory,
                $"{known.Service} ağa açık ({group.Key.Port}/{group.Key.Protocol})", status,
                $"{where} adresinde dinleniyor{(process is null ? string.Empty : $" ({process})")}. {known.Note}",
                "Servisi 127.0.0.1'e bağlayın veya güvenlik duvarıyla yalnızca gereken adreslere izin verin.",
                ufwPresent ? $"sudo ufw deny {group.Key.Port}/{group.Key.Protocol}" : null));
            flagged++;
        }

        if (flagged == 0)
        {
            findings.Add(new("ports.risky", NetworkCategory, "Riskli servisler", SecurityCheckStatus.Pass,
                "Veritabanı, Docker API, Telnet gibi riskli servisler ağa açık değil."));
        }

        var publicPorts = reachable.Select(p => p.Port).Distinct().Order().ToList();
        if (publicPorts.Count > 0)
        {
            findings.Add(new("ports.summary", NetworkCategory, "Ağdan erişilebilen portlar", SecurityCheckStatus.Info,
                $"{publicPorts.Count} port ağdan erişilebilir: {string.Join(", ", publicPorts.Take(30))}{(publicPorts.Count > 30 ? "…" : string.Empty)}. Kullanmadığınız servisleri kapatın."));
        }

        return riskyExposed;
    }

    private static void AddFirewall(SecurityFacts facts, List<SecurityFinding> findings, bool riskyExposed)
    {
        var firewall = facts.Firewall;
        if (firewall.AnyActive)
        {
            var active = firewall.Tools.Where(t => t.Active == true).Select(t => string.IsNullOrEmpty(t.Detail) ? t.Name : $"{t.Name} ({t.Detail})");
            findings.Add(new("firewall.status", FirewallCategory, "Güvenlik duvarı", SecurityCheckStatus.Pass,
                "Güvenlik duvarı etkin: " + string.Join(", ", active) + "."));
            return;
        }

        if (firewall.Tools.Count > 0 && !firewall.StateKnown)
        {
            findings.Add(new("firewall.status", FirewallCategory, "Güvenlik duvarı", SecurityCheckStatus.Unknown,
                $"Kurulu araç: {string.Join(", ", firewall.Tools.Select(t => t.Name))}; durum okunamadı (root gerekir)."));
            return;
        }

        var ufw = firewall.Tools.Any(t => t.Name == "ufw");
        var firewalld = firewall.Tools.Any(t => t.Name == "firewalld");
        var command = ufw
            ? "sudo ufw allow OpenSSH && sudo ufw enable"
            : firewalld
                ? "sudo systemctl enable --now firewalld"
                : facts.Updates.Manager == "apt" ? "sudo apt install ufw && sudo ufw allow OpenSSH && sudo ufw enable" : null;

        findings.Add(new("firewall.status", FirewallCategory,
            firewall.Tools.Count == 0 ? "Güvenlik duvarı kurulu değil" : "Güvenlik duvarı etkin değil",
            riskyExposed ? SecurityCheckStatus.Critical : SecurityCheckStatus.Warning,
            firewall.Tools.Count == 0
                ? "ufw, firewalld, nftables veya iptables kuralı bulunamadı; ağa açık her servis internetten erişilebilir."
                : $"Kurulu araç: {string.Join(", ", firewall.Tools.Select(t => t.Name))}; hiçbiri etkin değil veya kural tanımlı değil.",
            "Önce SSH portuna izin verip sonra güvenlik duvarını açın; aksi halde bağlantınız kesilir. Bulut sağlayıcının ağ güvenlik grubunu da kontrol edin.",
            command));
    }

    private static void AddDocker(SecurityFacts facts, List<SecurityFinding> findings)
    {
        var docker = facts.Docker;
        if (!docker.Installed)
            return;

        var tcpHosts = docker.DaemonHosts.Where(h => h.StartsWith("tcp://", StringComparison.OrdinalIgnoreCase)).ToList();
        var remoteHosts = tcpHosts.Where(h => !IsLoopbackHost(h)).ToList();
        if (remoteHosts.Count > 0)
        {
            findings.Add(new("docker.tcp", DockerCategory, "Docker API ağa açık",
                docker.TlsVerify ? SecurityCheckStatus.Warning : SecurityCheckStatus.Critical,
                $"Docker daemon TCP üzerinden dinliyor: {string.Join(", ", remoteHosts)}" +
                (docker.TlsVerify ? " (istemci sertifikası doğrulaması açık)." : ". TLS doğrulaması olmadan bu adrese erişen herkes sunucuda root yetkisi alır."),
                "TCP soketini kapatın; uzaktan erişim gerekiyorsa docker -H ssh://kullanici@sunucu veya TLS + istemci sertifikası kullanın."));
        }
        else
        {
            findings.Add(new("docker.tcp", DockerCategory, "Docker API", SecurityCheckStatus.Pass,
                tcpHosts.Count > 0
                    ? $"Docker API yalnızca yerel adreste dinliyor: {string.Join(", ", tcpHosts)}."
                    : "Docker daemon yalnızca yerel Unix soketinden erişilebilir."));
        }

        if (!docker.Accessible)
        {
            findings.Add(new("docker.access", DockerCategory, "Container portları", SecurityCheckStatus.Unknown,
                "docker komutuna erişim yok; container'ların yayınladığı portlar listelenemedi."));
            return;
        }

        var open = docker.PublishedPorts.Where(p => p.HostAddress is "0.0.0.0" or "::" or "").ToList();
        if (open.Count == 0)
            return;

        var ufwActive = facts.Firewall.Tools.Any(t => t.Name == "ufw" && t.Active == true);
        var list = string.Join(", ", open.Take(15).Select(p => $"{p.Container} {p.HostPort}→{p.ContainerPort}/{p.Protocol}"));
        findings.Add(new("docker.published", DockerCategory, "Container'ların ağa açtığı portlar", SecurityCheckStatus.Info,
            $"Tüm arayüzlerde yayınlanan portlar: {list}{(open.Count > 15 ? "…" : string.Empty)}." +
            (ufwActive ? " Docker bu portlar için ufw kurallarını atlar (iptables DOCKER zinciri)." : string.Empty),
            "Yalnızca reverse proxy üzerinden erişilmesi gereken servisleri 127.0.0.1:PORT:PORT şeklinde yayınlayın; kısıtlama için DOCKER-USER zincirini kullanın."));
    }

    private static void AddUpdates(SecurityFacts facts, List<SecurityFinding> findings)
    {
        var updates = facts.Updates;
        if (updates.Manager is null || updates.Pending is null)
        {
            findings.Add(new("updates.pending", UpdatesCategory, "Paket güncellemeleri", SecurityCheckStatus.Unknown,
                "Paket güncellemeleri kontrol edilemedi (apt, dnf, yum veya apk bulunamadı)."));
        }
        else
        {
            const string cacheNote = " Sonuç paket listesinin en son yenilendiği zamana göredir.";
            var upgrade = UpgradeCommand(updates.Manager);
            if (updates.Security > 0)
            {
                findings.Add(new("updates.pending", UpdatesCategory, "Güvenlik güncellemeleri bekliyor", SecurityCheckStatus.Warning,
                    $"{updates.Security} güvenlik güncellemesi bekliyor (toplam {updates.Pending} paket).{cacheNote}",
                    "Güvenlik güncellemelerini bakım penceresinde uygulayın.", upgrade));
            }
            else if (updates.Pending > 0)
            {
                findings.Add(new("updates.pending", UpdatesCategory, "Paket güncellemeleri bekliyor", SecurityCheckStatus.Info,
                    $"{updates.Pending} paket güncellemesi bekliyor" + (updates.Security == 0 ? "; güvenlik güncellemesi yok." : ".") + cacheNote,
                    null, upgrade));
            }
            else
            {
                findings.Add(new("updates.pending", UpdatesCategory, "Paket güncellemeleri", SecurityCheckStatus.Pass, "Bekleyen paket güncellemesi yok." + cacheNote));
            }
        }

        if (updates.RebootRequired)
        {
            findings.Add(new("updates.reboot", UpdatesCategory, "Yeniden başlatma gerekiyor", SecurityCheckStatus.Warning,
                "Yüklenen güncellemeler (ör. çekirdek) sunucu yeniden başlatılana kadar etkin değil.",
                "Uygun bir zamanda sunucuyu yeniden başlatın."));
        }

        if (updates.AutoUpdates == false)
        {
            findings.Add(new("updates.auto", UpdatesCategory, "Otomatik güvenlik güncellemeleri kapalı", SecurityCheckStatus.Info,
                "Güvenlik yamaları elle uygulanana kadar bekler.",
                "Otomatik güvenlik güncellemelerini açın.",
                updates.Manager switch
                {
                    "apt" => "sudo apt install unattended-upgrades && sudo dpkg-reconfigure -plow unattended-upgrades",
                    "dnf" => "sudo dnf install dnf-automatic && sudo systemctl enable --now dnf-automatic.timer",
                    _ => null
                }));
        }
    }

    private static void AddUsers(SecurityFacts facts, List<SecurityFinding> findings)
    {
        var users = facts.Users;
        if (users.Accounts.Count > 0)
        {
            var extraRoot = users.Accounts.Where(a => a.Uid == 0 && a.Name != "root").Select(a => a.Name).ToList();
            findings.Add(extraRoot.Count > 0
                ? new("users.uid0", UsersCategory, "root dışında UID 0 hesabı var", SecurityCheckStatus.Critical,
                    $"Bu hesaplar root yetkisine sahip: {string.Join(", ", extraRoot)}.",
                    "Hesabın neden oluşturulduğunu araştırın; gerekmiyorsa kilitleyin veya kaldırın.")
                : new("users.uid0", UsersCategory, "Root yetkili hesaplar", SecurityCheckStatus.Pass, "Yalnızca root hesabının UID'si 0."));

            var loginAccounts = users.Accounts.Where(a => a.CanLogin).Select(a => a.Name).ToList();
            findings.Add(new("users.login", UsersCategory, "Giriş yapabilen hesaplar", SecurityCheckStatus.Info,
                loginAccounts.Count == 0 ? "Kabuğu olan kullanıcı hesabı yok." : $"Kabuğu olan hesaplar: {string.Join(", ", loginAccounts)}. Kullanılmayanları kilitleyin."));
        }

        if (!users.ShadowReadable)
        {
            findings.Add(new("users.empty_password", UsersCategory, "Boş parolalı hesaplar", SecurityCheckStatus.Unknown,
                "Parola durumu okunamadı (/etc/shadow root ister)."));
        }
        else if (users.EmptyPasswordUsers.Count > 0)
        {
            findings.Add(new("users.empty_password", UsersCategory, "Boş parolalı hesap var", SecurityCheckStatus.Critical,
                $"Parolası boş hesaplar: {string.Join(", ", users.EmptyPasswordUsers)}.",
                "Bu hesaplara parola verin veya kilitleyin.",
                "sudo passwd -l " + string.Join(" && sudo passwd -l ", users.EmptyPasswordUsers)));
        }
        else
        {
            findings.Add(new("users.empty_password", UsersCategory, "Boş parolalı hesaplar", SecurityCheckStatus.Pass, "Parolası boş hesap yok."));
        }

        var sudoText = users.SudoMembers.Count == 0
            ? "sudo, wheel veya admin grubunda üye yok."
            : $"sudo/wheel/admin grubu üyeleri: {string.Join(", ", users.SudoMembers)}.";
        if (users.NoPasswordSudoRules.Count > 0)
            sudoText += $" Parolasız sudo kuralı: {users.NoPasswordSudoRules.Count} ({string.Join(" | ", users.NoPasswordSudoRules.Take(3))}).";
        findings.Add(new("users.sudo", UsersCategory, "Sudo yetkili kullanıcılar", SecurityCheckStatus.Info, sudoText));
    }

    private static void AddDisk(SecurityFacts facts, List<SecurityFinding> findings)
    {
        var disk = facts.Disk;
        if (!disk.Checked)
        {
            findings.Add(new("disk.encryption", DiskCategory, "Disk şifrelemesi", SecurityCheckStatus.Unknown, "Disk şifrelemesi kontrol edilemedi (lsblk bulunamadı)."));
            return;
        }

        findings.Add(disk.EncryptedDevices.Count > 0
            ? new("disk.encryption", DiskCategory, "Disk şifrelemesi", SecurityCheckStatus.Pass, $"Şifreli aygıtlar: {string.Join(", ", disk.EncryptedDevices)}.")
            : new("disk.encryption", DiskCategory, "Disk şifrelemesi tespit edilmedi", SecurityCheckStatus.Info,
                "LUKS ile şifrelenmiş aygıt yok. Bulut sağlayıcıları diskleri altyapı tarafında şifreleyebilir; hassas veri varsa sağlayıcı ayarını kontrol edin."));
    }

    private static SecurityCheckStatus Downgrade(SecurityCheckStatus status) => status switch
    {
        SecurityCheckStatus.Critical => SecurityCheckStatus.Warning,
        SecurityCheckStatus.Warning => SecurityCheckStatus.Info,
        _ => status
    };

    private static string FormatAddress(string address) => address.Contains(':') ? $"[{address}]" : address;

    private static bool IsLoopbackHost(string host)
    {
        var value = host["tcp://".Length..];
        return value.StartsWith("127.", StringComparison.Ordinal)
               || value.StartsWith("localhost", StringComparison.OrdinalIgnoreCase)
               || value.StartsWith("[::1]", StringComparison.Ordinal);
    }

    private static string? UpgradeCommand(string? manager) => manager switch
    {
        "apt" => "sudo apt update && sudo apt upgrade",
        "dnf" => "sudo dnf upgrade --security",
        "yum" => "sudo yum update --security",
        "apk" => "sudo apk update && sudo apk upgrade",
        _ => null
    };

    private static string? InstallCommand(string? manager, string package) => manager switch
    {
        "apt" => $"sudo apt install {package}",
        "dnf" => $"sudo dnf install {package}",
        "yum" => $"sudo yum install {package}",
        "apk" => $"sudo apk add {package}",
        _ => null
    };
}
