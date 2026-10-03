namespace ServerManager.Plugin.Git.GitHub.DTOs;

/// <summary>Tarayıcının GitHub'a POST edeceği form: <c>manifest</c> alanı <see cref="Manifest"/> değerini taşır.</summary>
public sealed record GitHubManifestStartDto(string PostUrl, string Manifest);
