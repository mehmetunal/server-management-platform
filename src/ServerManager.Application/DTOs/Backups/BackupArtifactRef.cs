namespace ServerManager.Application.DTOs.Backups;

/// <summary>Bir işin dosyası depolamada duran en yeni başarılı yedeği (indirme bağlantısı için).</summary>
public sealed record BackupArtifactRef(Guid JobId, Guid RunId, DateTime StartedAt, bool IsEncrypted, long? SizeBytes);
