using ServerManager.Plugin.DevOps.Dokku.DTOs;

namespace ServerManager.Plugin.DevOps.Dokku.Integration;

public sealed record DokkuReport(bool IsInstalled, string? Version, IReadOnlyList<DokkuAppDto> Apps);
