namespace ServerManager.Application.DTOs.Backups;

/// <summary>Arka planda süren yedeklemeyi başlatan veya iptal eden kullanıcı; zamanlanmış çalışmada "Sistem".</summary>
public sealed record BackupActor(string? UserId, string? UserName, string? IpAddress)
{
    public static BackupActor System { get; } = new(null, "Sistem", null);
}
