using FluentValidation;
using ServerManager.Application.Alerting;
using ServerManager.Application.DTOs.Uptime;
using ServerManager.Domain.Enums;

namespace ServerManager.Application.Validators.Alerting;

public sealed class UptimeCheckFormDtoValidator : AbstractValidator<UptimeCheckFormDto>
{
    public const int MaxIntervalSeconds = 86400;
    public const int MaxTimeoutSeconds = 60;

    public UptimeCheckFormDtoValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Kontrol adı zorunludur.")
            .MaximumLength(128).WithMessage("Kontrol adı en fazla 128 karakter olabilir.");

        RuleFor(x => x.Type)
            .IsInEnum().WithMessage("Geçerli bir kontrol türü seçin.");

        RuleFor(x => x.Url).Custom((url, context) =>
        {
            if (!NetworkTargets.TryValidateUrl(url, out var error))
                context.AddFailure(nameof(UptimeCheckFormDto.Url), error!);
        }).When(x => x.Type == UptimeCheckType.Http);

        RuleFor(x => x.AcceptedStatusCodes)
            .Must(codes => StatusCodeRanges.TryParse(codes, out _))
            .WithMessage("Durum kodlarını 200-399 veya 200,204,301 biçiminde girin (100-599).")
            .When(x => x.Type == UptimeCheckType.Http);

        RuleFor(x => x.Host)
            .Must(NetworkTargets.IsValidHost).WithMessage("Geçerli bir sunucu adı veya IP adresi girin.")
            .When(x => x.Type == UptimeCheckType.Tcp);

        RuleFor(x => x.Port)
            .NotNull().WithMessage("Port zorunludur.")
            .InclusiveBetween(1, 65535).WithMessage("Port 1 ile 65535 arasında olmalıdır.")
            .When(x => x.Type == UptimeCheckType.Tcp);

        RuleFor(x => x.IntervalSeconds)
            .InclusiveBetween(10, MaxIntervalSeconds).WithMessage($"Kontrol aralığı 10 ile {MaxIntervalSeconds} saniye arasında olmalıdır.");

        RuleFor(x => x.TimeoutSeconds)
            .InclusiveBetween(1, MaxTimeoutSeconds).WithMessage($"Zaman aşımı 1 ile {MaxTimeoutSeconds} saniye arasında olmalıdır.")
            .LessThan(x => x.IntervalSeconds).WithMessage("Zaman aşımı kontrol aralığından kısa olmalıdır.");
    }
}
