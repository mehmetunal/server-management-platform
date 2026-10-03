using FluentValidation;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Application.Validators.Docker;

public class CreateVolumeDtoValidator : AbstractValidator<CreateVolumeDto>
{
    public CreateVolumeDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Volume adı zorunludur.")
            .MaximumLength(DockerNames.MaxLength).WithMessage($"Volume adı en fazla {DockerNames.MaxLength} karakter olabilir.")
            .Must(DockerNames.IsValidVolumeName)
            .WithMessage("Volume adı harf veya rakamla başlamalı; yalnızca harf, rakam, alt çizgi, nokta ve tire içerebilir (en az 2 karakter).");
    }
}
