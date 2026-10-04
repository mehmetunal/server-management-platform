namespace ServerManager.Application.Security;

public sealed class FirewallTool
{
    /// <summary>ufw, firewalld, nftables veya iptables.</summary>
    public string Name { get; init; } = string.Empty;

    /// <summary>Durum okunamadıysa (yetki yok) boş.</summary>
    public bool? Active { get; init; }

    public string? Detail { get; init; }

    public IReadOnlyList<string> Rules { get; init; } = [];
}
