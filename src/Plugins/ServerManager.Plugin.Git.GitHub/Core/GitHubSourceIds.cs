using System.Globalization;

namespace ServerManager.Plugin.Git.GitHub.Core;

/// <summary>Projede saklanan bağlantı kimliği: <c>{uygulama kaydı (N)}:{kurulum kimliği}</c>.</summary>
public static class GitHubSourceIds
{
    public static string Format(Guid appRecordId, long installationId) =>
        $"{appRecordId:N}:{installationId.ToString(CultureInfo.InvariantCulture)}";

    public static bool TryParse(string? sourceId, out Guid appRecordId, out long installationId)
    {
        appRecordId = Guid.Empty;
        installationId = 0;
        if (string.IsNullOrWhiteSpace(sourceId))
            return false;

        var separator = sourceId.IndexOf(':');
        return separator > 0
            && Guid.TryParseExact(sourceId[..separator], "N", out appRecordId)
            && long.TryParse(sourceId[(separator + 1)..], NumberStyles.None, CultureInfo.InvariantCulture, out installationId)
            && installationId > 0;
    }

    /// <summary>Bu uygulama kaydına bağlı projeleri bulmak için kimlik öneki.</summary>
    public static string Prefix(Guid appRecordId) => $"{appRecordId:N}:";
}
