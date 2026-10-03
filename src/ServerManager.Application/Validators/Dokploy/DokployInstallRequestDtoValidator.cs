using FluentValidation;
using ServerManager.Application.Dokploy;
using ServerManager.Application.DTOs.Dokploy;

namespace ServerManager.Application.Validators.Dokploy;

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
