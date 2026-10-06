namespace ServerManager.Domain.Entities;

/// <summary>Sunucunun son otomatik temizlik taramasının özeti (silme yapılmaz; "Temizlenebilir alan" alarmı için).</summary>
public class ServerReclaimableSpace
{
    public Guid ServerId { get; set; }

    public DateTime ScannedAt { get; set; }

    /// <summary>Silinebilir tüm öğelerin toplamı.</summary>
    public long ReclaimableBytes { get; set; }

    /// <summary>Yalnızca "güvenli" işaretli öğelerin toplamı.</summary>
    public long SafeReclaimableBytes { get; set; }
}
