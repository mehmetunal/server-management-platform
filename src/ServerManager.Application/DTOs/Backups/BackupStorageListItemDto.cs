namespace ServerManager.Application.DTOs.Backups;

public sealed record BackupStorageListItemDto(
    Guid Id,
    string Name,
    string ProviderSystemName,
    string ProviderDisplayName,
    bool IsProviderEnabled,
    int JobCount,
    DateTime? LastTestedAt,
    string? LastError);
