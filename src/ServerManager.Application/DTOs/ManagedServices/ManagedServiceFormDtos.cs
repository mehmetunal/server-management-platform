using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.ManagedServices;

/// <summary>Şablon portunun formdaki karşılığı: yayınlansın mı ve hangi sunucu portuna.</summary>
public sealed class ServicePortFormItem
{
    public int ContainerPort { get; set; }

    public bool Publish { get; set; }

    public int? HostPort { get; set; }
}

public sealed class EnvironmentEntryDto
{
    public string? Key { get; set; }

    public string? Value { get; set; }
}

/// <summary>Kurulumda ve Ayarlar sekmesinde ortak alanlar (ağ, port, kaynak sınırları, ek ortam değişkenleri).</summary>
public abstract class ManagedServiceSettingsDto
{
    public List<ServicePortFormItem> Ports { get; set; } = [];

    /// <summary>Yayınlanan portlar 0.0.0.0'a bağlanır ("Dışarıya aç").</summary>
    public bool ExposePublicly { get; set; }

    /// <summary>Dışarıya açık portlara erişebilecek IP/CIDR'lar; satır, virgül veya boşlukla ayrılır.</summary>
    public string? AllowedSourceIps { get; set; }

    public List<EnvironmentEntryDto> Environment { get; set; } = [];

    public int? MemoryLimitMb { get; set; }

    public decimal? CpuLimit { get; set; }

    public bool JoinProxyNetwork { get; set; }

    /// <summary>Katılınacak mevcut Docker ağları (virgülle ayrılmış).</summary>
    public string? Networks { get; set; }
}

public sealed class CreateManagedServiceDto : ManagedServiceSettingsDto
{
    public Guid ServerId { get; set; }

    public string? TemplateKey { get; set; }

    public string? Name { get; set; }

    public string? ImageTag { get; set; }

    public string? Username { get; set; }

    public string? Password { get; set; }

    public string? Database { get; set; }

    public ManagedServiceVolumeMode VolumeMode { get; set; } = ManagedServiceVolumeMode.NamedVolume;

    public string? HostDataPath { get; set; }

    public override string ToString() => $"CreateManagedServiceDto {{ ServerId = {ServerId}, TemplateKey = {TemplateKey}, Name = {Name} }}";
}

/// <summary>Ayarlar sekmesi: kaydedilir ve container yeniden oluşturulur (volume korunur).</summary>
public sealed class UpdateManagedServiceDto : ManagedServiceSettingsDto
{
    public Guid Id { get; set; }
}

public sealed class UpgradeManagedServiceDto
{
    public string? ImageTag { get; set; }

    /// <summary>Ana sürüm değişikliği uyarısı onaylandı mı.</summary>
    public bool ConfirmMajorUpgrade { get; set; }
}

public sealed class RemoveManagedServiceDto
{
    /// <summary>Onay için servis adı aynen yazılmalıdır.</summary>
    public string? ConfirmationName { get; set; }

    /// <summary>Volume / veri klasörü de silinsin mi (geri alınamaz).</summary>
    public bool RemoveData { get; set; }
}
