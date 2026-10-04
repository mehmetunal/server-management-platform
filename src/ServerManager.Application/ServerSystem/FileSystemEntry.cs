namespace ServerManager.Application.ServerSystem;

public sealed record FileSystemEntry(
    string Device,
    string? Type,
    long SizeKilobytes,
    long UsedKilobytes,
    long AvailableKilobytes,
    int UsePercent,
    string MountPoint,
    int? InodeUsePercent);
