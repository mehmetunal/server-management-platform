namespace ServerManager.Domain.Entities;

/// <summary>Kaynak geçmişi: o anda en çok CPU ve bellek kullanan process'ler (kısa JSON; en fazla birkaç düzine satır).</summary>
public class ProcessSnapshot
{
    public long Id { get; set; }

    public Guid ServerId { get; set; }

    public DateTime CollectedAt { get; set; }

    /// <summary>Sunucunun o andaki toplam CPU kullanımı (%); okunamadıysa null.</summary>
    public double? CpuBusyPercent { get; set; }

    public string ProcessesJson { get; set; } = "[]";
}
