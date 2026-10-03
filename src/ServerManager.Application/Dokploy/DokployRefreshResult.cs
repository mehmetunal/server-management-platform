using ServerManager.Application.DTOs.Dokploy;
using ServerManager.Domain.Entities;

namespace ServerManager.Application.Dokploy;

internal sealed record DokployRefreshResult(DokployInstance? Instance, DokployHostStatusDto? Host, string? HostError);
