namespace ServerManager.Application.ServerSystem;

public sealed record BlockDeviceEntry(string Name, string Type, long? SizeBytes, string? MountPoint, string? FileSystem, string? Model);
