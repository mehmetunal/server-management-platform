namespace ServerManager.Application.Agent;

public sealed record AgentReportOutcome(AgentReportStatus Status, string Message);
