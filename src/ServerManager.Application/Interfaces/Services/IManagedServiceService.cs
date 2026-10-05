using ServerManager.Application.Common;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.DTOs.Terminal;
using ServerManager.Application.Interfaces.ManagedServices;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.Interfaces.Services;

/// <summary>Başlatılan arka plan işleminin kimlikleri.</summary>
public sealed record ServiceOperationStart(Guid ServiceId, Guid OperationId);

/// <summary>
/// "Servisler" modülü: sunucuda tek tıkla Docker tabanlı veritabanı ve uygulama servisleri.
/// Kurulum/yeniden oluşturma/yükseltme/kaldırma <c>Begin*</c> ile kayda alınır ve <see cref="RunOperationAsync"/> ile arka planda yürütülür.
/// </summary>
public interface IManagedServiceService
{
    // ---- Entegrasyon (diğer modüller için) ----

    /// <summary>
    /// Servise Docker iç ağından bağlanmak için gereken bilgi: container adı ve iç port, kimlik bilgileri, iç ağ biçiminde
    /// bağlantı adresi, önerilen ortam değişkenleri ve servisin katıldığı ağlar. Parola içerir; çağıran taraf değeri
    /// yalnızca sunucuda kullanmalı (ör. şifreli ortam değişkenine yazmalı), loglamamalı ve tarayıcıya göndermemelidir.
    /// Çağrı audit log'a yazılmaz; kullanıcıya parola gösterilecekse <see cref="RevealSecretsAsync"/> kullanılmalıdır.
    /// </summary>
    Task<ServiceResult<ManagedServiceConnectionInfo>> GetConnectionInfoAsync(Guid serviceId, CancellationToken cancellationToken = default);

    // ---- Okuma ----

    /// <param name="serverId">null ise tüm sunuculardaki servisler.</param>
    Task<IReadOnlyList<ManagedServiceListItemDto>> ListAsync(Guid? serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ManagedServiceDetailsDto>> GetAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Ayarlar sekmesinin formu; ek ortam değişkenlerinin değerleri çözülmüş olarak döner.</summary>
    Task<ServiceResult<UpdateManagedServiceDto>> GetSettingsAsync(Guid id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ManagedServiceOperationDto>> ListOperationsAsync(Guid serviceId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ManagedServiceOperationDto>> GetOperationAsync(Guid operationId, bool includeLog, CancellationToken cancellationToken = default);

    /// <summary>Kimlik bilgilerini ve parolalı bağlantı adreslerini döner; audit log'a yazılır.</summary>
    Task<ServiceResult<ManagedServiceSecretsDto>> RevealSecretsAsync(Guid id, CancellationToken cancellationToken = default);

    // ---- Sunucu ----

    Task<ServiceResult<ServiceHostProbe>> ProbeServerAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<IReadOnlyList<string>>> ListServerNetworksAsync(Guid serverId, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceRuntimeState>> GetRuntimeAsync(Guid id, CancellationToken cancellationToken = default);

    Task<ServiceResult<DockerLogsDto>> GetLogsAsync(Guid id, int? tail, string? since, CancellationToken cancellationToken = default);

    /// <summary>Başlat / durdur / yeniden başlat (diğer işlemler kabul edilmez).</summary>
    Task<ServiceResult> ExecuteContainerActionAsync(Guid id, DockerContainerAction action, CancellationToken cancellationToken = default);

    /// <summary>Güvenlik duvarı kurallarını yeniden yazar (ör. sunucu yeniden başladıktan sonra).</summary>
    Task<ServiceResult> ReapplyFirewallAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Konsol sekmesi: şablonun istemci komutuyla container'da terminal açar ve audit log'a yazar.</summary>
    Task<ServiceResult<TerminalHandle>> OpenConsoleAsync(
        Guid id,
        int columns,
        int rows,
        ServiceActor actor,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);

    // ---- Arka plan işlemleri ----

    Task<ServiceResult<ServiceOperationStart>> BeginInstallAsync(CreateManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceOperationStart>> BeginRecreateAsync(Guid id, UpdateManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceOperationStart>> BeginUpgradeAsync(Guid id, UpgradeManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceOperationStart>> BeginRemoveAsync(Guid id, RemoveManagedServiceDto dto, ServiceActor actor, CancellationToken cancellationToken = default);

    Task<ServiceResult> RunOperationAsync(Guid operationId, ServiceActor actor, IServiceOperationObserver observer, CancellationToken cancellationToken);

    Task<int> InterruptRunningAsync(CancellationToken cancellationToken = default);
}
