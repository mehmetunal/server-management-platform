namespace ServerManager.Application.ServerSystem;

/// <param name="BlockDevicesAvailable">lsblk yoksa (ör. container, BusyBox) disk listesi gösterilmez.</param>
public sealed record StorageSnapshot(IReadOnlyList<FileSystemEntry> FileSystems, bool BlockDevicesAvailable, IReadOnlyList<BlockDeviceEntry> BlockDevices);
