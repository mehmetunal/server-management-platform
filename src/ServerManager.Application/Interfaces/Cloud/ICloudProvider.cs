using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Cloud;

namespace ServerManager.Application.Interfaces.Cloud;

/// <summary>Bulut sağlayıcı API'si. Eklentide tanımlanır; API anahtarı her çağrıda çözülmüş olarak verilir ve saklanmaz.</summary>
public interface ICloudProvider
{
    string SystemName { get; }

    string DisplayName { get; }

    /// <summary>API anahtarının nereden ve hangi yetkiyle alınacağı.</summary>
    string TokenHelp { get; }

    /// <summary>Sağlayıcının fiyat para birimi (ISO 4217).</summary>
    string Currency { get; }

    /// <summary>Anahtarı doğrular; başarılıysa hesabı tanımlayan kısa bir etiket döner.</summary>
    Task<ServiceResult<string>> ValidateTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<CloudServerInfo>>> ListServersAsync(string token, CancellationToken cancellationToken = default);

    Task<ServiceResult<CloudCatalog>> GetCatalogAsync(string token, CancellationToken cancellationToken = default);

    Task<ServiceResult<CloudCreateResult>> CreateServerAsync(string token, CloudCreateServerRequest request, CancellationToken cancellationToken = default);
}
