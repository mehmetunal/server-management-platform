namespace ServerManager.Application.DTOs.Deployments;

public sealed class UpdateProjectDto : ProjectFormDto
{
    public Guid Id { get; set; }

    public string Slug { get; set; } = string.Empty;

    public bool HasAccessToken { get; set; }

    public bool RemoveAccessToken { get; set; }

    public IReadOnlyList<string> EnvironmentKeys { get; set; } = [];

    public bool HasEnvironment { get; set; }

    public bool RemoveEnvironment { get; set; }
}
