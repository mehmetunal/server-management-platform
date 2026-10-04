using ServerManager.Domain.Enums;

namespace ServerManager.Application.Security;

/// <param name="Risk">Ağa açık olduğunda bulgu seviyesi; boşsa olağan bir servistir.</param>
public sealed record KnownPort(int Port, string Service, SecurityCheckStatus? Risk = null, string? Note = null);
