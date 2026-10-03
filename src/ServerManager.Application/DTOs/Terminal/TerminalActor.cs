namespace ServerManager.Application.DTOs.Terminal;

/// <summary>Terminal oturumunu açan kullanıcı. Oturum HTTP isteğinden uzun yaşadığı için kimlik açıkça taşınır.</summary>
public sealed record TerminalActor(Guid SessionId, string UserId, string? UserName, string? IpAddress);
