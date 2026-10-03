namespace ServerManager.Domain.Entities;

public class AlertRuleChannel
{
    public Guid RuleId { get; set; }

    public Guid ChannelId { get; set; }

    public NotificationChannel? Channel { get; set; }
}
