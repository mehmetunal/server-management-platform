using System.Net;

namespace ServerManager.Plugin.Git.GitHub.DTOs;

public sealed record GitHubHttpResponse(HttpStatusCode StatusCode, string Body, string? Error)
{
    public static GitHubHttpResponse Failed(string error) => new(0, string.Empty, error);
}
