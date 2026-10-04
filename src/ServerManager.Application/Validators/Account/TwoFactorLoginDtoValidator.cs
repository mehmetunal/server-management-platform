using FluentValidation;
using ServerManager.Application.DTOs.Account;

namespace ServerManager.Application.Validators.Account;

public sealed class TwoFactorLoginDtoValidator : AbstractValidator<TwoFactorLoginDto>
{
    public TwoFactorLoginDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Doğrulama kodunu girin.")
            .MaximumLength(32).WithMessage("Kod en fazla 32 karakter olabilir.");

        RuleFor(x => x.Code)
            .Must(TwoFactorCodes.IsAuthenticatorCode).WithMessage("Doğrulama kodu 6 haneli olmalıdır.")
            .When(x => !x.UseRecoveryCode && !string.IsNullOrWhiteSpace(x.Code));
    }
}
