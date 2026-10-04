namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudProvisionResultDto(Guid AccountId, CloudServerInfo Server, string? RootPassword);
