namespace ServerManager.Application.Terminal;

public sealed class DangerousCommandRule
{
    /// <summary>Komut satırında aranan .NET regex deseni (büyük/küçük harf duyarsız).</summary>
    public string Pattern { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>Komut adının satır başında, ayırıcıdan (; &amp; | ( `), boşluktan veya yol ayracından sonra gelmesi.</summary>
    private const string CommandStart = @"(^|[;&|(`/]|\s)";

    public static IReadOnlyList<DangerousCommandRule> Defaults { get; } =
    [
        new() { Pattern = CommandStart + @"rm\s+(-\S+\s+)*(-[a-z]*r[a-z]*|--recursive)\b", Description = "Özyinelemeli silme (rm -r / rm -rf)" },
        new() { Pattern = CommandStart + @"mkfs(\.\w+)?\b", Description = "Dosya sistemi biçimlendirme (mkfs)" },
        new() { Pattern = CommandStart + @"dd\s+.*\bof=", Description = "dd ile ham yazma" },
        new() { Pattern = CommandStart + @"(shutdown|poweroff|halt)\b", Description = "Sunucuyu kapatma" },
        new() { Pattern = CommandStart + @"reboot\b", Description = "Sunucuyu yeniden başlatma" },
        new() { Pattern = CommandStart + @"systemctl\s+(\S+\s+)*(poweroff|reboot|halt|kexec)\b", Description = "systemctl ile kapatma / yeniden başlatma" },
        new() { Pattern = CommandStart + @"(iptables|ip6tables|nft|ufw)\b", Description = "Güvenlik duvarı kuralı değişikliği" },
        new() { Pattern = CommandStart + @"userdel\b", Description = "Kullanıcı silme (userdel)" },
        new() { Pattern = @">\s*/dev/(sd|nvme|vd|xvd|hd)[a-z0-9]*\b", Description = "Disk aygıtının üzerine yazma" },
        new() { Pattern = @":\(\)\s*\{\s*:\s*\|\s*:\s*&\s*\}\s*;\s*:", Description = "Fork bomb" }
    ];
}
