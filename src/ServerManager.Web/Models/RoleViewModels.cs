using ServerManager.Application.ApiKeys;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.ApiKeys;
using ServerManager.Application.DTOs.Roles;

namespace ServerManager.Web.Models;

/// <summary>Kullanıcı formundaki rol seçimi (birden fazla rol seçilebilir).</summary>
public sealed record RoleSelectModel(IReadOnlyList<RoleOptionDto> Options, IReadOnlyCollection<string> Selected, bool CanAssignSuperAdmin);

public sealed class RoleFormViewModel
{
    public required RoleEditorDto Editor { get; init; }

    public bool IsNew => Editor.Form.Id is null;

    /// <summary>Kataloğu modüllere göre gruplar (çekirdek modüller önce, eklentiler sonra).</summary>
    public IReadOnlyList<IGrouping<string, PermissionInfo>> Groups =>
        Editor.Catalog
            .GroupBy(p => p.Group)
            .OrderBy(g => g.First().IsPlugin)
            .ToList();
}

public sealed class ApiKeysPageViewModel
{
    public IReadOnlyList<ApiKeyListItemDto> Keys { get; init; } = [];

    public IReadOnlyList<PermissionInfo> GrantableScopes { get; init; } = [];

    public IReadOnlyList<int?> LifetimeOptions { get; init; } = [];

    public bool Enabled { get; init; }

    public bool CanManageAll { get; init; }

    public string BaseUrl { get; init; } = string.Empty;

    public static string LifetimeLabel(int? days) => days switch
    {
        null => "Süresiz",
        365 => "1 yıl",
        _ => $"{days} gün"
    };

    public static int? DefaultLifetime(IReadOnlyList<int?> options) =>
        options.Contains(90) ? 90 : options.FirstOrDefault(d => d is not null);
}

public sealed class ApiDocsViewModel
{
    public string BaseUrl { get; init; } = string.Empty;

    public bool Enabled { get; init; }

    public int RequestsPerMinute { get; init; }

    public static IReadOnlyList<ApiEndpointDoc> Endpoints { get; } =
    [
        new("GET", "/api/v1/me", "—", "Anahtarın sahibi, adı ve etkin izinleri"),
        new("GET", "/api/v1/servers?search=&status=&page=1&pageSize=20", Permissions.ServerView, "Sunucu listesi (sayfalı)"),
        new("GET", "/api/v1/servers/{id}", Permissions.ServerView, "Sunucu ayrıntısı"),
        new("GET", "/api/v1/servers/{id}/status", Permissions.ServerView, "Durum ve son ölçüm (CPU, bellek, disk)"),
        new("GET", "/api/v1/projects?search=&serverId=", Permissions.DeploymentView, "Deployment projeleri ve son deployment"),
        new("GET", "/api/v1/projects/{id}/deployments", Permissions.DeploymentView, "Projenin deployment geçmişi"),
        new("POST", "/api/v1/projects/{id}/deployments", Permissions.DeploymentExecute, "Deploy başlat (gövde: {\"commitSha\": null}) → 202"),
        new("POST", "/api/v1/projects/{id}/restart", Permissions.DeploymentExecute, "Build etmeden yeniden başlat → 202"),
        new("GET", "/api/v1/deployments/{id}", Permissions.DeploymentView, "Deployment durumu"),
        new("GET", "/api/v1/deployments/{id}/log?tail=200", Permissions.DeploymentView, "Logun son satırları"),
        new("GET", "/api/v1/services?serverId=", Permissions.ServicesView, "Servis listesi"),
        new("GET", "/api/v1/services/{id}", Permissions.ServicesView, "Servis ayrıntısı"),
        new("GET", "/api/v1/services/{id}/status", Permissions.ServicesView, "Container'ın anlık durumu"),
        new("POST", "/api/v1/services/{id}/start | stop | restart", Permissions.ServicesManage, "Servisi başlat / durdur / yeniden başlat"),
        new("GET", "/api/v1/backups/jobs?serverId=", Permissions.BackupView, "Yedekleme işleri"),
        new("POST", "/api/v1/backups/jobs/{id}/run", Permissions.BackupExecute, "Yedeklemeyi hemen başlat → 202"),
        new("GET", "/api/v1/backups/runs?jobId=&status=&page=1", Permissions.BackupView, "Yedek geçmişi (sayfalı)"),
        new("GET", "/api/v1/backups/runs/{id}", Permissions.BackupView, "Yedek ayrıntısı"),
        new("GET", "/api/v1/backups/runs/{id}/download", Permissions.BackupDownload, "Yedek dosyasını indir (akış)"),
        new("GET", "/api/v1/alerts?status=firing|resolved|all", Permissions.AlertView, "Alarmlar (varsayılan: açık olanlar)"),
        new("POST", "/api/v1/alerts/{id}/acknowledge", Permissions.AlertAcknowledge, "Alarmı üstlen")
    ];
}

public sealed record ApiEndpointDoc(string Method, string Path, string Permission, string Description);
