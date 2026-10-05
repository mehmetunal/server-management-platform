using ServerManager.Domain.Common;

namespace ServerManager.Domain.Entities;

/// <summary>
/// Deployment projesi ile aynı sunucudaki yönetilen servis arasındaki bağ ("Projeye bağla"). Bağlı projenin container'ları
/// servislerin panel ağına (<c>sm-services</c>) katılır; bağlantı bilgisi projenin ortam değişkenlerine yazılır.
/// </summary>
public class ProjectServiceLink : BaseEntity
{
    public Guid ProjectId { get; set; }

    public DeploymentProject? Project { get; set; }

    public Guid ManagedServiceId { get; set; }

    public ManagedService? ManagedService { get; set; }

    /// <summary>Bağlarken projeye yazılan (eklenen veya değeri değişen) ortam değişkeni anahtarları; virgülle ayrılmış.</summary>
    public string? EnvironmentKeys { get; set; }
}
