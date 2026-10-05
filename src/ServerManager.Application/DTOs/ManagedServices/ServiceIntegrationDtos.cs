using ServerManager.Application.DTOs.Deployments;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.ManagedServices;

/// <summary>Proje ↔ servis bağı (proje sayfasındaki "Bağlı servisler" ve servis sayfasındaki "Bağlı projeler").</summary>
public sealed class ProjectServiceLinkDto
{
    public Guid Id { get; init; }

    public Guid ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public Guid ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string TemplateKey { get; init; } = string.Empty;

    public string TemplateName { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;

    public ManagedServiceStatus ServiceStatus { get; init; }

    /// <summary>Bağlarken projeye yazılan anahtarlar.</summary>
    public IReadOnlyList<string> EnvironmentKeys { get; init; } = [];

    public DateTime CreatedAt { get; init; }

    public string? CreatedBy { get; init; }
}

/// <summary>"Projeye bağla" penceresi: aday projeler ve eklenecek değişkenlerin maskeli önizlemesi.</summary>
public sealed class ServiceLinkPreviewDto
{
    public Guid ServiceId { get; init; }

    public string ServiceName { get; init; } = string.Empty;

    public string ContainerName { get; init; } = string.Empty;

    public IReadOnlyList<ServiceLinkVariablePreviewDto> Variables { get; init; } = [];

    /// <summary>Aynı sunucudaki (komutla dağıtılmayan) projeler.</summary>
    public IReadOnlyList<ServiceLinkProjectOptionDto> Projects { get; init; } = [];
}

/// <param name="Preview">Parolası <c>****</c> ile gizlenmiş değer.</param>
public sealed record ServiceLinkVariablePreviewDto(string Key, string Preview);

/// <param name="ExistingKeys">Projede tanımlı anahtarlar (çakışma uyarısı için; değerler dönmez).</param>
public sealed record ServiceLinkProjectOptionDto(Guid Id, string Name, DeploymentBuildType BuildType, bool IsLinked, IReadOnlyList<string> ExistingKeys);

public sealed class ServiceLinkVariableDto
{
    /// <summary>Önerilen değişkenin adı (değer sunucuda bu anahtardan alınır).</summary>
    public string? SourceKey { get; set; }

    /// <summary>Projeye yazılacak ad (kullanıcı değiştirebilir).</summary>
    public string? Key { get; set; }

    public bool Include { get; set; }
}

/// <summary>"Projeye bağla" formu.</summary>
public sealed class LinkServiceToProjectDto
{
    public Guid ProjectId { get; set; }

    public List<ServiceLinkVariableDto> Variables { get; set; } = [];

    /// <summary>Projede zaten tanımlı anahtarların değeri değiştirilsin mi.</summary>
    public bool Overwrite { get; set; }

    /// <summary>Bağladıktan sonra proje hemen yeniden başlatılsın (deployment.execute gerekir).</summary>
    public bool ApplyNow { get; set; }
}

public sealed class ServiceLinkResultDto
{
    public Guid LinkId { get; init; }

    public Guid ProjectId { get; init; }

    public string ProjectName { get; init; } = string.Empty;

    public EnvironmentChangeResultDto Change { get; init; } = new();
}

/// <summary>Veritabanı servisi için otomatik yedekleme işi (kurulum sihirbazı ve servis sayfası).</summary>
public sealed class ServiceBackupOptionsDto
{
    /// <summary>Kurulum sihirbazında "Otomatik yedek" açık mı; servis sayfasındaki formda yok sayılır.</summary>
    public bool Enabled { get; set; }

    public Guid? StorageId { get; set; }

    /// <summary>Günlük (saatte) veya saatlik aralık.</summary>
    public BackupScheduleType ScheduleType { get; set; } = BackupScheduleType.Daily;

    public string? ScheduleTime { get; set; } = "03:00";

    public int ScheduleIntervalHours { get; set; } = 24;

    public int KeepLast { get; set; } = 7;

    /// <summary>Doluysa (en az 12 karakter) yedek şifrelenir.</summary>
    public string? EncryptionPassphrase { get; set; }

    /// <summary>Servisin kendi veritabanı adı yoksa (SQL Server) yedeklenecek veritabanı.</summary>
    public string? DatabaseName { get; set; }

    public override string ToString() => $"ServiceBackupOptionsDto {{ Enabled = {Enabled}, StorageId = {StorageId}, ScheduleType = {ScheduleType} }}";
}
