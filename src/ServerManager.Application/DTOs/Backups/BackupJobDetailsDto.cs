using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

public sealed class BackupJobDetailsDto
{
    public required BackupJobListItemDto Job { get; init; }

    public IReadOnlyList<string> Paths { get; init; } = [];

    public IReadOnlyList<string> Excludes { get; init; } = [];

    public string? VolumeName { get; init; }

    public BackupDatabaseEngine? DatabaseEngine { get; init; }

    public string? ContainerName { get; init; }

    public string? DatabaseName { get; init; }

    public string? DatabaseUser { get; init; }

    public bool HasDatabasePassword { get; init; }

    public string? DatabaseHost { get; init; }

    public int? DatabasePort { get; init; }

    public DateTime CreatedAt { get; init; }

    public string? CreatedBy { get; init; }

    public DateTime? UpdatedAt { get; init; }

    public string? UpdatedBy { get; init; }
}
