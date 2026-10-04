using FluentValidation;
using ServerManager.Application.DTOs.Account;

namespace ServerManager.Application.Validators.Account;

public sealed class EnableAuthenticatorDtoValidator : AbstractValidator<EnableAuthenticatorDto>
{
    public EnableAuthenticatorDtoValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Uygulamanın gösterdiği kodu girin.")
            .Must(TwoFactorCodes.IsAuthenticatorCode).WithMessage("Doğrulama kodu 6 haneli olmalıdır.");
    }
}
