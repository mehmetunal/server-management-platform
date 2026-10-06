using FluentValidation;
using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Alerting;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Alerting;

public sealed class AlertRuleFormDtoValidator : AbstractValidator<AlertRuleFormDto>
{
    public const int MaxChannels = 20;
    public const int MaxRepeatMinutes = 10080;

    public AlertRuleFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kural adı zorunludur.")
            .MaximumLength(128).WithMessage("Kural adı en fazla 128 karakter olabilir.");

        RuleFor(x => x.Kind)
            .IsInEnum().WithMessage("Geçerli bir kural türü seçin.");

        RuleFor(x => x.Severity)
            .IsInEnum().WithMessage("Geçerli bir önem derecesi seçin.");

        RuleFor(x => x.Threshold)
            .InclusiveBetween(1, 100).WithMessage("Eşik 1 ile 100 arasında bir yüzde olmalıdır.")
            .When(x => AlertRuleKinds.IsMetric(x.Kind));

        RuleFor(x => x.Threshold)
            .InclusiveBetween(1, AlertRuleKinds.MaxSslThresholdDays).WithMessage($"Gün sayısı 1 ile {AlertRuleKinds.MaxSslThresholdDays} arasında olmalıdır.")
            .When(x => x.Kind == AlertRuleKind.SslCertificateExpiry);

        RuleFor(x => x.Threshold)
            .InclusiveBetween(1, AlertRuleKinds.MaxRestartThreshold).WithMessage($"Yeniden başlama sayısı 1 ile {AlertRuleKinds.MaxRestartThreshold} arasında olmalıdır.")
            .When(x => x.Kind == AlertRuleKind.ContainerRestartLoop);

        RuleFor(x => x.Threshold)
            .InclusiveBetween(1, AlertRuleKinds.MaxReclaimableGigabytes).WithMessage($"Eşik 1 ile {AlertRuleKinds.MaxReclaimableGigabytes} GB arasında olmalıdır.")
            .When(x => x.Kind == AlertRuleKind.ReclaimableSpace);

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(5, AlertRuleKinds.MaxDurationMinutes).WithMessage($"Pencere 5 ile {AlertRuleKinds.MaxDurationMinutes} dakika arasında olmalıdır.")
            .When(x => x.Kind == AlertRuleKind.ContainerRestartLoop);

        RuleFor(x => x.DurationMinutes)
            .InclusiveBetween(0, AlertRuleKinds.MaxDurationMinutes).WithMessage($"Süre 0 ile {AlertRuleKinds.MaxDurationMinutes} dakika arasında olmalıdır.")
            .When(x => AlertRuleKinds.UsesDuration(x.Kind));

        RuleFor(x => x.RepeatIntervalMinutes)
            .InclusiveBetween(0, MaxRepeatMinutes).WithMessage($"Tekrar aralığı 0 ile {MaxRepeatMinutes} dakika arasında olmalıdır.")
            .Must(minutes => minutes == 0 || minutes >= 5).WithMessage("Tekrar aralığı en az 5 dakika olmalıdır (0 tekrarlamaz).");

        RuleFor(x => x.ChannelIds)
            .Must(ids => ids.Count <= MaxChannels).WithMessage($"En fazla {MaxChannels} kanal seçilebilir.");
    }
}
