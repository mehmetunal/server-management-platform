namespace ServerManager.Application.Agent;

public enum AgentReportStatus
{
    Accepted = 1,
    InvalidToken = 2,
    TooFrequent = 3,
    MonitoringDisabled = 4,
    InvalidReport = 5
}
