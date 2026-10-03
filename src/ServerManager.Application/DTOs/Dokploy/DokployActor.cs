namespace ServerManager.Application.DTOs.Dokploy;

/// <summary>Arka planda süren kurulumu başlatan kullanıcı; HTTP isteği bittikten sonra audit için kullanılır.</summary>
public sealed record DokployActor(string? UserId, string? UserName, string? IpAddress);
