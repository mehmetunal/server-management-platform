using FluentValidation;
using ServerManager.Application.DTOs.Account;

namespace ServerManager.Application.Validators.Account;

public sealed class PasswordConfirmationDtoValidator : AbstractValidator<PasswordConfirmationDto>
{
    public PasswordConfirmationDtoValidator()
    {
        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parolanızı girin.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.");
    }
}
