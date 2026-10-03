using FluentValidation;
using ServerManager.Application.DTOs.Account;

namespace ServerManager.Application.Validators.Account;

public sealed class LoginDtoValidator : AbstractValidator<LoginDto>
{
    public LoginDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parola zorunludur.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.");
    }
}
