namespace ServerManager.Application.DTOs.Cloud;

public sealed record CloudCreateServerRequest(string Name, string Region, string Size, string Image, string? UserData);
