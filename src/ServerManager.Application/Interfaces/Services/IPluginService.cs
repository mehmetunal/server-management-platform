using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Plugins;

namespace ServerManager.Application.Interfaces.Services;

public interface IPluginService
{
    Task<IReadOnlyList<PluginDto>> GetPluginsAsync(CancellationToken cancellationToken = default);

    /// <summary>Eklentinin migration'larını uygular, izinlerini rollere ekler ve eklentiyi etkinleştirir.</summary>
    Task<ServiceResult> InstallAsync(string systemName, CancellationToken cancellationToken = default);

    /// <summary>Devre dışı eklentinin sayfaları, sekmeleri ve arka plan işleri çalışmaz; tabloları ve verisi korunur.</summary>
    Task<ServiceResult> SetEnabledAsync(string systemName, bool enabled, CancellationToken cancellationToken = default);

    /// <summary>
    /// Açılışta kurulum durumlarını yükler, <c>Plugins:InstallOnStartup</c> listesindeki eklentileri ilk kez kurar ve
    /// kurulu eklentilerin yeni migration'larını ve izinlerini uygular.
    /// </summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);
}
