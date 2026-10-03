namespace ServerManager.Application.Alerting;

/// <summary>SSL alarmı sürerken kalan gün bu eşiklerin altına indikçe bildirim bir kez daha gönderilir.</summary>
public static class SslExpirySteps
{
    public static readonly IReadOnlyList<int> Days = [30, 15, 7, 3, 1, 0];

    public static bool ShouldRemind(double? notifiedDays, double currentDays) =>
        notifiedDays is { } notified && Days.Any(step => currentDays < step && notified >= step);
}
