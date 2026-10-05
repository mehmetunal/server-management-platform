namespace ServerManager.Application.ManagedServices;

public sealed class ManagedServiceOptions
{
    public const string SectionName = "ManagedServices";

    /// <summary>İmaj indirme (docker pull) üst sınırı.</summary>
    public int PullTimeoutMinutes { get; set; } = 20;

    /// <summary>Tek docker komutunun (create, start, network …) üst sınırı.</summary>
    public int CommandTimeoutSeconds { get; set; } = 120;

    /// <summary>
    /// Container'ın sağlıklı duruma gelmesi için beklenen en kısa süre; şablon daha uzun bir süre isterse (SQL Server)
    /// şablonun süresi kullanılır.
    /// </summary>
    public int HealthTimeoutSeconds { get; set; } = 180;

    /// <summary>İşlem logunun veritabanında saklanan en büyük boyutu.</summary>
    public int MaxStoredLogKilobytes { get; set; } = 256;

    /// <summary>1024 altındaki sunucu portlarına izin verilir mi (varsayılan: hayır).</summary>
    public bool AllowPrivilegedHostPorts { get; set; }

    public int DefaultLogTail { get; set; } = 200;
}
