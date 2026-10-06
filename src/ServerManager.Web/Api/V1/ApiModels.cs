using ServerManager.Application.Common;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Application.DTOs.Backups;
using ServerManager.Application.DTOs.Deployments;
using ServerManager.Application.DTOs.ManagedServices;
using ServerManager.Application.DTOs.Monitoring;
using ServerManager.Application.DTOs.Servers;
using ServerManager.Application.ManagedServices;

namespace ServerManager.Web.Api.V1;

// API sözleşmesi panel DTO'larından ayrıdır: enum'lar metin, gizli bilgi yok, alan adları camelCase.

/// <summary>Sayfalı liste.</summary>
public sealed record ApiPage<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount, int TotalPages)
{
    public static ApiPage<T> From<TSource>(PagedResult<TSource> page, Func<TSource, T> map) =>
        new(page.Items.Select(map).ToList(), page.Page, page.PageSize, page.TotalCount, page.TotalPages);
}

/// <summary>Arka planda başlatılan işlem (202 Accepted).</summary>
public sealed record ApiAccepted(Guid Id, string Message, string StatusUrl);

public sealed record ApiMessage(string Message);

public sealed record ApiMe(Guid UserId, string UserName, Guid KeyId, string KeyName, IReadOnlyList<string> Permissions);

public sealed record ApiServer(
    Guid Id,
    string Name,
    string Hostname,
    string IpAddress,
    int SshPort,
    string Environment,
    string Status,
    string? OperatingSystem,
    string? Provider,
    string? Location,
    IReadOnlyList<string> Tags,
    string? GroupName,
    DateTime? LastConnectionTestAt,
    bool? LastConnectionSucceeded)
{
    public static ApiServer From(ServerListItemDto s) => new(
        s.Id, s.Name, s.Hostname, s.IpAddress, s.SshPort, s.Environment.ToString(), s.Status.ToString(), s.OperatingSystem,
        s.Provider, s.Location, s.Tags, s.GroupName, s.LastConnectionTestAt, s.LastConnectionSucceeded);

    public static ApiServer From(ServerDetailsDto s) => new(
        s.Id, s.Name, s.Hostname, s.IpAddress, s.SshPort, s.Environment.ToString(), s.Status.ToString(), s.OperatingSystem,
        s.Provider, s.Location, s.Tags, s.GroupName, s.LastConnectionTestAt, s.LastConnectionSucceeded);
}

public sealed record ApiServerMetrics(
    DateTime CollectedAt,
    double CpuUsagePercent,
    double MemoryUsagePercent,
    double DiskUsagePercent,
    double LoadAverage1,
    long UptimeSeconds)
{
    public static ApiServerMetrics From(ServerResourceSummaryDto m) =>
        new(m.CollectedAt, m.CpuUsagePercent, m.MemoryUsagePercent, m.DiskUsagePercent, m.LoadAverage1, m.UptimeSeconds);
}

public sealed record ApiServerStatus(Guid Id, string Name, string Status, DateTime? LastSeenAt, bool MonitoringEnabled, ApiServerMetrics? LatestMetrics);

public sealed record ApiProject(
    Guid Id,
    string Name,
    Guid ServerId,
    string ServerName,
    string RepositoryUrl,
    string Branch,
    string BuildType,
    ApiDeployment? LastDeployment)
{
    public static ApiProject From(ProjectListItemDto p) => new(
        p.Id, p.Name, p.ServerId, p.ServerName, p.RepositoryUrl, p.Branch, p.BuildType.ToString(),
        p.LastDeployment is null ? null : ApiDeployment.From(p.LastDeployment));
}

public sealed record ApiDeployment(
    Guid Id,
    Guid ProjectId,
    string ProjectName,
    Guid ServerId,
    string Kind,
    string Status,
    bool IsRunning,
    string Branch,
    string? CommitSha,
    string? CommitMessage,
    string? FailureReason,
    string? UserName,
    DateTime StartedAt,
    DateTime? CompletedAt)
{
    public static ApiDeployment From(DeploymentListItemDto d) => new(
        d.Id, d.ProjectId, d.ProjectName, d.ServerId, d.Kind.ToString(), d.Status.ToString(), d.IsRunning, d.Branch, d.CommitSha,
        d.CommitMessage, d.FailureReason, d.UserName, d.StartedAt, d.CompletedAt);
}

public sealed record ApiDeploymentLog(Guid DeploymentId, string Status, bool IsRunning, int TotalLines, bool Truncated, IReadOnlyList<string> Lines);

/// <summary>Deploy başlatma isteği.</summary>
public sealed class ApiStartDeployment
{
    /// <summary>Boşsa projenin dalındaki son commit dağıtılır.</summary>
    public string? CommitSha { get; set; }
}

public sealed record ApiManagedService(
    Guid Id,
    Guid ServerId,
    string ServerName,
    string Name,
    string Template,
    string ImageTag,
    string ContainerName,
    string Status,
    bool IsBusy,
    string? LastError,
    bool ExposePublicly,
    string? WebUiUrl,
    DateTime CreatedAt)
{
    public static ApiManagedService From(ManagedServiceListItemDto s) => new(
        s.Id, s.ServerId, s.ServerName, s.Name, s.TemplateKey, s.ImageTag, s.ContainerName, s.Status.ToString(), s.IsBusy,
        s.LastError, s.ExposePublicly, s.WebUiUrl, s.CreatedAt);
}

public sealed record ApiManagedServiceStatus(Guid Id, bool Exists, string? State, string? Health, DateTime? StartedAt, int RestartCount, string? Image)
{
    public static ApiManagedServiceStatus From(Guid id, ServiceRuntimeState s) =>
        new(id, s.Exists, s.State, s.Health, s.StartedAt, s.RestartCount, s.Image);
}

public sealed record ApiBackupJob(
    Guid Id,
    string Name,
    Guid ServerId,
    string ServerName,
    string StorageName,
    string SourceType,
    string SourceSummary,
    string ScheduleType,
    DateTime? NextRunAt,
    DateTime? LastRunAt,
    string? LastRunStatus,
    Guid? LatestBackupRunId,
    bool EncryptionEnabled,
    bool IsEnabled)
{
    public static ApiBackupJob From(BackupJobListItemDto j) => new(
        j.Id, j.Name, j.ServerId, j.ServerName, j.StorageName, j.SourceType.ToString(), j.SourceSummary, j.ScheduleType.ToString(),
        j.NextRunAt, j.LastRunAt, j.LastRunStatus?.ToString(), j.LatestBackup?.RunId, j.EncryptionEnabled, j.IsEnabled);
}

public sealed record ApiBackupRun(
    Guid Id,
    Guid? JobId,
    string JobName,
    string Operation,
    string Trigger,
    string Status,
    bool IsRunning,
    Guid ServerId,
    string ServerName,
    string StorageName,
    string? FileName,
    long? SizeBytes,
    bool IsEncrypted,
    bool IsArtifactAvailable,
    string? FailureReason,
    string? UserName,
    DateTime StartedAt,
    DateTime? CompletedAt)
{
    public static ApiBackupRun From(BackupRunListItemDto r) => new(
        r.Id, r.JobId, r.JobName, r.Operation.ToString(), r.Trigger.ToString(), r.Status.ToString(), r.IsRunning, r.ServerId,
        r.ServerName, r.StorageName, r.FileName, r.SizeBytes, r.IsEncrypted, r.IsArtifactAvailable, r.FailureReason, r.UserName,
        r.StartedAt, r.CompletedAt);
}

public sealed record ApiAlert(
    Guid Id,
    Guid RuleId,
    string RuleName,
    string Kind,
    string Severity,
    Guid? ServerId,
    string? ServerName,
    string TargetName,
    string Status,
    string Message,
    DateTime StartedAt,
    DateTime? ResolvedAt,
    DateTime? AcknowledgedAt,
    string? AcknowledgedBy)
{
    public static ApiAlert From(AlertEventDto a) => new(
        a.Id, a.RuleId, a.RuleName, a.Kind.ToString(), a.Severity.ToString(), a.ServerId, a.ServerName, a.TargetName,
        a.Status.ToString(), a.Message, a.StartedAt, a.ResolvedAt, a.AcknowledgedAt, a.AcknowledgedBy);
}
