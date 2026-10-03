using FluentValidation;
using ServerManager.Application.DTOs.Deployments;

namespace ServerManager.Application.Validators.Deployments;

public sealed class UpdateProjectDtoValidator : ProjectFormDtoValidator<UpdateProjectDto>
{
    public UpdateProjectDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Proje kimliği zorunludur.");
    }
}
