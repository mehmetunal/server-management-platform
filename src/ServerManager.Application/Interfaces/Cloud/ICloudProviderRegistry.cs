namespace ServerManager.Application.Interfaces.Cloud;

public interface ICloudProviderRegistry
{
    IReadOnlyList<ICloudProvider> GetEnabled();

    ICloudProvider? Find(string systemName);

    /// <summary>Eklentisi devre dışı olsa da görünen ad; bulunamazsa null.</summary>
    string? FindDisplayName(string systemName);
}
