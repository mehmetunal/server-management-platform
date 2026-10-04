namespace ServerManager.Application.DTOs.Backups;

/// <summary>Çağıran akışı kapatmakla yükümlüdür.</summary>
public sealed record BackupDownload(Stream Content, string FileName, string ContentType);
