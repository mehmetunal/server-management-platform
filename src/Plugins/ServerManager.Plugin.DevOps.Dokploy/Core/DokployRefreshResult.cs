using ServerManager.Plugin.DevOps.Dokploy.Domain;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Core;

internal sealed record DokployRefreshResult(DokployInstance? Instance, DokployHostStatusDto? Host, string? HostError);
