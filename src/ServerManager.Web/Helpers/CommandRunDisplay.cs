using ServerManager.Application.Commands;
using ServerManager.Domain.Enums;

namespace ServerManager.Web.Helpers;

public static class CommandRunDisplay
{
    public static string StatusText(CommandRunStatus status) => status switch
    {
        CommandRunStatus.Running => "Çalışıyor",
        CommandRunStatus.Completed => "Tamamlandı",
        CommandRunStatus.Interrupted => "Kesildi",
        _ => status.ToString()
    };

    public static string StatusBadgeClass(CommandRunStatus status, int failedCount) => status switch
    {
        CommandRunStatus.Running => "badge-info",
        CommandRunStatus.Interrupted => "badge-warning",
        _ when failedCount > 0 => "badge-danger",
        _ => "badge-success"
    };

    public static string TargetStatusText(CommandTargetStatus status) => status switch
    {
        CommandTargetStatus.Pending => "Sırada",
        CommandTargetStatus.Running => "Çalışıyor",
        CommandTargetStatus.Succeeded => "Başarılı",
        CommandTargetStatus.Failed => "Başarısız",
        CommandTargetStatus.TimedOut => "Zaman aşımı",
        CommandTargetStatus.Interrupted => "Kesildi",
        _ => status.ToString()
    };

    public static string TargetBadgeClass(CommandTargetStatus status) => status switch
    {
        CommandTargetStatus.Pending => "badge-neutral",
        CommandTargetStatus.Running => "badge-info",
        CommandTargetStatus.Succeeded => "badge-success",
        CommandTargetStatus.Interrupted or CommandTargetStatus.TimedOut => "badge-warning",
        _ => "badge-danger"
    };

    public static string Milliseconds(long? value) => value switch
    {
        null => "—",
        < 1000 => $"{value} ms",
        < 60_000 => $"{value / 1000d:0.0} sn",
        _ => $"{value / 60_000} dk {value % 60_000 / 1000} sn"
    };

    public static string KindText(ServerTemplateKind kind) => ServerTemplateKinds.DisplayName(kind);

    public static string KindBadgeClass(ServerTemplateKind kind) => kind == ServerTemplateKind.CloudInit ? "badge-info" : "badge-neutral";

    public static string FirstLine(string command)
    {
        var index = command.IndexOf('\n');
        return index < 0 ? command : command[..index] + " …";
    }
}
