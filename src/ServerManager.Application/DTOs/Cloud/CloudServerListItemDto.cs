namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudServerListItemDto(CloudServerInfo Server, Guid? LinkedServerId, string? LinkedServerName);
