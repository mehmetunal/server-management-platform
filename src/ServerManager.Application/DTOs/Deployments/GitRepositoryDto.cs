namespace ServerManager.Application.DTOs.Deployments;

public sealed record GitRepositoryDto(string FullName, string CloneUrl, string DefaultBranch, bool IsPrivate, string? HtmlUrl);
