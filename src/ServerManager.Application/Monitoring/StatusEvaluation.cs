using ServerManager.Domain.Enums;

namespace ServerManager.Application.Monitoring;

public sealed record StatusEvaluation(ServerStatus Status, IReadOnlyList<string> Reasons);
