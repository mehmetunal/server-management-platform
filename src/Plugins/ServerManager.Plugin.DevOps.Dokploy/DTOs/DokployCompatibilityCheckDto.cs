using ServerManager.Plugin.DevOps.Dokploy.Core;

namespace ServerManager.Plugin.DevOps.Dokploy.DTOs;

public sealed record DokployCompatibilityCheckDto(string Key, string Title, DokployCheckStatus Status, string Detail);
