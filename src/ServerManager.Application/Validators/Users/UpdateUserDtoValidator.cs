using FluentValidation;
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

        // Rollerin var olup olmadığı serviste denetlenir (özel roller veritabanındadır).
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("En az bir rol seçin.")
            .Must(r => r.All(name => !string.IsNullOrWhiteSpace(name) && name.Length <= 256)).WithMessage("Geçerli bir rol seçin.");

        RuleFor(x => x.NewPassword)
            .MinimumLength(UserPasswordRules.MinimumLength).WithMessage($"Parola en az {UserPasswordRules.MinimumLength} karakter olmalıdır.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));

        RuleFor(x => x.ConfirmNewPassword)
            .Equal(x => x.NewPassword).WithMessage("Parolalar eşleşmiyor.")
            .When(x => !string.IsNullOrEmpty(x.NewPassword));
    }
}
