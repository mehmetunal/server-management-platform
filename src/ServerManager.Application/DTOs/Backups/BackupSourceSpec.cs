using ServerManager.Domain.Enums;

namespace ServerManager.Application.DTOs.Backups;

/// <summary>Sunucuda yedeği alınacak veya geri yüklenecek kaynak. Veritabanı parolası komut satırına yazılmaz; stdin ile verilir.</summary>
public sealed class BackupSourceSpec
{
    public BackupSourceType Type { get; init; }

    /// <summary>Normalleştirilmiş mutlak yollar.</summary>
    public IReadOnlyList<string> Paths { get; init; } = [];

    public IReadOnlyList<string> Excludes { get; init; } = [];

    public string? VolumeName { get; init; }

    public BackupDatabaseEngine? Engine { get; init; }

    public string? ContainerName { get; init; }

    public string? DatabaseName { get; init; }

    public string? DatabaseUser { get; init; }

    public string? DatabasePassword { get; init; }

    public string? DatabaseHost { get; init; }

    public int? DatabasePort { get; init; }

    /// <summary>Dosya geri yüklemesinde arşivin açılacağı klasör ("/" özgün konumlar).</summary>
    public string? TargetDirectory { get; init; }

    public override string ToString() =>
        $"BackupSourceSpec {{ Type = {Type}, Volume = {VolumeName}, Engine = {Engine}, Container = {ContainerName}, Database = {DatabaseName}, Paths = {Paths.Count} }}";
}
