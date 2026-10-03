using FluentValidation;
using ServerManager.Application.Deployments;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Validators.Deployments;

public sealed class StartDeploymentDtoValidator : AbstractValidator<StartDeploymentDto>
{
    public StartDeploymentDtoValidator()
    {
        RuleFor(x => x.CommitSha)
            .Must(commit => GitRefs.IsValidCommit(GitRefs.NormalizeCommit(commit)))
            .WithMessage("Commit için tam özeti girin (40 karakterlik SHA-1 veya 64 karakterlik SHA-256).")
            .When(x => !string.IsNullOrWhiteSpace(x.CommitSha));
    }
}
