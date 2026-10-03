using ServerManager.Domain.Enums;

namespace ServerManager.Application.Alerting;

public sealed record AlertServerSnapshot(Guid Id, string Name, ServerStatus Status, DateTime? LastSeenAt, DateTime CreatedAt);
