namespace ServerManager.Application.Authorization;

/// <summary>Rol düzenleme ekranında çekirdek izinlerin gruplandığı modüller (izin adının nokta öncesi kısmına göre).</summary>
public static class PermissionGroups
{
    public const string Other = "Diğer";

    private static readonly (string Prefix, string Group)[] Map =
    [
        ("dashboard", "Dashboard"),
        ("server.cleanup", "Sistem ve temizlik"),
        ("server", "Sunucular"),
        ("docker", "Docker"),
        ("terminal", "Terminal"),
        ("file", "Dosyalar"),
        ("deployment", "Deployment"),
        ("services", "Servisler"),
        ("alert", "Alarmlar ve izleme"),
        ("backup", "Yedekleme"),
        ("security", "Güvenlik"),
        ("system", "Sistem ve temizlik"),
        ("command", "Toplu komut ve şablonlar"),
        ("template", "Toplu komut ve şablonlar"),
        ("cloud", "Bulut sağlayıcılar"),
        ("user", "Kullanıcılar ve roller"),
        ("roles", "Kullanıcılar ve roller"),
        ("audit", "Audit log"),
        ("plugin", "Eklentiler ve ayarlar"),
        ("settings", "Eklentiler ve ayarlar")
    ];

    /// <summary>Çekirdek iznin modül adı; tanınmayan izinler <see cref="Other"/> grubuna düşer.</summary>
    public static string For(string permission)
    {
        foreach (var (prefix, group) in Map)
        {
            if (string.Equals(permission, prefix, StringComparison.Ordinal)
                || permission.StartsWith(prefix + ".", StringComparison.Ordinal))
                return group;
        }

        return Other;
    }
}
