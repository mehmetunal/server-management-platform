using ServerManager.Application.Cleanup;
using ServerManager.Application.ResourceUsage;
using ServerManager.Application.ServerSystem;
using ServerManager.Web.Framework.Servers;

namespace ServerManager.Web.Models;

public sealed class ServerCleanupPageViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required string PanelUrl { get; init; }

    public required CleanupOptions Options { get; init; }
}

public sealed class ServerCleanupPanelModel
{
    public required Guid ServerId { get; init; }

    public required CleanupScan Scan { get; init; }
}

public sealed class ServerResourcesPageViewModel
{
    public required ServerPageViewModel Page { get; init; }

    public required string PanelUrl { get; init; }

    public required string DiskUrl { get; init; }

    public required string HistoryUrl { get; init; }

    public required string SnapshotUrl { get; init; }

    public bool HistoryEnabled { get; init; }

    public int HistoryIntervalMinutes { get; init; }
}

public sealed class ServerResourcesPanelModel
{
    public required Guid ServerId { get; init; }

    public required ResourceOverview Overview { get; init; }

    public bool CanManageProcesses { get; init; }

    public bool CanViewDocker { get; init; }

    public bool CanCleanup { get; init; }
}

/// <summary>Kaynak Kullanımı sayfasındaki "en çok CPU / RAM" tabloları.</summary>
public sealed record ResourceProcessTableModel(
    Guid ServerId,
    string Title,
    string Note,
    IReadOnlyList<ProcessEntry> Processes,
    bool HasCpuUsage,
    bool CanManage);
