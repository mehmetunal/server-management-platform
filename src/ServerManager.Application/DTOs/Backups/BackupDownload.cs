namespace ServerManager.Application.DTOs.Backups;

/// <summary>Çağıran akışı kapatmakla yükümlüdür. <paramref name="Length"/> biliniyorsa Content-Length olarak gönderilir.</summary>
public sealed record BackupDownload(Stream Content, string FileName, string ContentType, long? Length = null);
