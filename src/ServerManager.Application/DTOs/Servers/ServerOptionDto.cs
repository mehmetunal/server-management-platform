namespace ServerManager.Application.DTOs.Servers;

public sealed record ServerOptionDto(Guid Id, string Name, string IpAddress, Guid? GroupId = null);
