namespace ServerManager.Application.Interfaces.Backups;

public interface IBackupStorageRegistry
{
    IReadOnlyList<IBackupStorageProvider> GetEnabled();

    IBackupStorageProvider? Find(string systemName);

    /// <summary>Eklentisi devre dışı olsa da görünen ad; bulunamazsa null.</summary>
    string? FindDisplayName(string systemName);
}
