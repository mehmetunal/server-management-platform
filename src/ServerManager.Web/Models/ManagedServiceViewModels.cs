using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.ManagedServices;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ManagedServiceIndexViewModel
{
    public IReadOnlyList<ManagedServiceListItemDto> Services { get; init; } = [];

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];

    public Guid? ServerId { get; init; }

    /// <summary>Sunucu sayfasındaki sekmede gösteriliyorsa sunucu başlığı; genel listede null.</summary>
    public ServerPageViewModel? Page { get; init; }

    public bool ShowServer => Page is null;
}

public sealed class ManagedServiceCreateViewModel
{
    /// <summary>Seçilen şablon; null ise şablon kartları gösterilir.</summary>
    public ServiceTemplate? Template { get; init; }

    public required CreateManagedServiceDto Form { get; init; }

    public IReadOnlyList<ServerOptionDto> Servers { get; init; } = [];

    public bool AllowPrivilegedPorts { get; init; }

    /// <summary>Veritabanı şablonu ve backup.manage yetkisi varsa "Otomatik yedek" bölümü gösterilir.</summary>
    public bool CanConfigureBackup { get; init; }

    public IReadOnlyList<BackupStorageOptionDto> BackupStorages { get; init; } = [];
}

public sealed class ManagedServiceDetailsViewModel
{
    public required ManagedServiceDetailsDto Service { get; init; }

    public IReadOnlyList<ManagedServiceOperationDto> Operations { get; init; } = [];

    /// <summary>Ayarlar sekmesinin formu; yönetim yetkisi yoksa null.</summary>
    public UpdateManagedServiceDto? Settings { get; init; }

    public bool AllowPrivilegedPorts { get; init; }

    /// <summary>Servisin bağlı olduğu projeler.</summary>
    public IReadOnlyList<ProjectServiceLinkDto> Links { get; init; } = [];

    /// <summary>"Projeye bağla" (services.manage + deployment.manage).</summary>
    public bool CanLink { get; init; }

    /// <summary>Veritabanı servisi ve backup.manage yetkisi.</summary>
    public bool CanBackup { get; init; }

    public IReadOnlyList<BackupJobListItemDto> BackupJobs { get; init; } = [];

    public IReadOnlyList<BackupStorageOptionDto> BackupStorages { get; init; } = [];
}

/// <summary>Kurulum sihirbazı ve servis sayfasındaki yedek formunun ortak alanları (_BackupFields).</summary>
public sealed record ManagedServiceBackupFieldsModel(IReadOnlyList<BackupStorageOptionDto> Storages, bool AskDatabaseName, bool WithToggle);

/// <summary>Kurulum formu ve Ayarlar sekmesinin ortak alanları (_SettingsFields).</summary>
public sealed record ManagedServiceSettingsFieldsModel(ServiceTemplate Template, ManagedServiceSettingsDto Form, bool AllowPrivilegedPorts, Guid ServerId);

public sealed class ManagedServiceOperationViewModel
{
    public required ManagedServiceOperationDto Operation { get; init; }

    public ManagedServiceDetailsDto? Service { get; init; }
}
