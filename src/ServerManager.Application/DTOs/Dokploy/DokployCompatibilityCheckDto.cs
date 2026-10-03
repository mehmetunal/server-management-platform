using ServerManager.Application.Dokploy;

namespace ServerManager.Application.DTOs.Dokploy;

public sealed record DokployCompatibilityCheckDto(string Key, string Title, DokployCheckStatus Status, string Detail);
