using FluentValidation;
using ServerManager.Application.Docker;
using ServerManager.Application.DTOs.Docker;

namespace ServerManager.Application.Validators.Docker;

public class RenameContainerDtoValidator : AbstractValidator<RenameContainerDto>
{
    public RenameContainerDtoValidator()
    {
        RuleFor(x => x.Container)
            .Must(DockerNames.IsValidContainerReference).WithMessage("Geçersiz container.");

        RuleFor(x => x.NewName)
            .NotEmpty().WithMessage("Yeni ad zorunludur.")
            .MaximumLength(DockerNames.MaxLength).WithMessage($"Ad en fazla {DockerNames.MaxLength} karakter olabilir.")
            .Must(DockerNames.IsValidContainerName)
            .WithMessage("Ad harf veya rakamla başlamalı; yalnızca harf, rakam, alt çizgi, nokta ve tire içerebilir (en az 2 karakter).");
    }
}
