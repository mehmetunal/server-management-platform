using ServerManager.Application.Interfaces.Backups;

namespace ServerManager.Application.Backups;

/// <summary>Çözülmüş ayarlarıyla kullanıma hazır depolama; yalnızca bellekte tutulur.</summary>
public sealed record BackupStorageTarget(Guid Id, string Name, IBackupStorageProvider Provider, IReadOnlyDictionary<string, string> Settings);
