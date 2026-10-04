namespace ServerManager.Application.Security;

public sealed class UpdateFacts
{
    /// <summary>apt, dnf, yum veya apk; bulunamadıysa boş.</summary>
    public string? Manager { get; init; }

    /// <summary>Paket yöneticisinin önbelleğine göre; liste güncel değilse eksik olabilir.</summary>
    public int? Pending { get; init; }

    public int? Security { get; init; }

    public bool RebootRequired { get; init; }

    /// <summary>Otomatik güvenlik güncellemeleri (unattended-upgrades / dnf-automatic); bilinmiyorsa boş.</summary>
    public bool? AutoUpdates { get; init; }
}
