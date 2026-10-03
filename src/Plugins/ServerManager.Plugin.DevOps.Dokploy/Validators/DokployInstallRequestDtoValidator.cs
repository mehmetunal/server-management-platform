using FluentValidation;
using ServerManager.Plugin.DevOps.Dokploy.Core;
using ServerManager.Plugin.DevOps.Dokploy.DTOs;

namespace ServerManager.Plugin.DevOps.Dokploy.Validators;

public class DokployInstallRequestDtoValidator : AbstractValidator<DokployInstallRequestDto>
{
    public DokployInstallRequestDtoValidator()
    {
        RuleFor(x => x.Version)
            .Must(DokployVersions.IsValid)
            .WithMessage("Geçersiz sürüm. Boş bırakın veya latest, canary ya da v0.25.3 biçiminde girin.")
            .When(x => !string.IsNullOrEmpty(x.Version));

        RuleFor(x => x.ConfirmationName)
            .NotEmpty().WithMessage("Onay için sunucu adını yazın.");
    }
}
