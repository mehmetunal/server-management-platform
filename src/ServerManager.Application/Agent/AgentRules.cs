using System.Text.RegularExpressions;

namespace ServerManager.Application.Agent;

public static partial class AgentRules
{
    public const string CurrentVersion = "1.0.0";
    public const int ReportIntervalSeconds = 60;
    public const int MinReportIntervalSeconds = 20;
    public const int MaxReportBytes = 256 * 1024;

    /// <summary>Bu süre içinde rapor gönderen agent'ın sunucusu SSH ile ayrıca toplanmaz.</summary>
    public static readonly TimeSpan ActiveWindow = TimeSpan.FromMinutes(3);

    /// <summary>SSH ile toplanamayan sunucuda agent bu süre sessiz kalırsa sunucu çevrimdışı sayılır.</summary>
    public static readonly TimeSpan SilentAfter = TimeSpan.FromMinutes(5);

    public static bool IsActive(DateTime? lastSeenAt, DateTime nowUtc) =>
        lastSeenAt is { } seen && nowUtc - seen <= ActiveWindow;

    public static string? NormalizeVersion(string? version) =>
        version is not null && VersionPattern().IsMatch(version.Trim()) ? version.Trim() : null;

    [GeneratedRegex(@"^[0-9A-Za-z.+\-]{1,32}$")]
    private static partial Regex VersionPattern();
}
