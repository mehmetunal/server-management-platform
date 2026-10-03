namespace ServerManager.Application.Interfaces.Deployments;

public interface IGitIntegrationRegistry
{
    /// <summary>Eklentisi kurulu ve etkin olan entegrasyonlar.</summary>
    IReadOnlyList<IGitIntegration> GetEnabled();

    /// <summary>Etkin entegrasyon; eklenti yoksa veya devre dışıysa null.</summary>
    IGitIntegration? Find(string systemName);
}
