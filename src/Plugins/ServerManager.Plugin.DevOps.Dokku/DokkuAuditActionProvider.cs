using ServerManager.Application.Auditing;

namespace ServerManager.Plugin.DevOps.Dokku;

public sealed class DokkuAuditActionProvider : IAuditActionProvider
{
    public IReadOnlyDictionary<string, string> GetDisplayNames() => new Dictionary<string, string>
    {
        [DokkuAuditActions.InstallStart] = "Dokku kurulumu başlatıldı",
        [DokkuAuditActions.InstallComplete] = "Dokku kurulumu tamamlandı",
        [DokkuAuditActions.AppRestart] = "Dokku uygulaması yeniden başlatıldı"
    };
}
