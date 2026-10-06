using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using ServerManager.Application.Auditing;
using ServerManager.Application.Authorization;
using ServerManager.Application.Common;
using ServerManager.Application.DTOs.AuditLogs;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Interfaces.Repositories;
using ServerManager.Application.Interfaces.Services;
using ServerManager.Application.ManagedServices;
using ServerManager.Application.Plugins;
using ServerManager.Application.Services;
using ServerManager.Application.Tests.Fakes;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Tests.Plugins;

public class PluginServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 3, 12, 0, 0, TimeSpan.Zero);

    private readonly IPluginRepository _repository = Substitute.For<IPluginRepository>();
    private readonly IPluginMigrator _migrator = Substitute.For<IPluginMigrator>();
    private readonly IPermissionSeeder _seeder = Substitute.For<IPermissionSeeder>();
    private readonly IAuditLogService _auditLog = Substitute.For<IAuditLogService>();
    private readonly ICurrentUserService _currentUser = Substitute.For<ICurrentUserService>();
    private readonly PluginOptions _options = new();

    public PluginServiceTests()
    {
        _currentUser.UserName.Returns("admin@example.com");
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([]);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private PluginService CreateService(PluginCatalog catalog) => new(
        catalog,
        _repository,
        _migrator,
        _seeder,
        [new TestPluginPermissionProvider()],
        _auditLog,
        _currentUser,
        Options.Create(_options),
        new FixedTimeProvider(Now),
        NullLogger<PluginService>.Instance,
        new ServiceTemplateCatalog(catalog, [], [], NullLogger<ServiceTemplateCatalog>.Instance));

    [Fact]
    public async Task Install_runs_migrations_seeds_permissions_and_enables_plugin()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);
        IReadOnlyDictionary<string, IReadOnlyList<string>>? seeded = null;
        await _seeder.SeedAsync(Arg.Do<IReadOnlyDictionary<string, IReadOnlyList<string>>>(m => seeded = m), Arg.Any<CancellationToken>());
        InstalledPlugin? added = null;
        await _repository.AddAsync(Arg.Do<InstalledPlugin>(p => added = p), Arg.Any<CancellationToken>());

        var result = await CreateService(catalog).InstallAsync(PluginTestData.SystemName, Ct);

        Assert.True(result.IsSuccess);
        _migrator.Received(1).MigrateUp(PluginTestData.PluginAssembly);
        Assert.NotNull(seeded);
        Assert.Equal([TestPluginPermissionProvider.View, TestPluginPermissionProvider.Manage], seeded[Roles.SuperAdmin]);
        Assert.Equal([TestPluginPermissionProvider.View, TestPluginPermissionProvider.Manage], seeded[Roles.Admin]);
        Assert.Equal([TestPluginPermissionProvider.View], seeded[Roles.Viewer]);
        Assert.NotNull(added);
        Assert.True(added.IsEnabled);
        Assert.Equal("1.0.0", added.Version);
        Assert.Equal("admin@example.com", added.InstalledBy);
        Assert.Equal(Now.UtcDateTime, added.InstalledAt);
        Assert.True(catalog.IsEnabled(PluginTestData.SystemName));
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.PluginInstall && e.EntityType == AuditEntityTypes.Plugin
                                    && e.EntityId == PluginTestData.SystemName && e.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Install_rejects_unknown_plugin()
    {
        var result = await CreateService(new PluginCatalog([])).InstallAsync("Missing.Plugin", Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.NotFound, result.ErrorType);
    }

    [Fact]
    public async Task Install_rejects_plugin_that_failed_to_load()
    {
        var catalog = new PluginCatalog([PluginTestData.Failed("DevOps.Broken", "Eklenti yüklenemedi: eksik bağımlılık")]);

        var result = await CreateService(catalog).InstallAsync("DevOps.Broken", Ct);

        Assert.False(result.IsSuccess);
        Assert.Contains("eksik bağımlılık", result.Message);
        _migrator.DidNotReceiveWithAnyArgs().MigrateUp(default!);
    }

    [Fact]
    public async Task Install_rejects_already_installed_plugin()
    {
        _repository.GetAsync(PluginTestData.SystemName, Arg.Any<CancellationToken>())
            .Returns(new InstalledPlugin { SystemName = PluginTestData.SystemName, Version = "1.0.0", IsEnabled = false });

        var result = await CreateService(new PluginCatalog([PluginTestData.Loaded()])).InstallAsync(PluginTestData.SystemName, Ct);

        Assert.False(result.IsSuccess);
        Assert.Equal(ServiceErrorType.Conflict, result.ErrorType);
        _migrator.DidNotReceiveWithAnyArgs().MigrateUp(default!);
    }

    [Fact]
    public async Task Failed_migration_leaves_plugin_uninstalled_and_is_audited()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);
        _migrator.When(m => m.MigrateUp(PluginTestData.PluginAssembly)).Throw(new InvalidOperationException("tablo var"));

        var result = await CreateService(catalog).InstallAsync(PluginTestData.SystemName, Ct);

        Assert.False(result.IsSuccess);
        Assert.DoesNotContain("tablo var", result.Message);
        Assert.False(catalog.IsInstalled(PluginTestData.SystemName));
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
        await _auditLog.Received(1).LogAsync(
            Arg.Is<AuditEntry>(e => e.Action == AuditActions.PluginInstall && !e.IsSuccess),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Disable_keeps_record_and_updates_catalog()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded()]);
        catalog.SetState(PluginTestData.SystemName, true);
        var record = new InstalledPlugin { SystemName = PluginTestData.SystemName, Version = "1.0.0", IsEnabled = true };
        _repository.GetAsync(PluginTestData.SystemName, Arg.Any<CancellationToken>()).Returns(record);

        var result = await CreateService(catalog).SetEnabledAsync(PluginTestData.SystemName, false, Ct);

        Assert.True(result.IsSuccess);
        Assert.False(record.IsEnabled);
        Assert.Equal("admin@example.com", record.UpdatedBy);
        Assert.Equal(Now.UtcDateTime, record.UpdatedAt);
        Assert.True(catalog.IsInstalled(PluginTestData.SystemName));
        Assert.False(catalog.IsEnabled(PluginTestData.SystemName));
        await _repository.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _auditLog.Received(1).LogAsync(Arg.Is<AuditEntry>(e => e.Action == AuditActions.PluginDisable), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Enable_requires_installation()
    {
        var result = await CreateService(new PluginCatalog([PluginTestData.Loaded()])).SetEnabledAsync(PluginTestData.SystemName, true, Ct);

        Assert.False(result.IsSuccess);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
    }

    [Fact]
    public async Task Enabling_an_enabled_plugin_is_a_no_op()
    {
        var record = new InstalledPlugin { SystemName = PluginTestData.SystemName, Version = "1.0.0", IsEnabled = true };
        _repository.GetAsync(PluginTestData.SystemName, Arg.Any<CancellationToken>()).Returns(record);

        var result = await CreateService(new PluginCatalog([PluginTestData.Loaded()])).SetEnabledAsync(PluginTestData.SystemName, true, Ct);

        Assert.True(result.IsSuccess);
        await _repository.DidNotReceiveWithAnyArgs().SaveChangesAsync(Ct);
        await _auditLog.DidNotReceiveWithAnyArgs().LogAsync(default!, Ct);
    }

    [Fact]
    public async Task Initialize_restores_state_and_applies_new_migrations_of_installed_plugins()
    {
        var catalog = new PluginCatalog([PluginTestData.Loaded(version: "1.1.0")]);
        var record = new InstalledPlugin { SystemName = PluginTestData.SystemName, Version = "1.0.0", IsEnabled = false };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([record]);

        await CreateService(catalog).InitializeAsync(Ct);

        _migrator.Received(1).MigrateUp(PluginTestData.PluginAssembly);
        await _seeder.Received(1).SeedAsync(Arg.Any<IReadOnlyDictionary<string, IReadOnlyList<string>>>(), Arg.Any<CancellationToken>());
        Assert.True(catalog.IsInstalled(PluginTestData.SystemName));
        Assert.False(catalog.IsEnabled(PluginTestData.SystemName));
        Assert.Equal("1.1.0", record.Version);
        Assert.Equal("Sistem", record.UpdatedBy);
    }

    [Fact]
    public async Task Initialize_installs_plugins_listed_for_startup_only_once()
    {
        _options.InstallOnStartup.Add(PluginTestData.SystemName.ToLowerInvariant());
        var catalog = new PluginCatalog([PluginTestData.Loaded(), PluginTestData.Loaded("Other.Plugin")]);
        var added = new List<InstalledPlugin>();
        await _repository.AddAsync(Arg.Do<InstalledPlugin>(added.Add), Arg.Any<CancellationToken>());

        await CreateService(catalog).InitializeAsync(Ct);

        var installed = Assert.Single(added);
        Assert.Equal(PluginTestData.SystemName, installed.SystemName);
        Assert.Equal("Sistem", installed.InstalledBy);
        Assert.True(catalog.IsEnabled(PluginTestData.SystemName));
        Assert.False(catalog.IsInstalled("Other.Plugin"));
    }

    [Fact]
    public async Task Initialize_skips_plugins_that_failed_to_load()
    {
        _options.InstallOnStartup.Add("DevOps.Broken");
        var record = new InstalledPlugin { SystemName = "DevOps.Broken", Version = "1.0.0", IsEnabled = true };
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns([record]);
        var catalog = new PluginCatalog([PluginTestData.Failed("DevOps.Broken")]);

        await CreateService(catalog).InitializeAsync(Ct);

        _migrator.DidNotReceiveWithAnyArgs().MigrateUp(default!);
        await _repository.DidNotReceiveWithAnyArgs().AddAsync(default!, Ct);
        Assert.False(catalog.IsEnabled("DevOps.Broken"));
    }

    [Fact]
    public async Task Listing_marks_records_whose_files_failed_to_load_as_disabled()
    {
        _repository.GetAllAsync(Arg.Any<CancellationToken>()).Returns(
        [
            new InstalledPlugin { SystemName = "DevOps.Broken", Version = "1.0.0", IsEnabled = true },
            new InstalledPlugin { SystemName = PluginTestData.SystemName, Version = "0.9.0", IsEnabled = true }
        ]);
        var catalog = new PluginCatalog([PluginTestData.Loaded(), PluginTestData.Failed("DevOps.Broken")]);

        var plugins = await CreateService(catalog).GetPluginsAsync(Ct);

        var broken = plugins.Single(p => p.SystemName == "DevOps.Broken");
        Assert.True(broken.IsInstalled);
        Assert.False(broken.IsEnabled);
        Assert.NotNull(broken.LoadError);
        var sample = plugins.Single(p => p.SystemName == PluginTestData.SystemName);
        Assert.True(sample.IsEnabled);
        Assert.Equal("0.9.0", sample.InstalledVersion);
        Assert.Equal("1.0.0", sample.Version);
    }
}
