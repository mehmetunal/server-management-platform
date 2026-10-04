namespace ServerManager.Application.Backups;

public sealed class BackupOptions
{
    public const string SectionName = "Backup";

    public bool Enabled { get; set; } = true;

    public int SchedulerIntervalSeconds { get; set; } = 30;

    public int MaxConcurrency { get; set; } = 2;

    /// <summary>Günlük/haftalık zamanlamanın yorumlandığı saat dilimi (IANA veya Windows kimliği).</summary>
    public string TimeZone { get; set; } = "Europe/Istanbul";

    public int BackupTimeoutMinutes { get; set; } = 240;

    public int RestoreTimeoutMinutes { get; set; } = 240;

    /// <summary>Yerel depolama klasörlerinin oluşturulabileceği kök dizin (uygulama kök dizinine göre veya mutlak).</summary>
    public string LocalRootPath { get; set; } = "App_Data/backups";

    public int KeyDerivationIterations { get; set; } = BackupEncryption.DefaultIterations;

    public int MaxStoredLogKilobytes { get; set; } = 256;
}
