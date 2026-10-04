namespace ServerManager.Application.DTOs.Cloud;

/// <param name="RootPassword">Sağlayıcı yalnızca oluşturma yanıtında verir; saklanmaz, kullanıcıya bir kez gösterilir.</param>
public sealed record CloudCreateResult(CloudServerInfo Server, string? RootPassword);
