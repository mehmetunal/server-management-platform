using Microsoft.Extensions.Options;
using NSubstitute;
using ServerManager.Application.Alerting;
using ServerManager.Application.Backups;
using ServerManager.Application.Cloud;
using ServerManager.Application.DTOs.Settings;
using ServerManager.Application.Interfaces;
using ServerManager.Application.Monitoring;
using ServerManager.Application.Security;
using ServerManager.Application.Services;

namespace ServerManager.Application.Tests.Services;

public class SystemInfoServiceTests
{
    private readonly IDatabaseInfoReader _database = Substitute.For<IDatabaseInfoReader>();

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private SystemInfoService Service(
        MonitoringOptions? monitoring = null,
        CloudOptions? cloud = null,
        SecurityScanOptions? scan = null) =>
        new(
            _database,
            Options.Create(monitoring ?? new MonitoringOptions()),
            Options.Create(new AlertingOptions()),
            Options.Create(new BackupOptions()),
            Options.Create(scan ?? new SecurityScanOptions()),
            Options.Create(cloud ?? new CloudOptions()));

    [Fact]
    public async Task GetAsync_reports_database_and_enabled_features()
    {
        _database.GetAsync(Arg.Any<CancellationToken>()).Returns(new DatabaseStatusDto(true, 202610040016, null));

        var info = await Service().GetAsync(Ct);

        Assert.Equal("1.0.0", info.Version);
        Assert.True(info.Database.IsConnected);
        Assert.Equal(202610040016, info.Database.SchemaVersion);
        Assert.Contains(info.Sections, s => s.Title == "İzleme" && s.Items.Any(i => i.Label == "Otomatik toplama" && i.Value == "Açık"));
        Assert.Contains(info.Sections, s => s.Title == "Bulut" && s.Items.Single().Value == "Her 6 saatte");
        Assert.Contains(info.Sections, s => s.Title == "Agent" && s.Items.Any(i => i.Value == "Her 1 dakikada"));
    }

    [Fact]
    public async Task GetAsync_shows_disabled_intervals_as_closed()
    {
        _database.GetAsync(Arg.Any<CancellationToken>()).Returns(new DatabaseStatusDto(false, null, "Veritabanına bağlanılamadı."));

        var info = await Service(
            new MonitoringOptions { Enabled = false, IntervalSeconds = 45 },
            new CloudOptions { SyncIntervalHours = 0 },
            new SecurityScanOptions { ScanIntervalHours = 0 }).GetAsync(Ct);

        Assert.False(info.Database.IsConnected);
        Assert.Contains(info.Sections.Single(s => s.Title == "İzleme").Items, i => i.Value == "Kapalı");
        Assert.Contains(info.Sections.Single(s => s.Title == "İzleme").Items, i => i.Value == "Her 45 saniyede");
        Assert.Equal("Kapalı", info.Sections.Single(s => s.Title == "Bulut").Items.Single().Value);
        Assert.Equal("Kapalı", info.Sections.Single(s => s.Title == "Güvenlik taraması").Items.First().Value);
    }
}
