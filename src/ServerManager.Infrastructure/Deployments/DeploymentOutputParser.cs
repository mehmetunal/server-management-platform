using ServerManager.Application.Deployments;

namespace ServerManager.Infrastructure.Deployments;

public static class DeploymentOutputParser
{
    private const string HeadsPrefix = "refs/heads/";

    /// <summary><c>git ls-remote --heads</c> çıktısındaki dal adlarını sıralı döndürür.</summary>
    public static IReadOnlyList<string> ParseBranches(string output) =>
        output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(line => line.Split('\t', 2))
            .Where(parts => parts.Length == 2 && parts[1].StartsWith(HeadsPrefix, StringComparison.Ordinal))
            .Select(parts => parts[1][HeadsPrefix.Length..])
            .Where(name => name.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)
            .ToList();

    /// <summary>Son satırdaki <c>%H%x1f%an%x1f%s</c> biçimli commit bilgisini okur (bkz. <see cref="DeploymentCommands.ReadCommit"/>).</summary>
    public static DeploymentCommit? ParseCommit(string output)
    {
        var line = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault();
        if (line is null)
            return null;

        var parts = line.Split(DeploymentCommands.CommitFieldSeparator);
        var sha = parts[0].ToLowerInvariant();
        if (!GitRefs.IsValidCommit(sha))
            return null;

        return new DeploymentCommit(sha, NullIfEmpty(parts.ElementAtOrDefault(1)), NullIfEmpty(parts.ElementAtOrDefault(2)));
    }

    private static string? NullIfEmpty(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
