namespace ServerManager.Application.Interfaces.Notifications;

public interface INotificationChannelRegistry
{
    /// <summary>Eklentisi etkin olan sağlayıcılar.</summary>
    IReadOnlyList<INotificationChannelProvider> GetEnabled();

    INotificationChannelProvider? Find(string systemName);

    /// <summary>Eklentisi devre dışı olsa da yüklü sağlayıcının adı (liste ekranında göstermek için).</summary>
    string? FindDisplayName(string systemName);
}
