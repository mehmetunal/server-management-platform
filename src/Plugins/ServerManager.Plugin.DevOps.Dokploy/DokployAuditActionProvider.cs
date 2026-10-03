using ServerManager.Application.Auditing;

namespace ServerManager.Plugin.DevOps.Dokploy;

public sealed class DokployAuditActionProvider : IAuditActionProvider
{
    public IReadOnlyDictionary<string, string> GetDisplayNames() => new Dictionary<string, string>
    {
        [DokployAuditActions.InstallStart] = "Dokploy kurulumu başlatıldı",
        [DokployAuditActions.InstallComplete] = "Dokploy kurulumu tamamlandı",
        [DokployAuditActions.Detected] = "Dokploy tespit edildi",
        [DokployAuditActions.SettingsUpdate] = "Dokploy ayarları güncellendi",
        [DokployAuditActions.ApiKeyRemove] = "Dokploy API anahtarı kaldırıldı"
    };
}
