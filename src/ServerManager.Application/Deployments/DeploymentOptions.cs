namespace ServerManager.Application.Deployments;

public sealed class DeploymentOptions
{
    public const string SectionName = "Deployment";

    /// <summary>Depo erişim kontrolü, fetch ve checkout adımlarının zaman aşımı.</summary>
    public int GitTimeoutSeconds { get; set; } = 300;

    public int BuildTimeoutMinutes { get; set; } = 30;

    public int DeployTimeoutMinutes { get; set; } = 10;

    /// <summary>Deployment kaydında saklanan log (son kısım).</summary>
    public int MaxStoredLogKilobytes { get; set; } = 1024;

    /// <summary>Let's Encrypt hesap e-postası. Panel bu adresle sertifika ister.</summary>
    public string AcmeEmail { get; set; } = string.Empty;
}
