namespace ServerManager.Application.DTOs.Deployments;

public sealed class StartDeploymentDto
{
    /// <summary>Boşsa dalın son commit'i dağıtılır.</summary>
    public string? CommitSha { get; set; }
}
