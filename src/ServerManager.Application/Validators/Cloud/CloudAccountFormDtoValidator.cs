using FluentValidation;
using ServerManager.Application.DTOs.Cloud;

namespace ServerManager.Application.Validators.Cloud;

public sealed class CloudAccountFormDtoValidator : AbstractValidator<CloudAccountFormDto>
{
    public CloudAccountFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Hesap adı zorunludur.")
            .MaximumLength(64).WithMessage("Hesap adı en fazla 64 karakter olabilir.");

        RuleFor(x => x.Provider)
            .NotEmpty().WithMessage("Sağlayıcı seçin.");

        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("API anahtarı zorunludur.")
            .When(x => x.Id is null);

        RuleFor(x => x.Token)
            .MaximumLength(500).WithMessage("API anahtarı en fazla 500 karakter olabilir.")
            .Must(t => t is null || !t.Any(char.IsWhiteSpace)).WithMessage("API anahtarı boşluk içeremez.");
    }
}
