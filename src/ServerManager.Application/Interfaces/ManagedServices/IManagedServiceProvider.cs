using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Ssh;
using ServerManager.Application.Interfaces.Ssh;
using ServerManager.Application.ManagedServices;

namespace ServerManager.Application.Interfaces.ManagedServices;

/// <param name="FailureReason">Kullanıcıya gösterilen Türkçe neden; başarılıysa null.</param>
public sealed record ServiceOperationResult(bool Succeeded, string? FailureReason = null, int? ExitCode = null)
{
    public static ServiceOperationResult Success() => new(true);

    public static ServiceOperationResult Failed(string reason, int? exitCode = null) => new(false, reason, exitCode);
}

/// <summary>Servis container'larını SSH üzerinden kuran, kaldıran ve durumunu okuyan altyapı.</summary>
public interface IManagedServiceProvider
{
    /// <summary>Docker kurulu mu, sürümü, işlemci mimarisi ve Dokploy/Dokku kurulumu.</summary>
    Task<ServiceResult<ServiceHostProbe>> ProbeAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Kurulum, yeniden oluşturma ve sürüm yükseltme: Docker kontrolü → port çakışması → imaj → ağ/volume → container →
    /// sağlık → bağlantı testi. Aşamalar ve çıktı <paramref name="observer"/>'a iletilir.
    /// </summary>
    Task<ServiceResult<ServiceOperationResult>> DeployAsync(
        RemoteExecutionContext context,
        ManagedServicePlan plan,
        IServiceOperationObserver observer,
        CancellationToken cancellationToken = default);

    /// <summary>Container'ı, güvenlik duvarı kurallarını ve istenirse veriyi siler.</summary>
    Task<ServiceResult<ServiceOperationResult>> RemoveAsync(
        RemoteExecutionContext context,
        ManagedServiceRemovalPlan plan,
        IServiceOperationObserver observer,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<ServiceRuntimeState>> GetRuntimeAsync(RemoteExecutionContext context, string slug, CancellationToken cancellationToken = default);

    /// <summary>Kuralları siler ve plandaki kuralları yeniden yazar (idempotent).</summary>
    Task<ServiceResult> ApplyFirewallAsync(RemoteExecutionContext context, ServiceFirewallPlan plan, CancellationToken cancellationToken = default);

    /// <summary>Sunucudaki Docker ağlarının adları (formdaki ağ seçimi için).</summary>
    Task<ServiceResult<IReadOnlyList<string>>> ListNetworksAsync(RemoteExecutionContext context, CancellationToken cancellationToken = default);

    /// <summary>
    /// Container içinde etkileşimli konsol açar. <paramref name="consoleCommand"/> şablondan gelir (istemciden asla alınmaz);
    /// null ise bash/sh açılır.
    /// </summary>
    Task<ServiceResult<ITerminalSession>> OpenConsoleAsync(
        RemoteExecutionContext context,
        string container,
        string? consoleCommand,
        int columns,
        int rows,
        ITerminalOutputSink sink,
        CancellationToken cancellationToken = default);
}
