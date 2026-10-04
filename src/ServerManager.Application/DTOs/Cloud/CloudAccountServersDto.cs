namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudAccountServersDto(CloudAccountListItemDto Account, IReadOnlyList<CloudServerListItemDto> Servers);
