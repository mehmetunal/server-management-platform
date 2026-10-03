using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class TerminalDisplay
{
    public static string KindText(TerminalSessionKind kind) => kind switch
    {
        TerminalSessionKind.Server => "Sunucu",
        TerminalSessionKind.Container => "Container",
        _ => kind.ToString()
    };

    public static string CommandStatusText(TerminalCommandStatus status) => status switch
    {
        TerminalCommandStatus.Executed => "Çalıştırıldı",
        TerminalCommandStatus.Confirmed => "Onaylandı",
        TerminalCommandStatus.Cancelled => "İptal edildi",
        TerminalCommandStatus.Blocked => "Engellendi",
        _ => status.ToString()
    };

    public static string CommandStatusBadgeClass(TerminalCommandStatus status) => status switch
    {
        TerminalCommandStatus.Confirmed => "badge-warning",
        TerminalCommandStatus.Cancelled => "badge-neutral",
        TerminalCommandStatus.Blocked => "badge-danger",
        _ => "badge-success"
    };

    public static string Duration(DateTime startedAt, DateTime? endedAt)
    {
        if (endedAt is null)
            return "Açık";

        var duration = endedAt.Value - startedAt;
        if (duration.TotalMinutes < 1)
            return $"{Math.Max(0, (int)duration.TotalSeconds)} sn";
        if (duration.TotalHours < 1)
            return $"{(int)duration.TotalMinutes} dk";
        return $"{(int)duration.TotalHours} sa {duration.Minutes} dk";
    }
}
