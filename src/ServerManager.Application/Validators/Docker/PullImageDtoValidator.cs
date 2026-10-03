using FluentValidation;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Application.Validators.Docker;

public class PullImageDtoValidator : AbstractValidator<PullImageDto>
{
    public PullImageDtoValidator()
    {
        RuleFor(x => x.Reference)
            .NotEmpty().WithMessage("Image adı zorunludur.")
            .MaximumLength(DockerNames.MaxLength).WithMessage($"Image adı en fazla {DockerNames.MaxLength} karakter olabilir.")
            .Must(DockerNames.IsValidImageReference)
            .WithMessage("Geçersiz image adı. Örnek: nginx:alpine, ghcr.io/org/app:1.2.0");
    }
}
