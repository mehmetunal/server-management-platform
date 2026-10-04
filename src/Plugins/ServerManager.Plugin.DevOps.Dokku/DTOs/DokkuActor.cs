namespace ServerManager.Plugin.DevOps.Dokku.DTOs;

/// <summary>Arka planda süren kurulumu başlatan kullanıcı; HTTP isteği bittikten sonra audit için kullanılır.</summary>
public sealed record DokkuActor(string? UserId, string? UserName, string? IpAddress);
