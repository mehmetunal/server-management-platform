using FluentValidation;
using ServerManager.Application.DTOs.Backups;

namespace ServerManager.Application.Validators.Backups;

/// <summary>Ortak alanlar; sağlayıcıya özgü alanlar sağlayıcının tanımına göre serviste doğrulanır.</summary>
public sealed class BackupStorageFormDtoValidator : AbstractValidator<BackupStorageFormDto>
{
    public const int MaxSettings = 30;

    public BackupStorageFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Depolama adı zorunludur.")
            .MaximumLength(128).WithMessage("Depolama adı en fazla 128 karakter olabilir.");

        RuleFor(x => x.ProviderSystemName)
            .NotEmpty().WithMessage("Depolama türünü seçin.")
            .MaximumLength(100);

        RuleFor(x => x.Settings)
            .Must(settings => settings.Count <= MaxSettings).WithMessage("Çok fazla ayar gönderildi.");
    }
}
