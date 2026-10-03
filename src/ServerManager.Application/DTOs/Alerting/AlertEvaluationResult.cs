namespace ServerManager.Application.DTOs.Alerting;

public sealed record AlertEvaluationResult(int Rules, int Opened, int Recovered, int Closed, int Reminders, int Deliveries, int FailedDeliveries);
