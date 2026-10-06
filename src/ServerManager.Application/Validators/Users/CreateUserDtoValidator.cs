using FluentValidation;
using ServerManager.Application.DTOs.Users;

namespace ServerManager.Application.Validators.Users;

public sealed class CreateUserDtoValidator : AbstractValidator<CreateUserDto>
{
    public CreateUserDtoValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("E-posta zorunludur.")
            .EmailAddress().WithMessage("Geçerli bir e-posta adresi girin.")
            .MaximumLength(256).WithMessage("E-posta en fazla 256 karakter olabilir.");

        RuleFor(x => x.FullName)
            .MaximumLength(128).WithMessage("Ad soyad en fazla 128 karakter olabilir.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Parola zorunludur.")
            .MinimumLength(UserPasswordRules.MinimumLength).WithMessage($"Parola en az {UserPasswordRules.MinimumLength} karakter olmalıdır.")
            .MaximumLength(256).WithMessage("Parola en fazla 256 karakter olabilir.");

        RuleFor(x => x.ConfirmPassword)
            .Equal(x => x.Password).WithMessage("Parolalar eşleşmiyor.");

        // Rollerin var olup olmadığı serviste denetlenir (özel roller veritabanındadır).
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("En az bir rol seçin.")
            .Must(r => r.All(name => !string.IsNullOrWhiteSpace(name) && name.Length <= 256)).WithMessage("Geçerli bir rol seçin.");
    }
}
