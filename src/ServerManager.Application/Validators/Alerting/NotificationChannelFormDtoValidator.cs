using FluentValidation;
using ServerManager.Application.DTOs.Alerting;

namespace ServerManager.Application.Validators.Alerting;

/// <summary>Ortak alanlar; sağlayıcıya özgü alanlar sağlayıcının tanımına göre serviste doğrulanır.</summary>
public sealed class NotificationChannelFormDtoValidator : AbstractValidator<NotificationChannelFormDto>
{
    public const int MaxSettings = 30;

    public NotificationChannelFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kanal adı zorunludur.")
            .MaximumLength(128).WithMessage("Kanal adı en fazla 128 karakter olabilir.");

        RuleFor(x => x.ProviderSystemName)
            .NotEmpty().WithMessage("Kanal türünü seçin.")
            .MaximumLength(100);

        RuleFor(x => x.MinimumSeverity)
            .IsInEnum().WithMessage("Geçerli bir önem derecesi seçin.");

        RuleFor(x => x.Settings)
            .Must(settings => settings.Count <= MaxSettings).WithMessage("Çok fazla ayar gönderildi.");
    }
}
