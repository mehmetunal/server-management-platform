namespace ServerManager.Application.DTOs.Deployments;

/// <summary>Arka planda süren deployment'ı başlatan veya iptal eden kullanıcı; HTTP isteği bittikten sonra audit için kullanılır.</summary>
public sealed record DeploymentActor(string? UserId, string? UserName, string? IpAddress);
