using FluentValidation;
using ServerManager.Application.Authorization;
using ServerManager.Application.DTOs.Users;

namespace ServerManager.Application.Validators.Users;

public sealed class UpdateUserDtoValidator : AbstractValidator<UpdateUserDto>
{
    public UpdateUserDtoValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Kullanıcı kimliği zorunludur.");

        RuleFor(x => x.FullName)
            .MaximumLength(128).WithMessage("Ad soyad en fazla 128 karakter olabilir.");

        RuleFor(x => x.Role)
            .Must(r => Roles.All.Contains(r)).WithMessage("Geçerli bir rol seçin.");

        RuleFor(x => x.NewPassword)
            .MinimumLength(UserPasswordRules.MinimumLength).WithMessage($"Parola en az {UserPasswordRules.MinimumLength} karakter olmalıdır.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Parolalar eşleşmiyor.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}
