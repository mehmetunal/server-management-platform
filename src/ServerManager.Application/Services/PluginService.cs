using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.DTOs.Plugins;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.Plugins;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Services;

public class PluginService : IPluginService
{
    private const string SystemUserName = "Sistem";

    private readonly IPluginCatalog _catalog;
    private readonly IPluginRepository _pluginRepository;
    private readonly IPluginMigrator _migrator;
    private readonly IPermissionSeeder _permissionSeeder;
    private readonly IEnumerable<IPermissionProvider> _permissionProviders;
    private readonly IAuditLogService _auditLogService;
    private readonly ICurrentUserService _currentUser;
    private readonly PluginOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<PluginService> _logger;

    public PluginService(
        IPluginCatalog catalog,
        IPluginRepository pluginRepository,
        IPluginMigrator migrator,
        IPermissionSeeder permissionSeeder,
        IEnumerable<IPermissionProvider> permissionProviders,
        IAuditLogService auditLogService,
        ICurrentUserService currentUser,
        IOptions<PluginOptions> options,
        TimeProvider timeProvider,
        ILogger<PluginService> logger)
    {
        _catalog = catalog;
        _pluginRepository = pluginRepository;
        _migrator = migrator;
        _permissionSeeder = permissionSeeder;
        _permissionProviders = permissionProviders;
        _auditLogService = auditLogService;
        _currentUser = currentUser;
        _options = options.Value;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public async Task<IReadOnlyList<PluginDto>> GetPluginsAsync(CancellationToken cancellationToken = default)
    {
        var installed = (await _pluginRepository.GetAllAsync(cancellationToken))
            .ToDictionary(p => p.SystemName, StringComparer.OrdinalIgnoreCase);

        return _catalog.Plugins
            .Select(plugin =>
            {
                var record = installed.GetValueOrDefault(plugin.SystemName);
                return new PluginDto
                {
                    SystemName = plugin.SystemName,
                    FriendlyName = plugin.Descriptor.FriendlyName,
                    Group = plugin.Descriptor.Group,
                    Version = plugin.Descriptor.Version,
                    Author = plugin.Descriptor.Author,
                    Description = plugin.Descriptor.Description,
                    IsLoaded = plugin.IsLoaded,
                    LoadError = plugin.LoadError,
                    IsInstalled = record is not null,
                    IsEnabled = record?.IsEnabled == true && plugin.IsLoaded,
                    InstalledVersion = record?.Version,
                    InstalledAt = record?.InstalledAt,
                    InstalledBy = record?.InstalledBy
                };
            })
            .ToList();
    }

    public async Task<ServiceResult> InstallAsync(string systemName, CancellationToken cancellationToken = default)
    {
        var plugin = _catalog.Find(systemName);
        if (plugin is null)
            return ServiceResult.NotFound("Eklenti bulunamadı.");

        if (!plugin.IsLoaded)
            return ServiceResult.Failure($"Eklenti yüklenemediği için kurulamaz: {plugin.LoadError}");

        if (await _pluginRepository.GetAsync(plugin.SystemName, cancellationToken) is not null)
            return ServiceResult.Failure("Eklenti zaten kurulu.", ServiceErrorType.Conflict);

        try
        {
            await InstallCoreAsync(plugin, _currentUser.UserName, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Eklenti kurulamadı: {Plugin}", plugin.SystemName);
            await LogAsync(AuditActions.PluginInstall, plugin, "Kurulum başarısız oldu; ayrıntılar uygulama logundadır.", false, cancellationToken);
            return ServiceResult.Failure("Eklenti kurulamadı. Ayrıntılar uygulama logundadır.");
        }

        await LogAsync(AuditActions.PluginInstall, plugin, $"Sürüm: {plugin.Descriptor.Version}", true, cancellationToken);
        return ServiceResult.Success($"{plugin.Descriptor.FriendlyName} eklentisi kuruldu ve etkinleştirildi.");
    }

    public async Task<ServiceResult> SetEnabledAsync(string systemName, bool enabled, CancellationToken cancellationToken = default)
    {
        var plugin = _catalog.Find(systemName);
        if (plugin is null)
            return ServiceResult.NotFound("Eklenti bulunamadı.");

        if (enabled && !plugin.IsLoaded)
            return ServiceResult.Failure($"Eklenti yüklenemediği için etkinleştirilemez: {plugin.LoadError}");

        var record = await _pluginRepository.GetAsync(plugin.SystemName, cancellationToken);
        if (record is null)
            return ServiceResult.Failure("Eklenti kurulu değil. Önce kurun.");

        if (record.IsEnabled == enabled)
            return ServiceResult.Success(enabled ? "Eklenti zaten etkin." : "Eklenti zaten devre dışı.");

        record.IsEnabled = enabled;
        record.UpdatedAt = UtcNow;
        record.UpdatedBy = _currentUser.UserName;
        await _pluginRepository.SaveChangesAsync(cancellationToken);
        _catalog.SetState(plugin.SystemName, enabled);

        await LogAsync(enabled ? AuditActions.PluginEnable : AuditActions.PluginDisable, plugin, null, true, cancellationToken);
        return ServiceResult.Success(enabled
            ? $"{plugin.Descriptor.FriendlyName} eklentisi etkinleştirildi."
            : $"{plugin.Descriptor.FriendlyName} eklentisi devre dışı bırakıldı. Verileri korunur.");
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        foreach (var plugin in _catalog.Plugins.Where(p => !p.IsLoaded))
            _logger.LogError("Eklenti yüklenemedi: {Plugin} — {Error}", plugin.SystemName, plugin.LoadError);

        var installed = (await _pluginRepository.GetAllAsync(cancellationToken))
            .ToDictionary(p => p.SystemName, StringComparer.OrdinalIgnoreCase);

        foreach (var plugin in _catalog.Plugins.Where(p => p.IsLoaded))
        {
            if (installed.TryGetValue(plugin.SystemName, out var record))
            {
                await UpgradeAsync(plugin, record, cancellationToken);
                _catalog.SetState(plugin.SystemName, record.IsEnabled);
            }
            else if (_options.InstallOnStartup.Contains(plugin.SystemName, StringComparer.OrdinalIgnoreCase))
            {
                await InstallCoreAsync(plugin, SystemUserName, cancellationToken);
                _logger.LogInformation("Eklenti açılışta kuruldu: {Plugin} {Version}", plugin.SystemName, plugin.Descriptor.Version);
            }
        }
    }

    private async Task InstallCoreAsync(LoadedPlugin plugin, string? userName, CancellationToken cancellationToken)
    {
        _migrator.MigrateUp(plugin.Assembly!);
        await _permissionSeeder.SeedAsync(DefaultPermissionsOf(plugin), cancellationToken);

        await _pluginRepository.AddAsync(new InstalledPlugin
        {
            SystemName = plugin.SystemName,
            Version = plugin.Descriptor.Version,
            IsEnabled = true,
            InstalledAt = UtcNow,
            InstalledBy = userName
        }, cancellationToken);
        await _pluginRepository.SaveChangesAsync(cancellationToken);
        _catalog.SetState(plugin.SystemName, true);
    }

    private async Task UpgradeAsync(LoadedPlugin plugin, InstalledPlugin record, CancellationToken cancellationToken)
    {
        _migrator.MigrateUp(plugin.Assembly!);
        await _permissionSeeder.SeedAsync(DefaultPermissionsOf(plugin), cancellationToken);

        if (string.Equals(record.Version, plugin.Descriptor.Version, StringComparison.Ordinal))
            return;

        _logger.LogInformation("Eklenti güncellendi: {Plugin} {OldVersion} → {NewVersion}", plugin.SystemName, record.Version, plugin.Descriptor.Version);
        record.Version = plugin.Descriptor.Version;
        record.UpdatedAt = UtcNow;
        record.UpdatedBy = SystemUserName;
        await _pluginRepository.SaveChangesAsync(cancellationToken);
    }

    private IReadOnlyDictionary<string, IReadOnlyList<string>> DefaultPermissionsOf(LoadedPlugin plugin)
    {
        var matrix = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var provider in _permissionProviders.Where(p => _catalog.FindByAssembly(p.GetType().Assembly) == plugin))
        {
            foreach (var (role, permissions) in provider.GetDefaultRolePermissions())
                Add(role, permissions);

            Add(Roles.SuperAdmin, provider.GetPermissions().Select(p => p.Name));
        }

        return matrix.ToDictionary(kv => kv.Key, kv => (IReadOnlyList<string>)kv.Value.Distinct(StringComparer.Ordinal).ToList());

        void Add(string role, IEnumerable<string> permissions)
        {
            if (!matrix.TryGetValue(role, out var list))
                matrix[role] = list = [];
            list.AddRange(permissions);
        }
    }

    private Task LogAsync(string action, LoadedPlugin plugin, string? details, bool isSuccess, CancellationToken cancellationToken) =>
        _auditLogService.LogAsync(new AuditEntry(
            action,
            AuditEntityTypes.Plugin,
            plugin.SystemName,
            plugin.Descriptor.FriendlyName,
            details,
            isSuccess), cancellationToken);

    private DateTime UtcNow => _timeProvider.GetUtcNow().UtcDateTime;
}
