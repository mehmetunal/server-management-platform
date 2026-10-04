namespace ServerManager.Application.DTOs.Settings;

public sealed record DatabaseStatusDto(bool IsConnected, long? SchemaVersion, string? Message);
