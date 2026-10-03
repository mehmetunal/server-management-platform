using ServerManager.Domain.Common;
using ServerManager.Domain.Enums;

namespace ServerManager.Domain.Entities;

public class DeploymentProject : BaseEntity
{
    public Guid ServerId { get; set; }

    public Server? Server { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Compose proje adı, container ve imaj adı olarak kullanılır; oluşturulduktan sonra değişmez.</summary>
    public string Slug { get; set; } = string.Empty;

    public string? Description { get; set; }

    public GitProvider GitProvider { get; set; } = GitProvider.GitHub;

    public string RepositoryUrl { get; set; } = string.Empty;

    public string Branch { get; set; } = "main";

    public string? GitUsername { get; set; }

    public string? EncryptedAccessToken { get; set; }

    /// <summary>
    /// Depo bir Git entegrasyonu (ör. GitHub App) üzerinden bağlandıysa entegrasyon eklentisinin SystemName'i.
    /// Bu durumda erişim anahtarı saklanmaz; her işlemde entegrasyondan kısa ömürlü anahtar alınır.
    /// </summary>
    public string? GitIntegration { get; set; }

    /// <summary>Entegrasyon içindeki bağlantı (GitHub App kurulumu gibi); biçimi entegrasyona aittir.</summary>
    public string? GitSourceId { get; set; }

    /// <summary>Entegrasyondaki depo adı (ör. <c>firma/uygulama</c>).</summary>
    public string? GitRepository { get; set; }

    public string DeployPath { get; set; } = string.Empty;

    public DeploymentBuildType BuildType { get; set; } = DeploymentBuildType.DockerCompose;

    public string? ComposeFile { get; set; }

    public string? DockerfilePath { get; set; }

    public string? PortMappings { get; set; }

    public string? BuildCommand { get; set; }

    public string? DeployCommand { get; set; }

    public bool UseSudoForCommands { get; set; }

    public string? EncryptedEnvironment { get; set; }

    public bool IsDeleted { get; set; }

    public DateTime? DeletedAt { get; set; }

    public string? DeletedBy { get; set; }
}
