using System.Text.RegularExpressions;

namespace ServerManager.Application.Deployments;

/// <summary>Dal adı ve commit doğrulaması; değerler shell'e tırnaklı verilse de git seçeneği gibi yorumlanmamaları gerekir.</summary>
public static partial class GitRefs
{
    public const int MaxBranchLength = 200;

    public static bool IsValidBranch(string? branch)
    {
        if (string.IsNullOrEmpty(branch) || branch.Length > MaxBranchLength || !BranchCharacters().IsMatch(branch))
            return false;

        return !branch.StartsWith('-')
               && !branch.StartsWith('/')
               && !branch.StartsWith('.')
               && !branch.EndsWith('/')
               && !branch.EndsWith('.')
               && !branch.EndsWith(".lock", StringComparison.Ordinal)
               && !branch.Contains("..", StringComparison.Ordinal)
               && !branch.Contains("//", StringComparison.Ordinal)
               && !branch.Contains("/.", StringComparison.Ordinal);
    }

    /// <summary>Yalnızca tam commit özeti (SHA-1 40 veya SHA-256 64 karakter); kısa özet sunucudan doğrudan alınamaz.</summary>
    public static bool IsValidCommit(string? commit) => commit is not null && CommitPattern().IsMatch(commit);

    public static string? NormalizeCommit(string? commit)
    {
        var trimmed = commit?.Trim();
        return string.IsNullOrEmpty(trimmed) ? null : trimmed.ToLowerInvariant();
    }

    public static string ShortSha(string? sha) =>
        string.IsNullOrEmpty(sha) ? string.Empty : sha.Length <= 12 ? sha : sha[..12];

    [GeneratedRegex("^[A-Za-z0-9._/-]+$")]
    private static partial Regex BranchCharacters();

    [GeneratedRegex("^(?:[0-9a-f]{40}|[0-9a-f]{64})$")]
    private static partial Regex CommitPattern();
}
