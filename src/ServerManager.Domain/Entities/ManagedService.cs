using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

/// <summary>
/// Panelden tek tıkla kurulan Docker tabanlı servis (veritabanı veya uygulama). Container adı <c>sm-svc-&lt;slug&gt;</c>;
/// kimlik bilgileri ve ek ortam değişkenleri şifreli saklanır.
/// </summary>
public class ManagedService : BaseEntity
{
    public Guid ServerId { get; set; }

    public Server? Server { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Sunucu içinde benzersiz; Docker adları bundan türetilir ve oluşturulduktan sonra değişmez.</summary>
    public string Slug { get; set; } = string.Empty;

    public string TemplateKey { get; set; } = string.Empty;

    public string ImageTag { get; set; } = string.Empty;

    public string ContainerName { get; set; } = string.Empty;

    /// <summary>Kullanıcı adı, parola, veritabanı adı ve şablona özel gizli değerler (JSON, şifreli).</summary>
    public string EncryptedCredentials { get; set; } = string.Empty;

    /// <summary>Ek ortam değişkenleri; <c>ANAHTAR=değer</c> satırları (şifreli).</summary>
    public string? EncryptedEnvironment { get; set; }

    /// <summary>Container portu → sunucu portu eşlemeleri (JSON). Sunucu portu boşsa port yayınlanmaz.</summary>
    public string PortBindings { get; set; } = "[]";

    /// <summary>true ise yayınlanan portlar 0.0.0.0'a, değilse 127.0.0.1'e bağlanır.</summary>
    public bool ExposePublicly { get; set; }

    /// <summary>Dışarıya açık portlara erişebilecek CIDR listesi (satır başına bir). Boşsa herkes erişebilir.</summary>
    public string? AllowedSourceIps { get; set; }

    public ManagedServiceVolumeMode VolumeMode { get; set; } = ManagedServiceVolumeMode.NamedVolume;

    public string? HostDataPath { get; set; }

    public int? MemoryLimitMb { get; set; }

    public decimal? CpuLimit { get; set; }

    /// <summary>Panel ağı (sm-services) dışında katılınacak mevcut Docker ağları; virgülle ayrılmış.</summary>
    public string? Networks { get; set; }

    public bool JoinProxyNetwork { get; set; }

    public ManagedServiceStatus Status { get; set; } = ManagedServiceStatus.Installing;

    public string? LastError { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
