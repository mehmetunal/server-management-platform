using FluentValidation;
using ServerManager.Application.DTOs.Account;
using ServerManager.Application.Validators.Users;

namespace ServerManager.Application.Validators.Account;

public sealed class ChangePasswordDtoValidator : AbstractValidator<ChangePasswordDto>
{
    public ChangePasswordDtoValidator()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage("Mevcut parolanızı girin.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Yeni parola zorunludur.")
            .MinimumLength(UserPasswordRules.MinimumLength).WithMessage($"Parola en az {UserPasswordRules.MinimumLength} karakter olmalıdır.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.")
            .NotEqual(x => x.CurrentPassword).WithMessage("Yeni parola mevcut paroladan farklı olmalıdır.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.NewPassword).WithMessage("Parolalar eşleşmiyor.");
    }
}
